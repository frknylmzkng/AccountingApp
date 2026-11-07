using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed partial class EmploymentService(string connectionString, UserSession user)
{
    internal static DateTime? Day(DateTime? value)
    { if (value.HasValue && (value.Value.Kind != DateTimeKind.Unspecified || value.Value.TimeOfDay != TimeSpan.Zero)) throw new ArgumentException("Çalışma tarihi saat/saat dilimi içeremez."); return value; }
    private static void Id(SqlCommand c, string key, Guid value) => c.Parameters.Add(key, SqlDbType.UniqueIdentifier).Value = value;
    private static void Date(SqlCommand c, string key, DateTime? value) => c.Parameters.Add(key, SqlDbType.Date).Value = (object?)Day(value) ?? DBNull.Value;
    internal static async Task<Guid> CurrentAsync(IUnitOfWork w, PersonnelGroup group, Guid person, CancellationToken ct)
    {
        using var c = w.Command($"SELECT E.EmploymentId FROM {PersonnelRepository.Table(group)} C WITH(UPDLOCK,HOLDLOCK) JOIN dbo.EmploymentPeriods E ON E.EmploymentId=C.CurrentEmploymentId AND E.PersonId=C.PersonId AND E.PersonnelGroup=@group WHERE C.PersonId=@person"); Id(c, "@person", person); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        return await c.ExecuteScalarAsync(ct) is Guid id ? id : throw new InvalidOperationException("Çalışma dönemi bulunamadı; kartı yenileyin.");
    }
    internal static async Task ValidateRangeAsync(IUnitOfWork w, PersonnelGroup group, Guid person, DateTime start, DateTime? end, CancellationToken ct)
    {
        var id = await CurrentAsync(w, group, person, ct); using var c = w.Command("SELECT StartDate,EndDate,IsClosed FROM dbo.EmploymentPeriods WITH(HOLDLOCK) WHERE EmploymentId=@id"); Id(c, "@id", id); using var r = await c.ExecuteReaderAsync(ct); await r.ReadAsync(ct);
        if (r.GetBoolean(2) || (!r.IsDBNull(0) && start.Date < r.GetDateTime(0)) || (!r.IsDBNull(1) && (!end.HasValue || end.Value.Date > r.GetDateTime(1)))) throw new InvalidOperationException("Tarihler güncel çalışma dönemi dışında.");
    }
    internal static async Task EnsureCurrentRecordAsync(IUnitOfWork w, PersonnelGroup group, Guid person, string kind, string key, CancellationToken ct)
    {
        Guid current = await CurrentAsync(w, group, person, ct); using var c = w.Command("SELECT EmploymentId FROM dbo.EmploymentRecordLinks WHERE RecordKind=@kind AND RecordKey=@key AND PersonId=@person"); c.Text("@kind", kind, 20); c.Text("@key", key, 64); Id(c, "@person", person);
        if (await c.ExecuteScalarAsync(ct) is not Guid linked || linked != current) throw new InvalidOperationException("Bu kayıt önceki çalışma dönemine ait; yeni dönemin bakiyesi/ataması değiştirilemez.");
    }
    internal static async Task InitialDateAsync(IUnitOfWork w, PersonnelGroup group, long tc, DateTime start, CancellationToken ct)
    {
        Day(start); using var c = w.Command($"UPDATE E SET StartDate=@start FROM dbo.EmploymentPeriods E JOIN {PersonnelRepository.Table(group)} C ON C.CurrentEmploymentId=E.EmploymentId WHERE C.TC=@tc AND E.IsLegacy=0 AND E.IsClosed=0 AND E.StartDate IS NULL"); c.Tc(tc); Date(c, "@start", start); if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("İlk çalışma dönemi kaydedilemedi.");
    }
    private static async Task CheckBoundsAsync(IUnitOfWork w, Guid employment, DateTime? start, DateTime? end, CancellationToken ct)
    {
        Day(start); Day(end); if (start.HasValue && end < start) throw new ArgumentException("Ayrılış tarihi işe girişten önce olamaz.");
        using var c = w.Command(@"SELECT COUNT(*) FROM(
 SELECT IZINBASLANGIC S,IZINBITIS E FROM dbo.izinler WHERE EmploymentId=@id
 UNION ALL SELECT COALESCE(CONVERT(date,T.LocalStart),T.WorkDate),COALESCE(CONVERT(date,DATEADD(minute,-1,T.LocalEnd)),T.WorkDate) FROM dbo.Timesheets T WHERE T.EmploymentId=@id AND T.Status<>N'Cancelled'
 UNION ALL SELECT CONVERT(date,S.LocalStart),CONVERT(date,DATEADD(minute,-1,S.LocalEnd)) FROM dbo.ShiftAssignments A JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId WHERE A.EmploymentId=@id AND A.CancelledUtc IS NULL AND S.Status<>N'Cancelled'
 UNION ALL SELECT CONVERT(date,L.LocalStart),CONVERT(date,DATEADD(minute,-1,L.LocalEnd)) FROM dbo.LeaveRequests L WHERE L.EmploymentId=@id AND L.Status=N'Approved'
 UNION ALL SELECT A.StartDate,A.EndDate FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE L.EmploymentId=@id AND A.CancelledUtc IS NULL
 UNION ALL SELECT F.PeriodStart,F.PeriodEnd FROM dbo.FinanceEntries F JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) WHERE L.EmploymentId=@id AND F.ReversedUtc IS NULL) X
 WHERE (@start IS NOT NULL AND S<@start) OR (@end IS NOT NULL AND (E IS NULL OR E>@end))"); Id(c, "@id", employment); Date(c, "@start", start); Date(c, "@end", end);
        if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Mevcut izin/atama/hakediş bu tarih aralığı dışında. Önce ilgili kayıtları gerekçeli düzeltin.");
    }
    internal static async Task CloseAsync(IUnitOfWork w, PersonnelGroup group, Guid person, DateTime? end, CancellationToken ct)
    {
        Guid id = await CurrentAsync(w, group, person, ct); DateTime? start; bool legacy;
        using (var c = w.Command("SELECT StartDate,IsLegacy FROM dbo.EmploymentPeriods WITH(UPDLOCK,HOLDLOCK) WHERE EmploymentId=@id AND IsClosed=0")) { Id(c, "@id", id); using var r = await c.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Etkin çalışma dönemi yok."); start = r.IsDBNull(0) ? null : r.GetDateTime(0); legacy = r.GetBoolean(1); }
        if (!legacy && !end.HasValue) throw new ArgumentException("Bu dönem için ayrılış tarihi gerekiyor."); await CheckBoundsAsync(w, id, start, end, ct);
        using var update = w.Command("UPDATE dbo.EmploymentPeriods SET EndDate=@end,IsClosed=1 WHERE EmploymentId=@id AND IsClosed=0"); Id(update, "@id", id); Date(update, "@end", end); await update.ExecuteNonQueryAsync(ct);
    }
    internal static async Task UndoArchiveAsync(IUnitOfWork w, PersonnelGroup group, Guid person, CancellationToken ct)
    { Guid id = await CurrentAsync(w, group, person, ct); using var c = w.Command("UPDATE dbo.EmploymentPeriods SET IsClosed=0 WHERE EmploymentId=@id AND IsClosed=1"); Id(c, "@id", id); if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Arşiv dönemi değişmiş."); }
    internal async Task<DataTable> ListAsync(PersonnelGroup group, Guid person, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"employment/{group}/{person:D}", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.GlobalRead, ct);
        using var c = w.Command($"SELECT E.*,CAST(CASE WHEN C.CurrentEmploymentId=E.EmploymentId THEN 1 ELSE 0 END AS bit) IsCurrent FROM dbo.EmploymentPeriods E JOIN {PersonnelRepository.Table(group)} C ON C.PersonId=E.PersonId WHERE E.PersonId=@person AND E.PersonnelGroup=@group ORDER BY E.CreatedUtc,E.EmploymentId"); Id(c, "@person", person); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)group; var data = await c.TableAsync(ct); await w.CommitAsync(ct); return data;
    }
    internal async Task DatesAsync(Wire.EmploymentDates input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("employment/dates", input, ct); return; }
        user.DemandEdit(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); var group = Group(input.Group); var pv = PersonnelConcurrency.RequireVersion(input.PersonVersion); var ev = PersonnelConcurrency.RequireVersion(input.EmploymentVersion);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Edit, ct);
        await LockCardAsync(w, group, input.PersonId, pv, null, ct); Guid current = await CurrentAsync(w, group, input.PersonId, ct);
        if (current != input.EmploymentId) throw new InvalidOperationException("Yalnız kartın güncel dönemi düzeltilebilir.");
        using (var check = w.Command("SELECT IsLegacy,IsClosed FROM dbo.EmploymentPeriods WITH(UPDLOCK,HOLDLOCK) WHERE EmploymentId=@id"))
        { Id(check, "@id", current); using var r = await check.ExecuteReaderAsync(ct); await r.ReadAsync(ct); if (!r.GetBoolean(0) && (!input.Start.HasValue || (r.GetBoolean(1) && !input.End.HasValue))) throw new ArgumentException("Yeni dönemlerde işe giriş ve kapalı dönemlerde ayrılış tarihi boş bırakılamaz."); }
        await CheckBoundsAsync(w, current, input.Start, input.End, ct);
        await CheckPreviousAsync(w, input.PersonId, current, input.Start, ct);
        using var c = w.Command("UPDATE dbo.EmploymentPeriods SET StartDate=@start,EndDate=@end WHERE EmploymentId=@id AND RecordVersion=@v"); Id(c, "@id", current); Date(c, "@start", input.Start); Date(c, "@end", input.End); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = ev; if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Dönem değişmiş; listeyi yenileyin.");
        // Date corrections invalidate snapshots and pending new debits too.
        using (var card = w.Command($"UPDATE {PersonnelRepository.Table(group)} SET CurrentEmploymentId=CurrentEmploymentId WHERE PersonId=@person AND RecordVersion=@v")) { Id(card, "@person", input.PersonId); card.Parameters.Add("@v", SqlDbType.Binary, 8).Value = pv; if (await card.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage); }
        await w.CommitAsync(ct);
    }
    internal async Task RehireAsync(Wire.EmploymentRehire input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("employment/rehire", input, ct); return; }
        user.DemandEdit(); user.DemandViewSalary(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); var group = Group(input.Group); var pv = PersonnelConcurrency.RequireVersion(input.PersonVersion); Day(input.Start);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Edit, ct);
        decimal balance = await LockCardAsync(w, group, input.PersonId, pv, true, ct); if (balance != 0) throw new InvalidOperationException("Eski dönem bakiyesi sıfır olmalı. Arşivlemeyi aynı dönem olarak geri alıp eski dönemin finans işlemlerini tamamlayın; ardından yeniden arşivleyin.");
        Guid old = await CurrentAsync(w, group, input.PersonId, ct);
        using (var c = w.Command("SELECT EndDate,IsLegacy,IsClosed FROM dbo.EmploymentPeriods WITH(UPDLOCK,HOLDLOCK) WHERE EmploymentId=@id")) { Id(c, "@id", old); using var r = await c.ExecuteReaderAsync(ct); await r.ReadAsync(ct); if (!r.GetBoolean(2) || (!r.IsDBNull(0) && input.Start <= r.GetDateTime(0)) || (r.IsDBNull(0) && !r.GetBoolean(1))) throw new InvalidOperationException("Önceki dönem kapalı ve ayrılış tarihi yeni başlangıçtan önce olmalı. Bilinmeyen eski aktarım dönemi ayrı etiketlenir."); }
        using (var c = w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND CancelledUtc IS NULL AND (EndDate IS NULL OR EndDate>=@start)")) { Id(c, "@person", input.PersonId); Date(c, "@start", input.Start); if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Yeni başlangıca uzanan eski atamaları önce iptal edin."); }
        await CheckPreviousAsync(w, input.PersonId, Guid.Empty, input.Start, ct);
        Guid id = Guid.NewGuid(); using (var c = w.Command("INSERT dbo.EmploymentPeriods(EmploymentId,PersonId,PersonnelGroup,StartDate,IsLegacy,IsClosed,CreatedBy) VALUES(@id,@person,@group,@start,0,0,@actor)")) { Id(c, "@id", id); Id(c, "@person", input.PersonId); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)group; Date(c, "@start", input.Start); c.Text("@actor", user.Username); await c.ExecuteNonQueryAsync(ct); }
        using (var c = w.Command($"UPDATE {PersonnelRepository.Table(group)} SET CurrentEmploymentId=@id,ArchivedUtc=NULL,ArchivedBy=NULL WHERE PersonId=@person AND RecordVersion=@v AND ArchivedUtc IS NOT NULL")) { Id(c, "@id", id); Id(c, "@person", input.PersonId); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = pv; if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage); }
        await w.CommitAsync(ct);
    }
    private static async Task CheckPreviousAsync(IUnitOfWork w, Guid person, Guid except, DateTime? start, CancellationToken ct)
    {
        using var c = w.Command(@"SELECT COUNT(*) FROM (
 SELECT E.EndDate EndDate FROM dbo.EmploymentPeriods E WHERE E.PersonId=@person AND E.EmploymentId<>@except
 UNION ALL SELECT COALESCE(CONVERT(date,DATEADD(minute,-1,T.LocalEnd)),T.WorkDate) FROM dbo.Timesheets T WHERE T.PersonId=@person AND T.EmploymentId<>@except AND T.Status<>N'Cancelled'
 UNION ALL SELECT CONVERT(date,DATEADD(minute,-1,S.LocalEnd)) FROM dbo.ShiftAssignments A JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId WHERE A.PersonId=@person AND A.EmploymentId<>@except AND A.CancelledUtc IS NULL AND S.Status<>N'Cancelled'
 UNION ALL SELECT CONVERT(date,DATEADD(minute,-1,L.LocalEnd)) FROM dbo.LeaveRequests L WHERE L.PersonId=@person AND L.EmploymentId<>@except AND L.Status=N'Approved'
 UNION ALL SELECT I.IZINBITIS FROM dbo.izinler I WHERE I.PersonId=@person AND I.EmploymentId<>@except
 UNION ALL SELECT F.PeriodEnd FROM dbo.FinanceEntries F JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) WHERE L.PersonId=@person AND L.EmploymentId<>@except AND F.ReversedUtc IS NULL
 UNION ALL SELECT A.EndDate FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE L.PersonId=@person AND L.EmploymentId<>@except AND A.CancelledUtc IS NULL) P
 WHERE @start IS NULL OR EndDate>=@start"); Id(c, "@person", person); Id(c, "@except", except); Date(c, "@start", start);
        if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Yeni başlangıç önceki dönemlerin ayrılış/izin/atama/hakediş tarihlerinden sonra olmalı.");
    }
    private static PersonnelGroup Group(Wire.PersonnelGroup group) => Enum.IsDefined(group) ? (PersonnelGroup)group : throw new ArgumentException("Geçersiz grup.");
    private static async Task<decimal> LockCardAsync(IUnitOfWork w, PersonnelGroup group, Guid person, byte[] version, bool? archived, CancellationToken ct)
    { using var c = w.Command($"SELECT PARA FROM {PersonnelRepository.Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND RecordVersion=@v" + (archived.HasValue ? (archived.Value ? " AND ArchivedUtc IS NOT NULL" : " AND ArchivedUtc IS NULL") : "")); Id(c, "@person", person); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = version; object? balance = await c.ExecuteScalarAsync(ct); return balance == null ? throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage) : balance == DBNull.Value ? 0 : Convert.ToDecimal(balance); }
}
