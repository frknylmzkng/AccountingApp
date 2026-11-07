using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WinFormsApp2;

internal sealed record ClosingRow(long LedgerId, long Tc, int Group, string Kind, decimal Delta, Guid? PersonId,
    Guid? RequestId, long? ReversesLedgerId, string Purpose, Guid? ProjectId, string Actor, DateTime CreatedUtc, DateTime? ReversedUtcAtClosing);
internal sealed record ClosingSnapshot(int Format, Guid ClosingId, DateTime BusinessDate, string TimeZoneId,
    DateTime RangeStartUtc, DateTime RangeEndUtc, DateTime ClosedUtc, string Actor, Guid ActorAccountId, string Reason,
    long PaymentCount, long ReversalCount, decimal CashOut, decimal CashReturn, decimal Declared, ClosingRow[] Rows)
{
    public decimal NetCash => CashOut - CashReturn;
    public decimal Difference => Declared - NetCash;
}

internal sealed class DailyClosingService(string connectionString, UserSession user)
{
    internal static TimeZoneInfo BusinessZone
    {
        get
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time"); }
        }
    }
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    internal async Task<Guid> FinalizeAsync(DateTime date, decimal declared, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).IdAsync("closings/finalize", new AccountingApp.Contracts.ClosingFinalize(DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified), declared, RemoteApi.Reason), "closingId", token).ConfigureAwait(false);
        user.DemandAdmin(); date = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);
        string reason = AuditContext.ValidateReason(AuditContext.Reason ?? throw new ArgumentException("Kesinleştirme için gerekçe gerekiyor."));
        if (declared < -9999999999999999.99m || declared > 9999999999999999.99m || decimal.Round(declared, 2) != declared)
            throw new ArgumentException("Beyan edilen net tutar iki ondalıklı geçerli bir TL değeri olmalı; iade ağırlıklı günde negatif olabilir.");
        var (start, end) = PaymentReportService.UtcDayRange(date, BusinessZone);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        DateTime now; Guid actorId;
        using (var clock = work.Command("SELECT SYSUTCDATETIME(),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))"))
        {
            using var r = await clock.ExecuteReaderAsync(token).ConfigureAwait(false); await r.ReadAsync(token).ConfigureAwait(false);
            now = Utc(r.GetDateTime(0)); actorId = r.GetGuid(1);
        }
        if (end > now) throw new InvalidOperationException("Yalnızca tamamlanmış iş günleri kesinleştirilebilir. Bugün için güncel önizlemeyi kullanın.");
        using (var gate = work.Command(@"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource,
            @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
            IF @result<0 THROW 51057,N'Kapanış kilidi alınamadı. Yeniden deneyin.',1;"))
        {
            gate.Text("@resource", "DailyClosing." + date.ToString("yyyyMMdd", CultureInfo.InvariantCulture), 100);
            await gate.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }
        using (var existing = work.Command("SELECT ClosingId,DeclaredCash FROM dbo.DailyClosings WITH(UPDLOCK,HOLDLOCK) WHERE BusinessDate=@day"))
        {
            existing.Parameters.Add("@day", SqlDbType.Date).Value = date;
            using var r = await existing.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await r.ReadAsync(token).ConfigureAwait(false))
            {
                if (r.GetDecimal(1) != declared) throw new InvalidOperationException("Bu gün zaten kesinleştirilmiş; beyan veya kapanış değiştirilemez.");
                return r.GetGuid(0);
            }
        }
        var rows = new List<ClosingRow>();
        using (var query = work.Command(@"SELECT LedgerId,TC,PersonnelGroup,Kind,Delta,PersonId,RequestId,ReversesLedgerId,
            PaymentPurpose,ProjectId,Actor,CreatedUtc,ReversedUtc FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK,INDEX(IX_Ledger_CreatedUtc))
            WHERE CreatedUtc>=@start AND CreatedUtc<@end AND Kind IN(N'Payment',N'Reversal') ORDER BY LedgerId"))
        {
            query.Parameters.Add("@start", SqlDbType.DateTime2).Value = start; query.Parameters.Add("@end", SqlDbType.DateTime2).Value = end;
            using var r = await query.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await r.ReadAsync(token).ConfigureAwait(false))
                rows.Add(new(r.GetInt64(0), r.GetInt64(1), r.GetInt32(2), r.GetString(3), r.GetDecimal(4),
                    r.IsDBNull(5) ? null : r.GetGuid(5), r.IsDBNull(6) ? null : r.GetGuid(6), r.IsDBNull(7) ? null : r.GetInt64(7),
                    r.GetString(8), r.IsDBNull(9) ? null : r.GetGuid(9), r.GetString(10), Utc(r.GetDateTime(11)), r.IsDBNull(12) ? null : Utc(r.GetDateTime(12))));
        }
        if (rows.Any(r => r.LedgerId <= 0 || (r.Kind == "Payment" && r.Delta >= 0) || (r.Kind == "Reversal" && r.Delta <= 0)))
            throw new InvalidOperationException("Defterde ödeme/iade işaret kuralına uymayan kayıt var. Kapanış kaydedilmedi; geçmiş kaydı kontrol edin.");
        decimal cashOut = -rows.Where(r => r.Kind == "Payment").Sum(r => r.Delta), cashReturn = rows.Where(r => r.Kind == "Reversal").Sum(r => r.Delta);
        using (var clock = work.Command("SELECT SYSUTCDATETIME()")) now = Utc((DateTime)(await clock.ExecuteScalarAsync(token).ConfigureAwait(false))!);
        var snapshot = new ClosingSnapshot(1, Guid.NewGuid(), date, "Europe/Istanbul", start, end, now, user.Username, actorId, reason,
            rows.LongCount(r => r.Kind == "Payment"), rows.LongCount(r => r.Kind == "Reversal"), cashOut, cashReturn, declared, rows.ToArray());
        string json = JsonSerializer.Serialize(snapshot);
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(json));
        using var save = work.Command(@"INSERT dbo.DailyClosings(ClosingId,BusinessDate,TimeZoneId,RangeStartUtc,RangeEndUtc,ClosedUtc,Actor,ActorAccountId,Reason,
            PaymentCount,ReversalCount,CashOut,CashReturn,NetCash,DeclaredCash,Difference,SnapshotJson,SnapshotHash)
            VALUES(@id,@day,@zone,@start,@end,@now,@actor,@actorId,@reason,@payments,@reversals,@out,@return,@net,@declared,@difference,@json,@hash)");
        save.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = snapshot.ClosingId; save.Parameters.Add("@day", SqlDbType.Date).Value = date;
        save.Text("@zone", snapshot.TimeZoneId, 64); save.Parameters.Add("@start", SqlDbType.DateTime2).Value = start;
        save.Parameters.Add("@end", SqlDbType.DateTime2).Value = end; save.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
        save.Text("@actor", user.Username); save.Parameters.Add("@actorId", SqlDbType.UniqueIdentifier).Value = actorId; save.Text("@reason", reason, 500);
        save.Parameters.Add("@payments", SqlDbType.BigInt).Value = snapshot.PaymentCount; save.Parameters.Add("@reversals", SqlDbType.BigInt).Value = snapshot.ReversalCount;
        void Money(string name, decimal value) { var p = save.Parameters.Add(name, SqlDbType.Decimal); p.Precision = 28; p.Scale = 2; p.Value = value; }
        Money("@out", cashOut); Money("@return", cashReturn); Money("@net", snapshot.NetCash); Money("@declared", declared); Money("@difference", snapshot.Difference);
        save.Text("@json", json, -1); save.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash;
        await save.ExecuteNonQueryAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false);
        return snapshot.ClosingId;
    }

    internal async Task<ClosingSnapshot> ReadAsync(Guid id, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).SendAsync<ClosingSnapshot>($"closings/{id:D}/view", null, token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        user.DemandViewSalary();
        using var cmd = work.Command(@"SELECT ClosingId,BusinessDate,TimeZoneId,RangeStartUtc,RangeEndUtc,ClosedUtc,Actor,ActorAccountId,Reason,
            PaymentCount,ReversalCount,CashOut,CashReturn,DeclaredCash,NetCash,Difference,SnapshotJson,SnapshotHash FROM dbo.DailyClosings WHERE ClosingId=@id");
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;
        ClosingSnapshot snapshot;
        using (var r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Kesinleşmiş kapanış bulunamadı.");
            string json = r.GetString(16);
            if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.Unicode.GetBytes(json)), r.GetFieldValue<byte[]>(17)))
                throw new InvalidOperationException("Kapanış içerik özeti doğrulanamadı.");
            snapshot = JsonSerializer.Deserialize<ClosingSnapshot>(json) ?? throw new InvalidOperationException("Kapanış içeriği okunamadı.");
            if (snapshot.Format != 1 || snapshot.ClosingId != r.GetGuid(0) || snapshot.BusinessDate != r.GetDateTime(1) ||
                snapshot.TimeZoneId != r.GetString(2) || snapshot.RangeStartUtc != Utc(r.GetDateTime(3)) || snapshot.RangeEndUtc != Utc(r.GetDateTime(4)) ||
                snapshot.ClosedUtc != Utc(r.GetDateTime(5)) || snapshot.Actor != r.GetString(6) || snapshot.ActorAccountId != r.GetGuid(7) || snapshot.Reason != r.GetString(8) ||
                snapshot.PaymentCount != r.GetInt64(9) || snapshot.ReversalCount != r.GetInt64(10) || snapshot.CashOut != r.GetDecimal(11) ||
                snapshot.CashReturn != r.GetDecimal(12) || snapshot.Declared != r.GetDecimal(13) || snapshot.NetCash != r.GetDecimal(14) || snapshot.Difference != r.GetDecimal(15) ||
                snapshot.Rows == null || snapshot.Rows.Any(x => x == null || x.LedgerId <= 0 || x.CreatedUtc < snapshot.RangeStartUtc || x.CreatedUtc >= snapshot.RangeEndUtc ||
                    (x.Kind != "Payment" && x.Kind != "Reversal") || (x.Kind == "Payment" && x.Delta >= 0) || (x.Kind == "Reversal" && x.Delta <= 0)) ||
                snapshot.Rows.Select(x => x.LedgerId).Distinct().LongCount() != snapshot.Rows.LongLength ||
                snapshot.PaymentCount != snapshot.Rows.LongCount(x => x.Kind == "Payment") ||
                snapshot.ReversalCount != snapshot.Rows.LongCount(x => x.Kind == "Reversal") || snapshot.CashOut != -snapshot.Rows.Where(x => x.Kind == "Payment").Sum(x => x.Delta) ||
                snapshot.CashReturn != snapshot.Rows.Where(x => x.Kind == "Reversal").Sum(x => x.Delta))
                throw new InvalidOperationException("Kapanış başlığı ve içeriği eşleşmiyor.");
        }
        await work.CommitAsync(token).ConfigureAwait(false); return snapshot;
    }
    internal async Task<PersonnelPage> PageAsync(long index, int size, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"closings?index={index}&size={size}", token).ConfigureAwait(false);
        if (index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz kapanış sayfası.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        user.DemandViewSalary();
        long total; using (var count = work.Command("SELECT COUNT_BIG(*) FROM dbo.DailyClosings"))
            total = Convert.ToInt64(await count.ExecuteScalarAsync(token).ConfigureAwait(false));
        long actual = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var query = work.Command(@"SELECT ClosingId,BusinessDate,TimeZoneId,ClosedUtc,Actor,PaymentCount,ReversalCount,CashOut,CashReturn,NetCash,DeclaredCash,Difference,Reason
            FROM dbo.DailyClosings ORDER BY BusinessDate DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY");
        query.Parameters.Add("@skip", SqlDbType.BigInt).Value = actual * size; query.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var rows = await query.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return new(rows, total, actual, size);
    }
    internal async Task<PrintReport> PrintAsync(Guid id, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Print(await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.PrintData>($"closings/{id:D}/print", null, token).ConfigureAwait(false));
        user.DemandExportPersonalData(); var s = await ReadAsync(id, token).ConfigureAwait(false);
        var lines = new List<string> { $"Gün: {s.BusinessDate:dd.MM.yyyy} · {s.TimeZoneId}", "KAPSAM: tüm veritabanı; ödeme ve iade hareketleri",
            $"Nakit çıkışı: {s.CashOut:N2} TL ({s.PaymentCount} ödeme)", $"Nakit iadesi: {s.CashReturn:N2} TL ({s.ReversalCount} iptal)",
            $"Net: {s.NetCash:N2} TL", $"Beyan: {s.Declared:N2} TL", $"Fark: {s.Difference:N2} TL",
            $"Kesinleşme UTC: {s.ClosedUtc:O}", $"Kesinleştiren: {s.Actor}", $"Gerekçe: {s.Reason}",
            "Kesinleşmiş içerik; sonradan yapılan iptaller bu kaydı değiştirmez." };
        lines.AddRange(s.Rows.Select(r => $"{r.LedgerId} · TC …{PaymentPrinting.Mask(r.Tc.ToString(CultureInfo.InvariantCulture))} · {r.Kind} · {r.Delta:N2} TL"));
        return new("KESİNLEŞMİŞ GÜNLÜK NAKİT KAPANIŞI", lines.AsReadOnly(), s.ClosingId.ToString("N"), s.Difference);
    }
    internal async Task<DataTable> ExportLinesAsync(Guid id, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"closings/{id:D}/lines", token).ConfigureAwait(false);
        user.DemandExportPersonalData(); var s = await ReadAsync(id, token).ConfigureAwait(false);
        var table = new DataTable("Kapanış hareketleri");
        table.Columns.Add("Kapanış kimliği"); table.Columns.Add("İş günü", typeof(DateTime)); table.Columns.Add("Defter no", typeof(long));
        table.Columns.Add("Maskeli TC"); table.Columns.Add("İşlem"); table.Columns.Add("Hareket TL", typeof(decimal));
        table.Columns.Add("Tür"); table.Columns.Add("Kaydeden"); table.Columns.Add("Hareket UTC", typeof(DateTime)); table.Columns.Add("İptal kaynağı defter no", typeof(long));
        foreach (var row in s.Rows) table.Rows.Add(s.ClosingId.ToString("D"), s.BusinessDate, row.LedgerId, PaymentPrinting.Mask(row.Tc.ToString(CultureInfo.InvariantCulture)),
            row.Kind, row.Delta, row.Purpose, row.Actor, row.CreatedUtc, (object?)row.ReversesLedgerId ?? DBNull.Value);
        return table;
    }
    internal async Task<DataTable> ExportSummaryAsync(Guid id, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"closings/{id:D}/summary", token).ConfigureAwait(false);
        user.DemandExportPersonalData(); var s = await ReadAsync(id, token).ConfigureAwait(false);
        var table = new DataTable("Kapanış özeti");
        table.Columns.Add("Kapanış kimliği"); table.Columns.Add("İş günü", typeof(DateTime)); table.Columns.Add("Saat dilimi");
        table.Columns.Add("Dönem başlangıç UTC", typeof(DateTime)); table.Columns.Add("Dönem bitiş UTC", typeof(DateTime));
        table.Columns.Add("Kesinleşme UTC", typeof(DateTime)); table.Columns.Add("Kesinleştiren"); table.Columns.Add("Gerekçe");
        table.Columns.Add("Ödeme adedi", typeof(long)); table.Columns.Add("İade adedi", typeof(long));
        foreach (string name in new[] { "Nakit çıkışı TL", "Nakit iadesi TL", "Net TL", "Beyan TL", "Fark TL" }) table.Columns.Add(name, typeof(decimal));
        table.Rows.Add(s.ClosingId.ToString("D"), s.BusinessDate, s.TimeZoneId, s.RangeStartUtc, s.RangeEndUtc, s.ClosedUtc, s.Actor, s.Reason,
            s.PaymentCount, s.ReversalCount, s.CashOut, s.CashReturn, s.NetCash, s.Declared, s.Difference);
        return table;
    }
}
