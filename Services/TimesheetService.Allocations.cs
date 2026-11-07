using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal static async Task AllocationGateAsync(IUnitOfWork w,CancellationToken ct)
    {using var c=w.Command("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'Timesheet.Allocations.v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;IF @r<0 THROW 51169,N'Dağılım kilidi alınamadı.',1;");await c.ExecuteNonQueryAsync(ct);}
    private static async Task EnsureNotAllocatedAsync(IUnitOfWork w,Guid id,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.TimesheetAllocationSets WITH(UPDLOCK,HOLDLOCK) WHERE TimesheetId=@id AND ReleasedUtc IS NULL");Id(c,"@id",id);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Puantaj projelere dağıtılmış. Saat/mola/olay veya iptal değişikliğinden önce dağılımı kaldırın.");}
    private static async Task TouchAllocationParentAsync(IUnitOfWork w,Guid id,Guid actor,string name,CancellationToken ct)
    {using var c=w.Command("UPDATE dbo.Timesheets SET EditedByAccountId=@actor,EditedBy=@name,EditedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id");Id(c,"@id",id);Id(c,"@actor",actor);c.Text("@name",name);await c.ExecuteNonQueryAsync(ct);}
    private static async Task<DataTable> ActiveSegmentsAsync(IUnitOfWork w,Guid id,CancellationToken ct)
    {using var c=w.Command("SELECT S.*,H.SetId ActiveSetId,H.IsSealed FROM dbo.TimesheetAllocationSets H WITH(HOLDLOCK) JOIN dbo.TimesheetSegments S WITH(HOLDLOCK) ON S.SetId=H.SetId WHERE H.TimesheetId=@id AND H.ReleasedUtc IS NULL ORDER BY S.SequenceNo");Id(c,"@id",id);return await c.TableAsync(ct);}
    private async Task DemandAllocationStateAsync(IUnitOfWork w,DataRow t,string state,CancellationToken ct)
    {
        var segments=await ActiveSegmentsAsync(w,t.Field<Guid>("TimesheetId"),ct);
        using(var q=w.Command("SELECT COUNT(*) FROM dbo.TimesheetAllocationSets WHERE TimesheetId=@id AND ReleasedUtc IS NULL AND IsSealed=0")){Id(q,"@id",t.Field<Guid>("TimesheetId"));if(Convert.ToInt32(await q.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Tamamlanmamış dağılımı önce kaldırın.");}
        foreach(var project in segments.AsEnumerable().Select(r=>r.Field<Guid>("ProjectId")).Distinct().Order())await ProjectAuthorization.DemandAsync(w,user,project,state is "Approved" or "Returned"?ProjectAccess.ApproveTimesheets:ProjectAccess.EnterTimesheets,ct);
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);
        if(state is "Submitted" or "Approved")foreach(DataRow s in segments.Rows)
            await ValidateTargetAsync(w,s.Field<Guid>("ProjectId"),t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),s.Field<Guid>("RosterAssignmentId"),s.Field<DateTime>("LocalStart"),s.Field<DateTime>("LocalEnd"),ct);
    }
    internal async Task SaveAllocationAsync(Wire.WorkAllocation input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("allocations/save",input,ct);return;}
        if(input.SetId==Guid.Empty||input.Segments==null||input.Segments.Length is <1 or >50||input.Segments.Any(s=>s==null||s.SegmentId==Guid.Empty)||input.Segments.Select(s=>s.SegmentId).Distinct().Count()!=input.Segments.Length)throw new ArgumentException("1–50 farklı zaman parçası gerekiyor.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Allocation.Save.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        foreach(var project in input.Segments.Select(s=>s.ProjectId).Distinct().Order())await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.EnterTimesheets,ct);
        var t=await RowAsync(w,input.TimesheetId,ct)??throw new InvalidOperationException("Puantaj bulunamadı.");if(t.Field<Guid>("ProjectId")!=input.ProjectId)throw new UnauthorizedAccessException("Puantaj bu kaynak projede değil.");Version(t,input.Version);
        if(t.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Yalnız taslak/iade puantaj dağıtılır.");
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);await EnsureNotAllocatedAsync(w,input.TimesheetId,ct);
        var fault=await FaultAsync(w,t,ct);if(fault.Length>0)throw new InvalidOperationException(fault);
        await SnapshotTargetAsync(w,input.ProjectId,t.Field<Guid>("PersonId"),t.Field<int>("PersonnelGroup"),t.Field<Guid>("EmploymentId"),t.Field<Guid>("RosterAssignmentId"),input.PersonVersion,input.RosterVersion,ct);
        await ValidateTargetAsync(w,input.ProjectId,t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),t.Field<Guid>("RosterAssignmentId"),t.Field<DateTime>("LocalStart"),t.Field<DateTime>("LocalEnd"),ct);
        var ordered=input.Segments.OrderBy(s=>s.Start).ToArray();DateTime next=t.Field<DateTime>("StartUtc");int pause=0,paid=0;
        foreach(var s in ordered)
        {
            var utc=SchedulingRules.UtcRange(s.Start,s.End);int gross=checked((int)(utc.End-utc.Start).TotalMinutes);
            if(utc.Start!=next||utc.End>t.Field<DateTime>("EndUtc")||s.Start<t.Field<DateTime>("LocalStart")||s.End>t.Field<DateTime>("LocalEnd")||s.BreakMinutes<0||s.BreakMinutes>gross||s.PayableMinutes<0||s.PayableMinutes>gross-s.BreakMinutes)throw new InvalidOperationException("Parçalar kaynak aralığını sırayla boşluksuz/çakışmasız kapsamalı; mola ve ücret dakikası parçanın süresini aşamaz.");
            next=utc.End;pause+=s.BreakMinutes;paid+=s.PayableMinutes;
            using(var p=w.Command("SELECT COUNT(*) FROM dbo.FinanceProjects WITH(HOLDLOCK) WHERE ProjectId=@id AND RecordVersion=@version")){Id(p,"@id",s.ProjectId);p.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(s.ProjectVersion);if(Convert.ToInt32(await p.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Hedef proje değişmiş; hedefleri yenileyin.");}
            using(var r=w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE AssignmentId=@id AND ProjectId=@project AND RecordVersion=@version AND CancelledUtc IS NULL")){Id(r,"@id",s.RosterAssignmentId);Id(r,"@project",s.ProjectId);r.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(s.RosterVersion);if(Convert.ToInt32(await r.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Hedef proje/ekip ataması değişmiş; yeniden seçin.");}
            await ValidateTargetAsync(w,s.ProjectId,t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),s.RosterAssignmentId,s.Start,s.End,ct);
        }
        if(next!=t.Field<DateTime>("EndUtc")||pause!=t.Field<int>("BreakMinutes")||paid!=t.Field<int>("PayableMinutes"))throw new InvalidOperationException("Parçalar bitişi kapsamalı; mola ve ücret dakikası toplamları ana puantajla tam eşleşmeli.");
        using(var c=w.Command(@"INSERT dbo.TimesheetAllocationSets(SetId,TimesheetId,SourceProjectId,SourceVersion,LocalStart,LocalEnd,StartUtc,EndUtc,BreakMinutes,PayableMinutes,TimeZoneId,CreatedByAccountId,CreatedBy,UsesSegmentApproval)
 VALUES(@id,@timesheet,@project,@version,@localstart,@localend,@start,@end,@break,@paid,@zone,@actor,@name,1)"))
        {
            Id(c,"@id",input.SetId);Id(c,"@timesheet",input.TimesheetId);Id(c,"@project",input.ProjectId);Id(c,"@actor",actor);c.Parameters.Add("@version",SqlDbType.Binary,8).Value=input.Version;c.Text("@name",user.Username);c.Text("@zone",t.Field<string>("TimeZoneId")!,100);
            Time(c,"@localstart",t.Field<DateTime>("LocalStart"));Time(c,"@localend",t.Field<DateTime>("LocalEnd"));Time(c,"@start",t.Field<DateTime>("StartUtc"));Time(c,"@end",t.Field<DateTime>("EndUtc"));c.Parameters.Add("@break",SqlDbType.Int).Value=pause;c.Parameters.Add("@paid",SqlDbType.Int).Value=paid;await c.ExecuteNonQueryAsync(ct);
        }
        for(int i=0;i<ordered.Length;i++)
        {
            var s=ordered[i];var utc=SchedulingRules.UtcRange(s.Start,s.End);
            using var c=w.Command(@"INSERT dbo.TimesheetSegments(SegmentId,SetId,SequenceNo,ProjectId,RosterAssignmentId,ProjectVersion,RosterVersion,LocalStart,LocalEnd,StartUtc,EndUtc,BreakMinutes,PayableMinutes)
 VALUES(@id,@set,@seq,@project,@roster,@pv,@rv,@localstart,@localend,@start,@end,@break,@paid)");
            Id(c,"@id",s.SegmentId);Id(c,"@set",input.SetId);Id(c,"@project",s.ProjectId);Id(c,"@roster",s.RosterAssignmentId);c.Parameters.Add("@seq",SqlDbType.Int).Value=i+1;c.Parameters.Add("@pv",SqlDbType.Binary,8).Value=s.ProjectVersion;c.Parameters.Add("@rv",SqlDbType.Binary,8).Value=s.RosterVersion;
            Time(c,"@localstart",s.Start);Time(c,"@localend",s.End);Time(c,"@start",utc.Start);Time(c,"@end",utc.End);c.Parameters.Add("@break",SqlDbType.Int).Value=s.BreakMinutes;c.Parameters.Add("@paid",SqlDbType.Int).Value=s.PayableMinutes;await c.ExecuteNonQueryAsync(ct);
        }
        using(var seal=w.Command("UPDATE dbo.TimesheetAllocationSets SET IsSealed=1 WHERE SetId=@id")){Id(seal,"@id",input.SetId);await seal.ExecuteNonQueryAsync(ct);}
        await TouchAllocationParentAsync(w,input.TimesheetId,actor,user.Username,ct);await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    internal async Task ReleaseAllocationAsync(Wire.WorkAllocationRelease input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("allocations/release",input,ct);return;}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Allocation.Release.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        using var q=w.Command("SELECT * FROM dbo.TimesheetAllocationSets WITH(UPDLOCK,HOLDLOCK) WHERE SetId=@id AND SourceProjectId=@project");Id(q,"@id",input.SetId);Id(q,"@project",input.ProjectId);var rows=await q.TableAsync(ct);if(rows.Rows.Count!=1)throw new InvalidOperationException("Dağılım bulunamadı.");var h=rows.Rows[0];Version(h,input.Version);if(!h.IsNull("ReleasedUtc"))throw new InvalidOperationException("Dağılım kaldırılmış.");
        var t=await RowAsync(w,h.Field<Guid>("TimesheetId"),ct)??throw new InvalidOperationException("Puantaj yok.");Version(t,input.TimesheetVersion);if(t.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Önce puantajı tüm projelerde yetkili hesapla iade edin/yeniden açın.");
        using(var c=w.Command("SELECT DISTINCT ProjectId FROM dbo.TimesheetSegments WHERE SetId=@id ORDER BY ProjectId")){Id(c,"@id",input.SetId);var projects=await c.TableAsync(ct);foreach(DataRow p in projects.Rows)await ProjectAuthorization.DemandAsync(w,user,p.Field<Guid>("ProjectId"),ProjectAccess.EnterTimesheets,ct);}
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);
        using(var c=w.Command("UPDATE dbo.TimesheetAllocationSets SET ReleasedUtc=SYSUTCDATETIME(),ReleasedByAccountId=@actor,ReleasedBy=@name WHERE SetId=@id")){Id(c,"@id",input.SetId);Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);}
        await TouchAllocationParentAsync(w,t.Field<Guid>("TimesheetId"),actor,user.Username,ct);await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
}
