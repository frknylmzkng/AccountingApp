using System.Data;
using Microsoft.Data.SqlClient;
namespace WinFormsApp2;

internal static class SchedulingRules
{
    internal static void Id(SqlCommand c, string name, Guid value) => c.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value;
    internal static void Time(SqlCommand c, string name, DateTime value) => c.Parameters.Add(name, SqlDbType.DateTime2).Value = value;
    internal static (DateTime Start, DateTime End) UtcRange(DateTime start, DateTime end)
    {
        if (start.Kind != DateTimeKind.Unspecified || end.Kind != DateTimeKind.Unspecified || start.Ticks % TimeSpan.TicksPerMinute != 0 || end.Ticks % TimeSpan.TicksPerMinute != 0 || start >= end || start.Year < 1900 || end.Year > 2200) throw new ArgumentException("Takvim tarihlerini saat dilimsiz, tam dakika ve bitiş başlangıçtan sonra olacak şekilde girin (1900–2200).");
        var zone = DailyClosingService.BusinessZone;
        if (zone.IsInvalidTime(start) || zone.IsInvalidTime(end) || zone.IsAmbiguousTime(start) || zone.IsAmbiguousTime(end)) throw new ArgumentException("Saat dilimindeki belirsiz/geçersiz saat kabul edilmez.");
        return (TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone));
    }
    internal static async Task LockPersonAsync(IUnitOfWork w, Guid person, CancellationToken ct)
    { using var c = w.Command("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @r<0 THROW 51124,N'Takvim kişi kilidi alınamadı.',1;"); c.Text("@key", "Assignment.Person." + person.ToString("N"), 100); await c.ExecuteNonQueryAsync(ct); }
    internal static async Task LegacyLockAsync(IUnitOfWork w, long tc, CancellationToken ct)
    { using var c = w.Command("SELECT PersonId FROM dbo.PersonnelIdentities WHERE TC=@tc"); c.Tc(tc); if (await c.ExecuteScalarAsync(ct) is Guid person) await LockPersonAsync(w, person, ct); }
    internal static async Task ClearAsync(IUnitOfWork w, Guid person, DateTime start, DateTime end, Guid? exceptShift, Guid? exceptLeave, bool legacy, CancellationToken ct)
    {
        var utc = UtcRange(start, end);
        using var c = w.Command(@"SELECT
 (SELECT COUNT(*) FROM dbo.Timesheets T WITH(HOLDLOCK) LEFT JOIN dbo.ShiftAssignments TA ON TA.ShiftAssignmentId=T.ShiftAssignmentId WHERE T.PersonId=@person AND T.Status IN(N'Submitted',N'Approved') AND (@shift IS NULL OR T.ShiftAssignmentId IS NULL OR TA.ShiftId<>@shift) AND T.StartUtc<@end AND T.EndUtc>@start)
 +(SELECT COUNT(*) FROM dbo.ShiftAssignments A WITH(HOLDLOCK) JOIN dbo.PlannedShifts S WITH(HOLDLOCK) ON S.ShiftId=A.ShiftId WHERE A.PersonId=@person AND A.CancelledUtc IS NULL AND S.Status<>N'Cancelled' AND (@shift IS NULL OR S.ShiftId<>@shift) AND S.StartUtc<@end AND S.EndUtc>@start)
 +(SELECT COUNT(*) FROM dbo.LeaveRequests L WITH(HOLDLOCK) WHERE L.PersonId=@person AND L.Status=N'Approved' AND (@leave IS NULL OR L.LeaveId<>@leave) AND L.StartUtc<@end AND L.EndUtc>@start)
 +(SELECT COUNT(*) FROM dbo.izinler I WITH(HOLDLOCK) WHERE @legacy=1 AND I.PersonId=@person AND I.IZINBASLANGIC<=@last AND I.IZINBITIS>=@first)");
        Id(c, "@person", person); Time(c, "@start", utc.Start); Time(c, "@end", utc.End); c.Parameters.Add("@shift", SqlDbType.UniqueIdentifier).Value = (object?)exceptShift ?? DBNull.Value; c.Parameters.Add("@leave", SqlDbType.UniqueIdentifier).Value = (object?)exceptLeave ?? DBNull.Value; c.Parameters.Add("@legacy", SqlDbType.Bit).Value = legacy; c.Parameters.Add("@first", SqlDbType.Date).Value = start.Date; c.Parameters.Add("@last", SqlDbType.Date).Value = end.AddTicks(-1).Date;
        if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Personelin başka vardiya, gönderilmiş/onaylı puantaj veya onaylı/eski izin kaydı bu aralıkla çakışıyor. Proje dışı ayrıntılar gösterilmez.");
    }
    internal static async Task RosterCancelAsync(IUnitOfWork w, Guid roster, CancellationToken ct)
    {
        using var find = w.Command("SELECT PersonId FROM dbo.PersonnelProjectAssignments WITH(HOLDLOCK) WHERE AssignmentId=@id"); Id(find, "@id", roster); if (await find.ExecuteScalarAsync(ct) is not Guid person) return; await LockPersonAsync(w, person, ct);
        using var c = w.Command(@"SELECT (SELECT COUNT(*) FROM dbo.ShiftAssignments A WITH(HOLDLOCK) JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId WHERE A.RosterAssignmentId=@id AND A.CancelledUtc IS NULL AND S.Status<>N'Cancelled')+(SELECT COUNT(*) FROM dbo.LeaveRequests WITH(HOLDLOCK) WHERE RosterAssignmentId=@id AND Status=N'Approved')+(SELECT COUNT(*) FROM dbo.Timesheets WITH(HOLDLOCK) WHERE RosterAssignmentId=@id AND Status<>N'Cancelled')+(SELECT COUNT(*) FROM dbo.TimesheetSegments S WITH(HOLDLOCK) JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId WHERE S.RosterAssignmentId=@id AND H.ReleasedUtc IS NULL)"); Id(c, "@id", roster); if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Atamaya bağlı etkin vardiya, onaylı izin veya puantaj var; önce ilgili takvim kayıtlarını gerekçeli iptal edin.");
    }
}
