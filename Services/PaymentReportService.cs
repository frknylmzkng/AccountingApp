using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

// Report input comes from a fresh, authorized transaction, never from a bound grid.
internal sealed class PaymentReportService(string connectionString, UserSession user)
{
    public async Task<PrintReport> ReceiptAsync(Guid requestId, CancellationToken token = default)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("Geçerli bir ödeme seçin.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        using var command = work.Command(@"SELECT TC,PersonnelGroup,CreatedUtc,Delta,Actor,ReversedUtc
            FROM dbo.PaymentLedger WHERE RequestId=@id AND Kind=N'Payment'");
        command.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
        var rows = await command.TableAsync(token).ConfigureAwait(false);
        if (rows.Rows.Count != 1) throw new InvalidOperationException("Ödeme bulunamadı. Defteri yenileyip ödeme satırını yeniden seçin.");
        var row = rows.Rows[0];
        var lines = new List<string>
        {
            $"Personel: TC {PaymentPrinting.Mask(row.Field<long>("TC").ToString(CultureInfo.InvariantCulture))}",
            $"Grup: {(row.Field<int>("PersonnelGroup") == (int)PersonnelGroup.Joker ? "Joker" : "Genel")}",
            $"Tutar: {-row.Field<decimal>("Delta"):N2} TL",
            $"Kayıt zamanı (UTC): {row.Field<DateTime>("CreatedUtc"):dd.MM.yyyy HH:mm:ss}",
            $"Kaydeden: {row.Field<string>("Actor")}",
            $"Durum: {(row.IsNull("ReversedUtc") ? "Kaydedildi" : "İptal edildi") }"
        };
        if (row["ReversedUtc"] is DateTime reversed) lines.Add($"İptal zamanı (UTC): {reversed:dd.MM.yyyy HH:mm:ss}");
        // Current personnel details may belong to a recreated record. They are not historical receipt data.
        lines.Add("Fiş ödeme kaydını gösterir; güncel personel bakiyesi ve IBAN'ı içermez.");
        await AddReadTimeAsync(work, lines, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
        return new("PERSONEL ÖDEME FİŞİ", lines.AsReadOnly(), requestId.ToString("N"));
    }

    public async Task<PrintReport> ClosingAsync(DateTime day, decimal declared, CancellationToken token = default)
    {
        if (declared < 0 || declared > 9999999999999999.99m || decimal.Round(declared, 2) != declared)
            throw new ArgumentException("Beyan edilen toplam geçerli, negatif olmayan bir TL tutarı olmalı.");
        var zone = TimeZoneInfo.Local;
        var (start, end) = UtcDayRange(day, zone);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        using var command = work.Command(@"SELECT TC,Delta FROM dbo.PaymentLedger
            WHERE Kind=N'Payment' AND ReversedUtc IS NULL AND CreatedUtc>=@start AND CreatedUtc<@end
            ORDER BY LedgerId");
        command.Parameters.Add("@start", SqlDbType.DateTime2).Value = start;
        command.Parameters.Add("@end", SqlDbType.DateTime2).Value = end;
        var rows = await command.TableAsync(token).ConfigureAwait(false);
        decimal total = -rows.AsEnumerable().Sum(r => r.Field<decimal>("Delta"));
        var lines = new List<string>
        {
            $"Gün: {day:dd.MM.yyyy}", $"Saat dilimi: {zone.Id}",
            $"Geçerli ödeme sayısı: {rows.Rows.Count}", $"Kayıt toplamı: {total:N2} TL", $"Beyan edilen: {declared:N2} TL",
            "Bu bir güncel durum önizlemesidir; kesinleştirilmiş kapanış değildir.",
            "Ödemeler kayıt gününe aittir. Sonradan iptal edilenler o günün güncel toplamından çıkarılır."
        };
        lines.AddRange(rows.AsEnumerable().Select(r => $"TC …{r.Field<long>("TC").ToString(CultureInfo.InvariantCulture)[^4..]}   {-r.Field<decimal>("Delta"):N2} TL"));
        await AddReadTimeAsync(work, lines, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
        return new("GÜNLÜK ÖDEME KAPANIŞ ÖNİZLEMESİ", lines.AsReadOnly(), "closing-" + day.ToString("yyyyMMdd", CultureInfo.InvariantCulture), declared - total);
    }

    internal static (DateTime Start, DateTime End) UtcDayRange(DateTime day, TimeZoneInfo zone)
    {
        var start = DateTime.SpecifyKind(day.Date, DateTimeKind.Unspecified);
        var end = start.AddDays(1);
        if (zone.IsInvalidTime(start) || zone.IsInvalidTime(end)) throw new ArgumentException("Seçilen günün saat dilimi sınırı geçersiz.");
        return (TimeZoneInfo.ConvertTimeToUtc(start, zone), TimeZoneInfo.ConvertTimeToUtc(end, zone));
    }

    private async Task AddReadTimeAsync(IUnitOfWork work, List<string> lines, CancellationToken token)
    {
        using var clock = work.Command("SELECT SYSUTCDATETIME()");
        var time = (DateTime)(await clock.ExecuteScalarAsync(token).ConfigureAwait(false))!;
        lines.Add($"Veri zamanı (UTC): {time:dd.MM.yyyy HH:mm:ss.fffffff}");
        lines.Add($"Raporu oluşturan: {user.Username}");
    }
}
