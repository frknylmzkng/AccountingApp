using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task<(DataTable Events,DataTable Matches)> EventsAsync(Guid project,DateTime first,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){var d=await RemoteApi.For(connectionString,user).SendAsync<Wire.AttendanceWindow>($"attendance/{project:D}/window?first={RemoteApi.Day(first)}",null,ct);return(RemoteApi.Decode(d.Events),RemoteApi.Decode(d.Matches));}
        EmploymentService.Day(first);if(first.Year is <1900 or >2199)throw new ArgumentException("Olay aralığı tarihi geçersiz.");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        void Bind(SqlCommand c){Id(c,"@project",project);Time(c,"@first",first);Time(c,"@last",first.AddDays(8));Time(c,"@weekend",first.AddDays(7));}
        using var events=w.Command(@"SELECT TOP(2001) E.*,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,M.MatchId ActiveMatchId,M.TimesheetId ActiveTimesheetId,
 CASE WHEN E.IgnoredUtc IS NOT NULL THEN N'Yoksayıldı' WHEN M.MatchId IS NOT NULL THEN N'Bağlı' WHEN E.Direction=N'In' THEN N'Çıkış / eşleme bekliyor' ELSE N'Giriş / eşleme bekliyor' END EventState
 FROM dbo.AttendanceEvents E LEFT JOIN dbo.AttendanceMatches M ON M.ReleasedUtc IS NULL AND (M.InEventId=E.EventId OR M.OutEventId=E.EventId)
 LEFT JOIN dbo.Calisan C ON C.PersonId=E.PersonId AND E.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=E.PersonId AND E.PersonnelGroup=1
 WHERE E.ProjectId=@project AND E.OccurredLocal>=@first AND E.OccurredLocal<@last ORDER BY E.OccurredLocal,E.EventId");Bind(events);var es=await events.TableAsync(ct);
        using var matches=w.Command(@"SELECT TOP(2001) M.*,(SELECT H.SetId FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL) ActiveAllocationId,T.PersonId,T.PersonnelGroup,T.EmploymentId,T.RosterAssignmentId,T.WorkDate,T.Status TimesheetStatus,T.RecordVersion TimesheetVersion,
 COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,A.OccurredLocal RawIn,B.OccurredLocal RawOut
 FROM dbo.AttendanceMatches M JOIN dbo.Timesheets T ON T.TimesheetId=M.TimesheetId JOIN dbo.AttendanceEvents A ON A.EventId=M.InEventId JOIN dbo.AttendanceEvents B ON B.EventId=M.OutEventId
 LEFT JOIN dbo.Calisan C ON C.PersonId=T.PersonId AND T.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=T.PersonId AND T.PersonnelGroup=1
 WHERE M.ProjectId=@project AND T.WorkDate>=@first AND T.WorkDate<@weekend ORDER BY M.CreatedUtc DESC,M.MatchId");Bind(matches);var ms=await matches.TableAsync(ct);
        if(es.Rows.Count>2000||ms.Rows.Count>2000)throw new InvalidOperationException("Haftalık 2000 olay/eşleme sınırı aşıldı; daha dar kapsam için filtreleme geliştirmesi gerekir.");
        ms.Columns.Add("MatchedStart",typeof(DateTime));ms.Columns.Add("MatchedEnd",typeof(DateTime));
        foreach(DataRow m in ms.Rows){m["MatchedStart"]=EventMinute(m.Field<DateTime>("RawIn"));m["MatchedEnd"]=EventMinute(m.Field<DateTime>("RawOut"));}ms.AcceptChanges();
        await w.CommitAsync(ct);return(es,ms);
    }
}
