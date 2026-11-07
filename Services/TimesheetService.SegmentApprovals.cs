using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    private static async Task<bool> HasAllocationAsync(IUnitOfWork w,Guid id,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.TimesheetAllocationSets WHERE TimesheetId=@id AND ReleasedUtc IS NULL");Id(c,"@id",id);return Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0;}
    private static async Task BeginSegmentRoundAsync(IUnitOfWork w,DataRow t,Guid actor,string name,CancellationToken ct)
    {
        using var q=w.Command("SELECT * FROM dbo.TimesheetAllocationSets WITH(UPDLOCK,HOLDLOCK) WHERE TimesheetId=@id AND ReleasedUtc IS NULL");Id(q,"@id",t.Field<Guid>("TimesheetId"));var rows=await q.TableAsync(ct);if(rows.Rows.Count==0)return;var h=rows.Rows[0];if(!h.Field<bool>("IsSealed"))throw new InvalidOperationException("Dağılım tamamlanmamış.");
        using(var open=w.Command("SELECT COUNT(*) FROM dbo.TimesheetApprovalRounds WITH(UPDLOCK,HOLDLOCK) WHERE SetId=@id AND ClosedUtc IS NULL")){Id(open,"@id",h.Field<Guid>("SetId"));if(Convert.ToInt32(await open.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Dağılımın açık onay turu var.");}
        if(!h.Field<bool>("UsesSegmentApproval")){using var mode=w.Command("UPDATE dbo.TimesheetAllocationSets SET UsesSegmentApproval=1 WHERE SetId=@id");Id(mode,"@id",h.Field<Guid>("SetId"));await mode.ExecuteNonQueryAsync(ct);}
        Guid round=Guid.NewGuid();
        using(var c=w.Command("INSERT dbo.TimesheetApprovalRounds(RoundId,SetId,SourceVersion,SubmittedByAccountId,SubmittedBy) VALUES(@id,@set,@version,@actor,@name)")){Id(c,"@id",round);Id(c,"@set",h.Field<Guid>("SetId"));Id(c,"@actor",actor);c.Text("@name",name);c.Parameters.Add("@version",SqlDbType.Binary,8).Value=t["RecordVersion"];await c.ExecuteNonQueryAsync(ct);}
        using(var c=w.Command("INSERT dbo.TimesheetSegmentApprovals(ApprovalId,RoundId,SegmentId,ProjectId) SELECT NEWID(),@round,SegmentId,ProjectId FROM dbo.TimesheetSegments WHERE SetId=@set")){Id(c,"@round",round);Id(c,"@set",h.Field<Guid>("SetId"));await c.ExecuteNonQueryAsync(ct);}
        using(var c=w.Command("UPDATE dbo.TimesheetApprovalRounds SET IsReady=1 WHERE RoundId=@id")){Id(c,"@id",round);await c.ExecuteNonQueryAsync(ct);}
    }
    private static async Task SetDerivedParentAsync(IUnitOfWork w,Guid id,string state,Guid actor,string name,CancellationToken ct)
    {
        using var c=w.Command("UPDATE dbo.Timesheets SET Status=@state,DecidedByAccountId=@actor,DecidedBy=@name,DecidedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id");Id(c,"@id",id);Id(c,"@actor",actor);c.Text("@state",state,20);c.Text("@name",name);await c.ExecuteNonQueryAsync(ct);
    }
    internal async Task DecideSegmentAsync(Wire.SegmentDecision input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("allocations/segment-decision",input,ct);return;}
        if(input.State is not ("Approved" or "Returned"))throw new ArgumentException("Parça kararı onay veya iade olmalı.");
        string text=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(text);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.ApproveTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Segment.Decision.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        using var find=w.Command("SELECT S.*,H.TimesheetId,H.IsSealed,H.ReleasedUtc,H.RecordVersion SetVersion,H.CreatedByAccountId SetCreator,H.UsesSegmentApproval FROM dbo.TimesheetSegments S WITH(HOLDLOCK) JOIN dbo.TimesheetAllocationSets H WITH(UPDLOCK,HOLDLOCK) ON H.SetId=S.SetId WHERE S.SegmentId=@id AND S.ProjectId=@project");Id(find,"@id",input.SegmentId);Id(find,"@project",input.ProjectId);var rows=await find.TableAsync(ct);
        if(rows.Rows.Count!=1)throw new UnauthorizedAccessException("Bu projede karar verilecek parça bulunamadı.");var s=rows.Rows[0];if(!s.IsNull("ReleasedUtc")||!s.Field<bool>("IsSealed")||!((byte[])s["SetVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.SetVersion)))throw new InvalidOperationException("Dağılım değişmiş/kaldırılmış; yenileyin.");
        var t=await RowAsync(w,s.Field<Guid>("TimesheetId"),ct)??throw new InvalidOperationException("Kaynak kayıt bulunamadı.");Version(t,input.ParentVersion);if(t.Field<string>("Status") is not ("Submitted" or "Approved"))throw new InvalidOperationException("Kaynak kayıt onay turunda değil; yenileyin.");
        if(input.State=="Returned")await DemandNotAccruedAsync(w,t.Field<Guid>("TimesheetId"),ct);
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);
        if(!s.Field<bool>("UsesSegmentApproval"))
        {
            if(input.RoundId!=null||input.State!="Returned")throw new InvalidOperationException("Eski toplu onay/gönderim önce proje parçasından iade edilmeli; yeniden gönderimde bağımsız tur açılır.");
            using(var c=w.Command("INSERT dbo.TimesheetSegmentApprovals(ApprovalId,SegmentId,ProjectId,LegacyReturn,State,DecidedByAccountId,DecidedBy,DecidedUtc,DecisionReason) VALUES(@id,@segment,@project,1,N'Returned',@actor,@name,SYSUTCDATETIME(),@reason)")){Id(c,"@id",Guid.NewGuid());Id(c,"@segment",input.SegmentId);Id(c,"@project",input.ProjectId);Id(c,"@actor",actor);c.Text("@name",user.Username);c.Text("@reason",text,500);await c.ExecuteNonQueryAsync(ct);}
            await SetDerivedParentAsync(w,t.Field<Guid>("TimesheetId"),"Returned",actor,user.Username,ct);
        }
        else
        {
            using var q=w.Command("SELECT D.*,R.IsReady,R.ClosedUtc,R.SubmittedByAccountId,R.SetId FROM dbo.TimesheetSegmentApprovals D WITH(UPDLOCK,HOLDLOCK) JOIN dbo.TimesheetApprovalRounds R WITH(UPDLOCK,HOLDLOCK) ON R.RoundId=D.RoundId WHERE D.RoundId=@round AND D.SegmentId=@segment AND D.ProjectId=@project AND D.LegacyReturn=0");Id(q,"@round",input.RoundId);Id(q,"@segment",input.SegmentId);Id(q,"@project",input.ProjectId);var ds=await q.TableAsync(ct);
            if(ds.Rows.Count!=1)throw new InvalidOperationException("Karar güncel onay turunda bulunamadı.");var d=ds.Rows[0];Version(d,input.ApprovalVersion);
            if(!d.Field<bool>("IsReady")||!d.IsNull("ClosedUtc")||d.Field<Guid>("SetId")!=s.Field<Guid>("SetId"))throw new InvalidOperationException("Onay turu kapanmış veya değişmiş.");
            string state=d.Field<string>("State")!;if(input.State=="Approved"?(state!="Pending"||t.Field<string>("Status")!="Submitted"):state is not ("Pending" or "Approved"))throw new InvalidOperationException("Parça artık bu karara uygun değil; yenileyin.");
            if(input.State=="Approved")
            {
                if(actor==t.Field<Guid>("CreatedByAccountId")||actor==t.Field<Guid>("EditedByAccountId")||actor==d.Field<Guid>("SubmittedByAccountId")||actor==s.Field<Guid>("SetCreator"))throw new InvalidOperationException("Hazırlayan/düzenleyen/gönderen hesap kendi parçasını onaylayamaz.");
                await DemandIndependentApproverAsync(w,t.Field<Guid>("TimesheetId"),actor,ct);
                var fault=await FaultAsync(w,t,ct);if(fault.Length>0)throw new InvalidOperationException(fault);
                await ValidateTargetAsync(w,t.Field<Guid>("ProjectId"),t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),t.Field<Guid>("RosterAssignmentId"),t.Field<DateTime>("LocalStart"),t.Field<DateTime>("LocalEnd"),ct);
                await ValidateTargetAsync(w,input.ProjectId,t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),s.Field<Guid>("RosterAssignmentId"),s.Field<DateTime>("LocalStart"),s.Field<DateTime>("LocalEnd"),ct);
            }
            using(var c=w.Command("UPDATE dbo.TimesheetSegmentApprovals SET State=@state,DecidedByAccountId=@actor,DecidedBy=@name,DecidedUtc=SYSUTCDATETIME(),DecisionReason=@reason WHERE ApprovalId=@id")){Id(c,"@id",d.Field<Guid>("ApprovalId"));Id(c,"@actor",actor);c.Text("@state",input.State,20);c.Text("@name",user.Username);c.Text("@reason",text,500);await c.ExecuteNonQueryAsync(ct);}
            if(input.State=="Returned")
            {
                using(var c=w.Command("UPDATE dbo.TimesheetApprovalRounds SET ClosedUtc=SYSUTCDATETIME(),ClosedByAccountId=@actor,ClosedBy=@name WHERE RoundId=@id")){Id(c,"@id",input.RoundId);Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);}
                await SetDerivedParentAsync(w,t.Field<Guid>("TimesheetId"),"Returned",actor,user.Username,ct);
            }
            else
            {
                using var count=w.Command("SELECT COUNT(*) FROM dbo.TimesheetSegmentApprovals WHERE RoundId=@id AND State<>N'Approved'");Id(count,"@id",input.RoundId);
                if(Convert.ToInt32(await count.ExecuteScalarAsync(ct))==0)
                {
                    var all=await ActiveSegmentsAsync(w,t.Field<Guid>("TimesheetId"),ct);foreach(DataRow part in all.Rows)await ValidateTargetAsync(w,part.Field<Guid>("ProjectId"),t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),part.Field<Guid>("RosterAssignmentId"),part.Field<DateTime>("LocalStart"),part.Field<DateTime>("LocalEnd"),ct);
                    await SetDerivedParentAsync(w,t.Field<Guid>("TimesheetId"),"Approved",actor,user.Username,ct);
                }
            }
        }
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
}
