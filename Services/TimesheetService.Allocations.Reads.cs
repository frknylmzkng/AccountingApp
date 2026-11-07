using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task<DataTable> AllocationTargetsAsync(Guid project,Guid timesheet,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"allocations/{project:D}/{timesheet:D}/targets",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.EnterTimesheets,ct);
        var t=await RowAsync(w,timesheet,ct)??throw new InvalidOperationException("Puantaj bulunamadı.");if(t.Field<Guid>("ProjectId")!=project)throw new UnauthorizedAccessException("Puantaj bu kaynak projede değil.");
        using var c=w.Command(@"SELECT TOP(501) P.ProjectId,P.Code ProjectCode,P.Name ProjectName,P.RecordVersion ProjectVersion,A.AssignmentId RosterAssignmentId,A.RecordVersion RosterVersion,A.IsSupplementary,A.StartDate,A.EndDate,A.TeamId,E.Name TeamName
 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 JOIN dbo.FinanceProjects P WITH(HOLDLOCK) ON P.ProjectId=A.ProjectId LEFT JOIN dbo.ProjectTeams E ON E.TeamId=A.TeamId
 LEFT JOIN dbo.ProjectAccountAccess G WITH(HOLDLOCK) ON G.ProjectId=P.ProjectId AND G.AccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND G.IsActive=1
 WHERE A.PersonId=@person AND A.PersonnelGroup=@group AND L.EmploymentId=@employment AND A.CancelledUtc IS NULL AND P.IsActive=1 AND (A.TeamId IS NULL OR E.IsActive=1)
 AND (@admin=1 OR (@scoped=1 AND G.CanEnterTimesheets=1)) ORDER BY P.Code,A.StartDate,A.AssignmentId");
        Id(c,"@person",t.Field<Guid>("PersonId"));Id(c,"@employment",t.Field<Guid>("EmploymentId"));c.Parameters.Add("@group",SqlDbType.Int).Value=t.Field<int>("PersonnelGroup");c.Parameters.Add("@admin",SqlDbType.Bit).Value=user.Role==UserRole.Admin;ProjectAuthorization.Bind(c,user);
        var rows=await c.TableAsync(ct);if(rows.Rows.Count>500)throw new InvalidOperationException("Hedef atama sınırı 500; kapsamı daraltın.");await w.CommitAsync(ct);return rows;
    }
    internal async Task<(DataTable Sets,DataTable Segments)> AllocationsAsync(Guid project,Guid timesheet,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){var d=await RemoteApi.For(connectionString,user).SendAsync<Wire.WorkAllocationData>($"allocations/{project:D}/{timesheet:D}",null,ct);return(RemoteApi.Decode(d.Sets),RemoteApi.Decode(d.Segments));}
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        var t=await RowAsync(w,timesheet,ct)??throw new InvalidOperationException("Puantaj yok.");if(t.Field<Guid>("ProjectId")!=project)throw new UnauthorizedAccessException("Puantaj bu kaynak projede değil.");
        using(var ids=w.Command("SELECT DISTINCT S.ProjectId FROM dbo.TimesheetAllocationSets H JOIN dbo.TimesheetSegments S ON S.SetId=H.SetId WHERE H.TimesheetId=@id ORDER BY S.ProjectId")){Id(ids,"@id",timesheet);var projects=await ids.TableAsync(ct);foreach(DataRow p in projects.Rows)await ProjectAuthorization.DemandAsync(w,user,p.Field<Guid>("ProjectId"),ProjectAccess.Read,ct);}
        using var q=w.Command("SELECT TOP(1001) H.*,T.RecordVersion TimesheetVersion,T.Status TimesheetStatus FROM dbo.TimesheetAllocationSets H JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE H.TimesheetId=@id ORDER BY H.CreatedUtc DESC,H.SetId");Id(q,"@id",timesheet);var sets=await q.TableAsync(ct);
        using var c=w.Command(@"SELECT TOP(50001) S.*,P.Code ProjectCode,P.Name ProjectName,E.Name TeamName,A.IsSupplementary FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.FinanceProjects P ON P.ProjectId=S.ProjectId JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=S.RosterAssignmentId LEFT JOIN dbo.ProjectTeams E ON E.TeamId=A.TeamId WHERE H.TimesheetId=@id ORDER BY H.CreatedUtc DESC,H.SetId,S.SequenceNo");Id(c,"@id",timesheet);var segments=await c.TableAsync(ct);
        if(sets.Rows.Count>1000||segments.Rows.Count>50000)throw new InvalidOperationException("Dağılım geçmişi sınırı aşıldı.");await w.CommitAsync(ct);return(sets,segments);
    }
    private static async Task<DataTable> ProjectSegmentsAsync(IUnitOfWork w,Guid project,DateTime first,CancellationToken ct,bool history)
    {
        using var c=w.Command(@"SELECT TOP(2001) S.*,T.TimesheetId,T.WorkDate,T.Status,T.RecordVersion ParentVersion,H.RecordVersion SetVersion,H.UsesSegmentApproval,H.ReleasedUtc AllocationReleasedUtc,R.RoundId,R.ClosedUtc RoundClosedUtc,D.RecordVersion ApprovalVersion,D.State DecisionState,D.DecidedBy,D.DecidedUtc,D.DecisionReason,
 COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,E.Name TeamName,
 CASE WHEN H.ReleasedUtc IS NOT NULL THEN N'Dağılım kaldırıldı' WHEN H.UsesSegmentApproval=0 THEN CASE WHEN T.Status=N'Approved' THEN N'Eski toplu onay' WHEN T.Status=N'Submitted' THEN N'Eski toplu gönderim; önce iade' ELSE N'Taslak / iade' END WHEN R.RoundId IS NULL THEN N'Tur bekleniyor' WHEN R.ClosedUtc IS NOT NULL THEN N'Tur kapandı; karar tarihsel' WHEN D.State=N'Approved' THEN N'Parça onaylı' WHEN D.State=N'Pending' THEN N'Proje onayı bekliyor' ELSE N'İade' END EffectiveState,
 dbo.TimesheetIdentityConflict(T.TimesheetId,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))) OwnPreparation
 FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.IsSealed=1 JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId
 OUTER APPLY(SELECT TOP(1) RR.* FROM dbo.TimesheetApprovalRounds RR WHERE RR.SetId=H.SetId AND RR.IsReady=1 ORDER BY CASE WHEN RR.ClosedUtc IS NULL THEN 0 ELSE 1 END,RR.SubmittedUtc DESC,RR.RoundId) R
 LEFT JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId AND D.SegmentId=S.SegmentId AND D.LegacyReturn=0
 JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=S.RosterAssignmentId LEFT JOIN dbo.ProjectTeams E ON E.TeamId=A.TeamId
 LEFT JOIN dbo.Calisan C ON C.PersonId=T.PersonId AND T.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=T.PersonId AND T.PersonnelGroup=1
 WHERE S.ProjectId=@project AND (@history=1 OR H.ReleasedUtc IS NULL) AND T.WorkDate>=@first AND T.WorkDate<@last ORDER BY T.WorkDate,S.StartUtc,S.SegmentId");Id(c,"@project",project);c.Parameters.Add("@history",SqlDbType.Bit).Value=history;c.Parameters.Add("@first",SqlDbType.Date).Value=first;c.Parameters.Add("@last",SqlDbType.Date).Value=first.AddDays(7);var rows=await c.TableAsync(ct);if(rows.Rows.Count>2000)throw new InvalidOperationException("Projeye ait parça sayısı 2000 sınırını aşıyor.");return rows;
    }
    internal async Task<DataTable> SegmentHistoryAsync(Guid project,Guid segment,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"allocations/{project:D}/segments/{segment:D}/decisions",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        using(var scope=w.Command("SELECT COUNT(*) FROM dbo.TimesheetSegments WHERE SegmentId=@id AND ProjectId=@project")){Id(scope,"@id",segment);Id(scope,"@project",project);if(Convert.ToInt32(await scope.ExecuteScalarAsync(ct))!=1)throw new UnauthorizedAccessException("Parça bu proje kapsamında değil.");}
        using var c=w.Command(@"SELECT TOP(1001) D.*,R.SubmittedUtc,R.IsReady,R.ClosedUtc RoundClosedUtc FROM dbo.TimesheetSegmentDecisionEvents D LEFT JOIN dbo.TimesheetApprovalRounds R ON R.RoundId=D.RoundId WHERE D.SegmentId=@id AND D.ProjectId=@project ORDER BY D.DecidedUtc DESC,D.EventId");Id(c,"@id",segment);Id(c,"@project",project);var rows=await c.TableAsync(ct);if(rows.Rows.Count>1000)throw new InvalidOperationException("Karar geçmişi 1000 satır sınırını aşıyor.");await w.CommitAsync(ct);return rows;
    }
}
