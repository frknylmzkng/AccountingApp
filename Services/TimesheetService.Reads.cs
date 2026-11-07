using System.Data;
using Microsoft.Data.SqlClient;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task<(DataTable Rows,DataTable Assignments,DataTable Summary,DataTable Segments)> WeekAsync(Guid project,DateTime first,CancellationToken ct,bool segmentHistory=false)
    {
        if(DesktopConnection.IsRemote(connectionString)){var data=await RemoteApi.For(connectionString,user).SendAsync<AccountingApp.Contracts.TimesheetWeek>($"timesheets/{project:D}/week?first={RemoteApi.Day(first)}&segmentHistory={segmentHistory}",null,ct);return(RemoteApi.Decode(data.Rows),RemoteApi.Decode(data.Assignments),RemoteApi.Decode(data.Summary),RemoteApi.Decode(data.Segments));}
        EmploymentService.Day(first);if(first.Year<1900||first.Year>2199)throw new ArgumentException("Hafta tarihini kontrol edin.");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        void Bind(SqlCommand c){Id(c,"@project",project);c.Parameters.Add("@first",SqlDbType.Date).Value=first;c.Parameters.Add("@last",SqlDbType.Date).Value=first.AddDays(7);}
        const string names=" LEFT JOIN dbo.Calisan C ON C.PersonId=A.PersonId AND A.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=A.PersonId AND A.PersonnelGroup=1 ";
        using var q=w.Command(@"SELECT TOP(2001) T.*,dbo.TimesheetIdentityConflict(T.TimesheetId,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))) OwnPreparation,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL) THEN 1 ELSE 0 END AS bit) HasProjectAllocation,(SELECT H.SetId FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL) ActiveAllocationId,(SELECT M.MatchId FROM dbo.AttendanceMatches M WHERE M.TimesheetId=T.TimesheetId AND M.ReleasedUtc IS NULL) ActiveMatchId,CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.AttendanceMatches M WHERE M.TimesheetId=T.TimesheetId AND M.ReleasedUtc IS NULL) THEN 1 ELSE 0 END AS bit) HasAttendanceMatch,DATEDIFF(minute,T.StartUtc,T.EndUtc) GrossMinutes,DATEDIFF(minute,S.LocalStart,S.LocalEnd)-S.BreakMinutes PlannedMinutes,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,CASE WHEN T.ShiftAssignmentId IS NULL THEN N'Vardiyasız çalışma' ELSE S.Name END ShiftName,CASE WHEN T.ShiftAssignmentId IS NULL THEN N'Unplanned' ELSE N'Planned' END WorkKind,R.RecordVersion RosterVersion,COALESCE(C.RecordVersion,J.RecordVersion) PersonVersion,S.LocalStart PlannedStart,S.LocalEnd PlannedEnd,S.RecordVersion ShiftVersion,A.RecordVersion AssignmentVersion,
 CASE WHEN T.Status=N'Cancelled' THEN N'İptal'
 WHEN T.StartUtc IS NULL OR T.EndUtc IS NULL THEN N'Eksik giriş / çıkış'
 WHEN T.EndUtc<=T.StartUtc OR DATEDIFF(minute,T.StartUtc,T.EndUtc)>1440 OR T.ActualMinutes<=0 THEN N'Süre / mola hatası'
 WHEN T.PayableMinutes IS NULL OR T.PayableMinutes>T.ActualMinutes THEN N'Ücretlendirilecek süre eksik / fazla'
 WHEN T.ShiftAssignmentId IS NOT NULL AND (S.Status<>N'Published' OR A.CancelledUtc IS NOT NULL) THEN N'Vardiya / atama geçersiz'
 WHEN T.ShiftAssignmentId IS NULL AND EXISTS(SELECT 1 FROM dbo.ShiftAssignments X JOIN dbo.PlannedShifts P ON P.ShiftId=X.ShiftId WHERE X.PersonId=T.PersonId AND X.CancelledUtc IS NULL AND P.Status<>N'Cancelled' AND P.StartUtc<T.EndUtc AND P.EndUtc>T.StartUtc) THEN N'Planlı vardiya var; vardiyalı puantaj kullanın'
 WHEN NOT EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments R JOIN dbo.EmploymentRecordLinks E ON E.RecordKind=N'Assignment' AND E.RecordKey=CONVERT(nvarchar(64),R.AssignmentId) WHERE R.AssignmentId=T.RosterAssignmentId AND R.CancelledUtc IS NULL AND E.EmploymentId=T.EmploymentId AND R.StartDate<=CONVERT(date,T.LocalStart) AND (R.EndDate IS NULL OR R.EndDate>=CONVERT(date,DATEADD(minute,-1,T.LocalEnd)))) THEN N'Proje ataması değişmiş'
 WHEN NOT EXISTS(SELECT 1 FROM dbo.EmploymentPeriods E WHERE E.EmploymentId=T.EmploymentId AND E.IsClosed=0 AND (E.StartDate IS NULL OR E.StartDate<=CONVERT(date,T.LocalStart)) AND (E.EndDate IS NULL OR E.EndDate>=CONVERT(date,DATEADD(minute,-1,T.LocalEnd)))) OR COALESCE(C.CurrentEmploymentId,J.CurrentEmploymentId)<>T.EmploymentId OR COALESCE(C.ArchivedUtc,J.ArchivedUtc) IS NOT NULL THEN N'Personel dönemi değişmiş / arşiv'
 WHEN NOT EXISTS(SELECT 1 FROM dbo.FinanceProjects P WHERE P.ProjectId=T.ProjectId AND P.IsActive=1 AND (P.StartDate IS NULL OR P.StartDate<=CONVERT(date,T.LocalStart)) AND (P.EndDate IS NULL OR P.EndDate>=CONVERT(date,DATEADD(minute,-1,T.LocalEnd)))) THEN N'Proje tarihi / durumu uygun değil'
 WHEN EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments R JOIN dbo.ProjectTeams E ON E.TeamId=R.TeamId WHERE R.AssignmentId=T.RosterAssignmentId AND E.IsActive=0) THEN N'Ekip pasif'
 WHEN EXISTS(SELECT 1 FROM dbo.Timesheets X WHERE X.PersonId=T.PersonId AND X.TimesheetId<>T.TimesheetId AND X.Status IN(N'Submitted',N'Approved') AND X.StartUtc<T.EndUtc AND X.EndUtc>T.StartUtc) THEN N'Çalışma çakışması'
 WHEN EXISTS(SELECT 1 FROM dbo.LeaveRequests L WHERE L.PersonId=T.PersonId AND L.Status=N'Approved' AND L.StartUtc<T.EndUtc AND L.EndUtc>T.StartUtc) OR EXISTS(SELECT 1 FROM dbo.izinler I WHERE I.PersonId=T.PersonId AND I.IZINBASLANGIC<=CONVERT(date,DATEADD(minute,-1,T.LocalEnd)) AND I.IZINBITIS>=CONVERT(date,T.LocalStart)) THEN N'İzin çakışması'
 ELSE N'' END Exception
 FROM dbo.Timesheets T LEFT JOIN dbo.ShiftAssignments A ON A.ShiftAssignmentId=T.ShiftAssignmentId LEFT JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId LEFT JOIN dbo.PersonnelProjectAssignments R ON R.AssignmentId=T.RosterAssignmentId"+names.Replace("A.PersonId", "T.PersonId").Replace("A.PersonnelGroup", "T.PersonnelGroup")+@" WHERE T.ProjectId=@project AND T.WorkDate>=@first AND T.WorkDate<@last ORDER BY T.WorkDate,AD,SOYAD,T.TimesheetId");Bind(q);var rows=await q.TableAsync(ct);
        using var a=w.Command(@"SELECT TOP(2001) A.ShiftAssignmentId,A.PersonId,A.PersonnelGroup,A.RecordVersion AssignmentVersion,S.RecordVersion ShiftVersion,S.Name ShiftName,S.LocalStart PlannedStart,S.LocalEnd PlannedEnd,S.BreakMinutes PlannedBreak,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,T.TimesheetId
 FROM dbo.ShiftAssignments A JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId"+names+@" LEFT JOIN dbo.Timesheets T ON T.ShiftAssignmentId=A.ShiftAssignmentId
 WHERE S.ProjectId=@project AND S.Status=N'Published' AND A.CancelledUtc IS NULL AND S.LocalStart>=@first AND S.LocalStart<@last ORDER BY S.LocalStart,AD,SOYAD,A.ShiftAssignmentId");Bind(a);var assignments=await a.TableAsync(ct);
        if(rows.Rows.Count>2000||assignments.Rows.Count>2000)throw new InvalidOperationException("Haftada 2000 kayıt sınırı aşıldı; bu kapsam için daha dar filtreleme gerekir.");
        var pieces=await ProjectSegmentsAsync(w,project,first,ct,segmentHistory);
        using var summary=w.Command(@"WITH ProjectHours AS(
 SELECT T.ActualMinutes Net,T.PayableMinutes Paid FROM dbo.Timesheets T WHERE T.ProjectId=@project AND T.WorkDate>=@first AND T.WorkDate<@last AND T.Status=N'Approved' AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL)
 UNION ALL SELECT S.NetMinutes,S.PayableMinutes FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.IsSealed=1 AND H.ReleasedUtc IS NULL JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE S.ProjectId=@project AND T.WorkDate>=@first AND T.WorkDate<@last AND ((H.UsesSegmentApproval=0 AND T.Status=N'Approved') OR (H.UsesSegmentApproval=1 AND T.Status IN(N'Submitted',N'Approved') AND EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId WHERE R.SetId=H.SetId AND R.IsReady=1 AND R.ClosedUtc IS NULL AND D.SegmentId=S.SegmentId AND D.State=N'Approved'))))
 SELECT (SELECT COUNT_BIG(*) FROM dbo.Timesheets WHERE ProjectId=@project AND WorkDate>=@first AND WorkDate<@last) RecordCount,
 (SELECT COUNT_BIG(*) FROM dbo.Timesheets T WHERE T.WorkDate>=@first AND T.WorkDate<@last AND T.Status=N'Submitted' AND (T.ProjectId=@project OR EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H JOIN dbo.TimesheetSegments S ON S.SetId=H.SetId WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL AND H.IsSealed=1 AND S.ProjectId=@project AND (H.UsesSegmentApproval=0 OR EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId WHERE R.SetId=H.SetId AND R.IsReady=1 AND R.ClosedUtc IS NULL AND D.SegmentId=S.SegmentId AND D.State=N'Pending'))))) AwaitingApproval,
 COALESCE(SUM(CONVERT(bigint,Net)),0) ApprovedActualMinutes,COALESCE(SUM(CONVERT(bigint,Paid)),0) ApprovedPayableMinutes FROM ProjectHours");Bind(summary);var sums=await summary.TableAsync(ct);await w.CommitAsync(ct);return(rows,assignments,sums,pieces);
    }
    internal async Task<string> CsvTemplateAsync(Guid project,DateTime first,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return(await RemoteApi.For(connectionString,user).SendAsync<AccountingApp.Contracts.ClipboardText>($"timesheets/{project:D}/csv-template?first={RemoteApi.Day(first)}",null,ct)).Text;
        var week=await WeekAsync(project,first,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Export,ct);await w.CommitAsync(ct);
        var result=new System.Text.StringBuilder("SourceKey;ShiftAssignmentId;Start;End;BreakMinutes;PayableMinutes\r\n");
        foreach(DataRow a in week.Assignments.Rows)if(a.IsNull("TimesheetId")){var id=a.Field<Guid>("ShiftAssignmentId");result.Append($"csv:{id:N};{id:D};;;0;\r\n");}return result.ToString();
    }

}
