using Microsoft.Data.SqlClient;
using NUnit.Framework;
using System.Data;
using System.Text.Json;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    private DateTime CompletedBusinessDay => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DailyClosingService.BusinessZone).Date.AddDays(-1);
    [Test, Category("Closings")]
    public async Task FrozenClosingSurvivesLaterReversalAndCannotBeEditedDeletedOrBackdatedInto()
    {
        using var scope = AuditContext.UseReason("Sentetik kapanış gerekçesi");
        Guid payment = await SeedReportPayment(30m);
        var day = CompletedBusinessDay;
        ReportPaymentDate(payment, PaymentReportService.UtcDayRange(day, DailyClosingService.BusinessZone).Start.AddHours(1));
        var closings = new DailyClosingService(connection, admin);
        Guid id = await closings.FinalizeAsync(day, 30m); var before = await closings.ReadAsync(id);
        Assert.That(before.PaymentCount, Is.EqualTo(1)); Assert.That(before.NetCash, Is.EqualTo(30m));
        Assert.That(await closings.FinalizeAsync(day, 30m), Is.EqualTo(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => closings.FinalizeAsync(day, 31m));
        Assert.Throws<SqlException>(() => Execute("UPDATE dbo.DailyClosings SET DeclaredCash=0"));
        Assert.Throws<SqlException>(() => Execute("DELETE dbo.DailyClosings"));
        await Service.ReversePaymentAsync(payment);
        var after = await closings.ReadAsync(id);
        Assert.That(JsonSerializer.Serialize(after), Is.EqualTo(JsonSerializer.Serialize(before)));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Reversal' AND ReversesLedgerId IS NOT NULL"), Is.EqualTo(1));
        Assert.Throws<SqlException>(() => ReportPaymentDate(payment, DateTime.UtcNow));
        using var lines = await closings.ExportLinesAsync(id);
        Assert.That(lines.Rows.Count, Is.EqualTo(1)); Assert.That(lines.Rows[0]["Hareket TL"], Is.EqualTo(-30m));
    }
    [Test, Category("Closings")]
    public async Task DailyClosingIncludesBothCashDirectionsAndAuditFailureRollsBackSnapshot()
    {
        using var scope = AuditContext.UseReason("Sentetik nakit hareketleri");
        var day = CompletedBusinessDay; var range = PaymentReportService.UtcDayRange(day, DailyClosingService.BusinessZone);
        Guid payment = await SeedReportPayment(30m); await Service.ReversePaymentAsync(payment);
        ReportPaymentDate(payment, range.Start.AddHours(1));
        using (var c = new SqlConnection(connection))
        {
            c.Open(); using var cmd = new SqlCommand("UPDATE dbo.PaymentLedger SET CreatedUtc=@time WHERE Kind=N'Reversal'", c);
            cmd.Parameters.Add("@time", SqlDbType.DateTime2).Value = range.Start.AddHours(2); cmd.ExecuteNonQuery();
        }
        var closings = new DailyClosingService(connection, admin);
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Closing.Finalize') THROW 51099,N'Synthetic audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(() => closings.FinalizeAsync(day, 0m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.DailyClosings"), Is.Zero);
        Execute("DROP TRIGGER dbo.GuideBlockAudit");
        var s = await closings.ReadAsync(await closings.FinalizeAsync(day, 0m));
        Assert.That(s.CashOut, Is.EqualTo(30m)); Assert.That(s.CashReturn, Is.EqualTo(30m)); Assert.That(s.NetCash, Is.Zero);
        Assert.That(s.Rows.Length, Is.EqualTo(2));
    }
    [Test, Category("Closings")]
    public async Task ClosingRequiresAdminAndCompletedDayWhileExportRequiresIndependentGrant()
    {
        using var scope = AuditContext.UseReason("Sentetik yetki kontrolü");
        var day = CompletedBusinessDay;
        var closings = new DailyClosingService(connection, admin);
        var today = day.AddDays(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => closings.FinalizeAsync(today, 0m));
        Guid id = await closings.FinalizeAsync(day, 0m);
        var reader = CreateActor("closing-reader", UserRole.Viewer);
        var read = new DailyClosingService(connection, reader);
        Assert.That((await read.ReadAsync(id)).ClosingId, Is.EqualTo(id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read.FinalizeAsync(day, 0m));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => read.ExportLinesAsync(id));
        Assert.That((await closings.PageAsync(0, 50, default)).Total, Is.EqualTo(1));
    }
}
