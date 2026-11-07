using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed partial class EmploymentService
{
    internal async Task TransitionGroupAsync(Wire.PersonnelGroupTransition input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("employment/group-transition", input, ct); return; }
        user.DemandEdit(); user.DemandViewSalary();
        var source = Group(input.SourceGroup); var target = source == PersonnelGroup.General ? PersonnelGroup.Joker : PersonnelGroup.General;
        var pv = PersonnelConcurrency.RequireVersion(input.PersonVersion); var ev = PersonnelConcurrency.RequireVersion(input.EmploymentVersion);
        DateTime start = Day(input.EffectiveDate)!.Value;
        if (input.PersonId == Guid.Empty || input.TransitionId == Guid.Empty || input.EmploymentId == Guid.Empty || start == DateTime.MinValue) throw new ArgumentException("Geçerli kişi, dönem, işlem kimliği ve tarih gerekiyor.");
        string text = AuditContext.ValidateReason(input.Reason ?? ""); using var reason = AuditContext.UseReason(text);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Edit, ct);
        // Same person lock as assignment writes; account authorization always comes first.
        using (var gate = w.Command("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @r<0 THROW 51072,N'Personel geçiş kilidi alınamadı.',1;"))
        { gate.Text("@key", "Assignment.Person." + input.PersonId.ToString("N"), 100); await gate.ExecuteNonQueryAsync(ct); }
        // Recover an already committed intent before checking the now-stale card.
        using (var prior = w.Command("SELECT PersonId,SourceGroup,EffectiveDate,SourceEmploymentId,SourcePersonVersion,SourceEmploymentVersion,Reason FROM dbo.PersonnelGroupTransitions WITH(UPDLOCK,HOLDLOCK) WHERE TransitionId=@id"))
        {
            Id(prior, "@id", input.TransitionId); using var r = await prior.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                if (r.GetGuid(0) != input.PersonId || r.GetInt32(1) != (int)source || r.GetDateTime(2) != start || r.GetGuid(3) != input.EmploymentId || !r.GetFieldValue<byte[]>(4).SequenceEqual(pv) || !r.GetFieldValue<byte[]>(5).SequenceEqual(ev) || r.GetString(6) != text) throw new InvalidOperationException("İşlem kimliği başka geçiş için kullanılmış.");
                await r.DisposeAsync(); await w.CommitAsync(ct); return;
            }
        }
        DateTime now;
        using (var clock = w.Command("SELECT SYSUTCDATETIME()")) now = DateTime.SpecifyKind((DateTime)(await clock.ExecuteScalarAsync(ct))!, DateTimeKind.Utc);
        if (start > TimeZoneInfo.ConvertTimeFromUtc(now, DailyClosingService.BusinessZone).Date) throw new ArgumentException("Gelecek tarihli geçiş henüz desteklenmiyor; bugün veya geçmiş bir tarih seçin.");
        var (cutoff, _) = PaymentReportService.UtcDayRange(start, DailyClosingService.BusinessZone);
        string sourceTable = PersonnelRepository.Table(source), targetTable = PersonnelRepository.Table(target);
        // All cross-group operations lock General then Joker, including the absent target key range.
        using (var cards = w.Command("SELECT PersonId FROM dbo.Calisan WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person; SELECT PersonId FROM dbo.Joker WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person")) { Id(cards, "@person", input.PersonId); using var r = await cards.ExecuteReaderAsync(ct); do { while (await r.ReadAsync(ct)) { } } while (await r.NextResultAsync(ct)); }
        long tc; string first, last, iban;
        using (var c = w.Command($"SELECT TC,AD,SOYAD,IBAN,COALESCE(PARA,0) FROM {sourceTable} WHERE PersonId=@person AND RecordVersion=@v AND ArchivedUtc IS NULL AND CurrentEmploymentId=@employment"))
        {
            Id(c, "@person", input.PersonId); Id(c, "@employment", input.EmploymentId); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = pv; using var r = await c.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            tc = r.GetInt64(0); first = r.IsDBNull(1) ? "" : r.GetString(1); last = r.IsDBNull(2) ? "" : r.GetString(2); iban = r.IsDBNull(3) ? "" : r.GetString(3);
            if (r.GetDecimal(4) != 0) throw new InvalidOperationException("Geçiş için eski grup bakiyesi sıfır olmalı. Ödeme veya düzeltmeyi kendi yetkili ekranında tamamlayın; bakiye taşınmaz.");
        }
        using (var c = w.Command("SELECT StartDate,EndDate FROM dbo.EmploymentPeriods WITH(UPDLOCK,HOLDLOCK) WHERE EmploymentId=@id AND PersonId=@person AND PersonnelGroup=@group AND RecordVersion=@v AND IsClosed=0"))
        {
            Id(c, "@id", input.EmploymentId); Id(c, "@person", input.PersonId); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)source; c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = ev; using var r = await c.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct) || (!r.IsDBNull(0) && start <= r.GetDateTime(0))) throw new InvalidOperationException("Dönem değişmiş veya geçiş tarihi mevcut başlangıçtan sonra değil.");
            if (!r.IsDBNull(1) && r.GetDateTime(1) != start.AddDays(-1)) throw new InvalidOperationException("Mevcut ayrılış tarihi geçişten önceki günle eşleşmiyor; tarihleri gözden geçirin.");
        }
        bool exists;
        using (var c = w.Command($"SELECT ArchivedUtc,COALESCE(PARA,0) FROM {targetTable} WHERE PersonId=@person"))
        { Id(c, "@person", input.PersonId); using var r = await c.ExecuteReaderAsync(ct); exists = await r.ReadAsync(ct); if (exists && (r.IsDBNull(0) || r.GetDecimal(1) != 0)) throw new InvalidOperationException("Hedef grubun önceki kartı arşivde ve sıfır bakiyeli olmalı."); }
        using (var c = w.Command(@"SELECT COUNT(*) FROM dbo.PaymentLedger P JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 WHERE L.EmploymentId=@employment AND P.Delta<>0 AND (P.CreatedUtc>=@cutoff OR P.ReversedUtc>=@cutoff)"))
        { Id(c, "@employment", input.EmploymentId); c.Parameters.Add("@cutoff", SqlDbType.DateTime2).Value = cutoff; if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) != 0) throw new InvalidOperationException("Geçiş gününde veya sonrasında eski döneme ait parasal hareket var. Daha sonraki bir tarih seçin; finansal geçmiş yeniden tarihlenmez."); }
        // Close validates every current leave, non-cancelled assignment and accrual.
        await CloseAsync(w, source, input.PersonId, start.AddDays(-1), ct);
        await CheckPreviousAsync(w, input.PersonId, Guid.Empty, start, ct);
        using (var c = w.Command($"UPDATE {sourceTable} SET ArchivedUtc=SYSUTCDATETIME(),ArchivedBy=@actor WHERE PersonId=@person AND RecordVersion=@v AND ArchivedUtc IS NULL"))
        { Id(c, "@person", input.PersonId); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = pv; c.Text("@actor", user.Username); if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage); }
        using (var c = w.Command("INSERT dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor,PaymentPurpose) VALUES(@person,@tc,@group,0,N'Closure',@actor,N'Unclassified')"))
        { Id(c, "@person", input.PersonId); c.Tc(tc); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)source; c.Text("@actor", user.Username); await c.ExecuteNonQueryAsync(ct); }
        Guid next = Guid.NewGuid();
        using (var c = w.Command("INSERT dbo.EmploymentPeriods(EmploymentId,PersonId,PersonnelGroup,StartDate,IsLegacy,IsClosed,CreatedBy) VALUES(@id,@person,@group,@start,0,0,@actor)"))
        { Id(c, "@id", next); Id(c, "@person", input.PersonId); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)target; Date(c, "@start", start); c.Text("@actor", user.Username); await c.ExecuteNonQueryAsync(ct); }
        using (var c = w.Command(exists
            ? $"UPDATE {targetTable} SET AD=@first,SOYAD=@last,IBAN=@iban,CurrentEmploymentId=@employment,ArchivedUtc=NULL,ArchivedBy=NULL WHERE PersonId=@person AND ArchivedUtc IS NOT NULL AND COALESCE(PARA,0)=0"
            : $"INSERT {targetTable}(PersonId,TC,AD,SOYAD,IBAN,PARA,CurrentEmploymentId) VALUES(@person,@tc,@first,@last,@iban,0,@employment)"))
        {
            Id(c, "@person", input.PersonId); Id(c, "@employment", next); c.Tc(tc); c.Text("@first", first); c.Text("@last", last); c.Text("@iban", iban, 34);
            if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Hedef kart değişti; geçiş geri alındı.");
        }
        using (var c = w.Command("INSERT dbo.PersonnelGroupTransitions(TransitionId,PersonId,SourceGroup,TargetGroup,EffectiveDate,SourceEmploymentId,TargetEmploymentId,SourcePersonVersion,SourceEmploymentVersion,Actor,Reason) VALUES(@id,@person,@source,@target,@date,@old,@new,@pv,@ev,@actor,@reason)"))
        {
            Id(c, "@id", input.TransitionId); Id(c, "@person", input.PersonId); Id(c, "@old", input.EmploymentId); Id(c, "@new", next); c.Parameters.Add("@source", SqlDbType.Int).Value = (int)source; c.Parameters.Add("@target", SqlDbType.Int).Value = (int)target;
            Date(c, "@date", start); c.Parameters.Add("@pv", SqlDbType.Binary, 8).Value = pv; c.Parameters.Add("@ev", SqlDbType.Binary, 8).Value = ev; c.Text("@actor", user.Username); c.Text("@reason", text, 500); await c.ExecuteNonQueryAsync(ct);
        }
        await w.CommitAsync(ct);
    }
}
