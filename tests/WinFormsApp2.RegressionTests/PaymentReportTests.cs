using System.Data;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    private PaymentReportService Reports => new(connection, admin);
    private async Task<Guid> SeedReportPayment(decimal amount = 30m)
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 1000m), true);
        var id = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.General, Person().Tc, amount, id); return id;
    }
    private void ReportPaymentDate(Guid id, DateTime value)
    {
        using var c = new SqlConnection(connection); c.Open();
        using var cmd = new SqlCommand("UPDATE dbo.PaymentLedger SET CreatedUtc=@date WHERE RequestId=@id", c);
        cmd.Parameters.Add("@date", SqlDbType.DateTime2).Value = value; cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id; cmd.ExecuteNonQuery();
    }
    [Test, Category("Reports")]
    public async Task ReceiptReloadsReversalAndKeepsPaymentIdentityWithoutWriting()
    {
        var id = await SeedReportPayment(); var stale = await Service.LedgerAsync();
        await new PersonnelService(connection, CreateActor("second-admin", UserRole.Admin)).ReversePaymentAsync(id);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        var report = await Reports.ReceiptAsync(id);
        Assert.That(stale.AsEnumerable().Single(r => r["RequestId"] is Guid)["ReversedUtc"], Is.EqualTo(DBNull.Value));
        Assert.That(report.Reference, Is.EqualTo(id.ToString("N")));
        Assert.That(report.Lines, Does.Contain("Durum: İptal edildi"));
        Assert.That(report.Lines.Any(s => s.StartsWith("İptal zamanı (UTC):")), Is.True);
        Assert.That(report.Lines.Any(s => s.StartsWith("Veri zamanı (UTC):")), Is.True);
        Assert.That(report.Lines, Does.Contain("Raporu oluşturan: test-admin"));
        Assert.That(string.Join("\n", report.Lines), Does.Not.Contain(Person().Tc.ToString()));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits));
    }
    [Test, Category("Reports")]
    public async Task OldReceiptDoesNotBorrowDetailsFromRecreatedPerson()
    {
        var id = await SeedReportPayment(); await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync());
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 777m) with { FirstName = "Replacement" }, true);
        var report = await Reports.ReceiptAsync(id);
        Assert.That(report.Lines, Does.Contain("Durum: Kaydedildi"));
        Assert.That(string.Join("\n", report.Lines), Does.Not.Contain("Replacement").And.Not.Contain("777").And.Not.Contain(Person().Iban));
        Assert.That(report.Reference, Is.EqualTo(id.ToString("N")));
    }
    [Test, Category("Reports")]
    public async Task ReceiptRemainsAvailableAfterPersonDeletion()
    {
        var id = await SeedReportPayment(); await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync());
        Assert.That((await Reports.ReceiptAsync(id)).Reference, Is.EqualTo(id.ToString("N")));
    }
    [Test, Category("Reports")]
    public async Task ReceiptRejectsMissingOrNonPaymentRequestInsteadOfUsingCachedData()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Reports.ReceiptAsync(Guid.NewGuid()));
        var id = await SeedReportPayment(); Execute("UPDATE dbo.PaymentLedger SET Kind=N'Allocation' WHERE Kind=N'Payment'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => Reports.ReceiptAsync(id));
    }
    [Test, Category("Reports")]
    public async Task ClosingReloadsNewPaymentsAndReversalsWithoutRefreshingScreen()
    {
        var id = await SeedReportPayment(); var stale = await Service.LedgerAsync();
        await Service.ReversePaymentAsync(id);
        var other = new PersonnelService(connection, CreateActor("report-operator", UserRole.PersonnelManager));
        await other.PayAsync(PersonnelGroup.General, Person().Tc, 45m, Guid.NewGuid());
        var report = await Reports.ClosingAsync(DateTime.Today, 50m);
        Assert.That(stale.AsEnumerable().Count(r => r.Field<string>("Kind") == "Payment"), Is.EqualTo(1));
        Assert.That(report.Difference, Is.EqualTo(5m));
        Assert.That(report.Lines, Does.Contain("Geçerli ödeme sayısı: 1"));
        Assert.That(report.Lines, Does.Contain("Raporu oluşturan: test-admin"));
        Assert.That(report.Lines.Any(s => s.StartsWith("Veri zamanı (UTC):")), Is.True);
        Assert.That(report.Title, Does.Contain("ÖNİZLEMESİ"));
    }
    [Test, Category("Reports")]
    public async Task ClosingUsesLocalDayHalfOpenUtcRangeAndExcludesOtherKinds()
    {
        var id = await SeedReportPayment(10m); var day = new DateTime(2026, 10, 20);
        var range = PaymentReportService.UtcDayRange(day, TimeZoneInfo.Local);
        ReportPaymentDate(id, range.Start);
        var before = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 20m, before); ReportPaymentDate(before, range.Start.AddTicks(-1));
        var last = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, last); ReportPaymentDate(last, range.End.AddTicks(-1));
        var after = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 40m, after); ReportPaymentDate(after, range.End);
        var report = await Reports.ClosingAsync(day, 40m);
        Assert.That(report.Difference, Is.Zero); Assert.That(report.Lines, Does.Contain("Geçerli ödeme sayısı: 2"));
    }
    [Test, Category("Reports")]
    public async Task LaterReversalIsReflectedInOriginalDaysCurrentPreview()
    {
        var id = await SeedReportPayment(); var day = DateTime.Today.AddDays(-2); ReportPaymentDate(id, PaymentReportService.UtcDayRange(day, TimeZoneInfo.Local).Start.AddHours(1));
        Assert.That((await Reports.ClosingAsync(day, 30m)).Difference, Is.Zero);
        await Service.ReversePaymentAsync(id);
        Assert.That((await Reports.ClosingAsync(day, 30m)).Difference, Is.EqualTo(30m));
        Assert.That((await Reports.ClosingAsync(DateTime.Today, 0m)).Difference, Is.Zero);
    }
    [TestCase(UserRole.Viewer), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Admin), Category("Reports")]
    public async Task ReportsHonorReadRoleAndRevokedSession(UserRole role)
    {
        var id = await SeedReportPayment(); var actor = CreateActor("report-reader", role); var reports = new PaymentReportService(connection, actor);
        Assert.That((await reports.ReceiptAsync(id)).Reference, Is.EqualTo(id.ToString("N")));
        Assert.That((await reports.ClosingAsync(DateTime.Today, 30m)).Difference, Is.Zero);
        Execute("UPDATE dbo.admin SET IsActive=0 WHERE username=N'report-reader'");
        await Assert.ThrowsAsync<SessionRevokedException>(() => reports.ReceiptAsync(id));
        await Assert.ThrowsAsync<SessionRevokedException>(() => reports.ClosingAsync(DateTime.Today, 30m));
    }
    [Test, Category("Reports")]
    public async Task CancelledReportReadDoesNotProduceFallbackReport()
    {
        var id = await SeedReportPayment(); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.That(async () => await Reports.ReceiptAsync(id, cancel.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(async () => await Reports.ClosingAsync(DateTime.Today, 0m, cancel.Token), Throws.InstanceOf<OperationCanceledException>());
    }
    [Test, Category("Reports")]
    public async Task ClosingWaitsForUncommittedChangesAndNeverReportsDirtyTotals()
    {
        var id = await SeedReportPayment(); await using var c = new SqlConnection(connection); await c.OpenAsync();
        await using var transaction = (SqlTransaction)await c.BeginTransactionAsync();
        using var cmd = new SqlCommand("UPDATE dbo.PaymentLedger SET ReversedUtc=SYSUTCDATETIME() WHERE RequestId=@id", c, transaction);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id; await cmd.ExecuteNonQueryAsync();
        var reading = Reports.ClosingAsync(DateTime.Today, 30m);
        await Task.Delay(100); Assert.That(reading.IsCompleted, Is.False);
        await transaction.RollbackAsync();
        Assert.That((await reading.WaitAsync(TimeSpan.FromSeconds(10))).Difference, Is.Zero);
    }
    [Test, Apartment(ApartmentState.STA), Category("Reports")]
    public void PaymentScreenPrintsFreshReportsFromStaleGridAndOriginalSelection()
    {
        var id = SeedReportPayment().GetAwaiter().GetResult(); var stale = Service.LedgerAsync().GetAwaiter().GetResult();
        Service.ReversePaymentAsync(id).GetAwaiter().GetResult(); Service.PayAsync(PersonnelGroup.General, Person().Tc, 45m, Guid.NewGuid()).GetAwaiter().GetResult();
        var field = typeof(AppDatabase).GetField("settings", BindingFlags.NonPublic | BindingFlags.Static)!; object? previous = field.GetValue(null);
        var b = new SqlConnectionStringBuilder(connection); field.SetValue(null, new DatabaseSettings(b.DataSource, b.InitialCatalog, b.AttachDBFilename, 30));
        try
        {
            using var form = new PaymentsForm { Session = admin, PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
            form.Show();
            T Field<T>(string name) => (T)typeof(PaymentsForm).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            var grid = Field<DataGridView>("ledger"); grid.DataSource = stale; grid.ClearSelection();
            int index = stale.AsEnumerable().ToList().FindIndex(r => r["RequestId"] is Guid);
            // A modified displayed key cannot redirect the report away from the original selection.
            stale.Rows[index]["RequestId"] = Guid.NewGuid(); grid.Rows[index].Selected = true;
            PrintReport? captured = null; form.PreviewReport = (_, report, _) => captured = report;
            void InvokeAndWait(string method)
            {
                typeof(PaymentsForm).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, [null, EventArgs.Empty]);
                var timer = Stopwatch.StartNew(); while ((captured == null || form.IsOperationRunning) && timer.Elapsed < TimeSpan.FromSeconds(10)) { Application.DoEvents(); Thread.Sleep(5); }
                Assert.That(captured, Is.Not.Null, "Preview must use the authorized fresh report.");
            }
            InvokeAndWait("Receipt"); Assert.That(captured!.Reference, Is.EqualTo(id.ToString("N"))); Assert.That(captured.Lines, Does.Contain("Durum: İptal edildi"));
            captured = null; Field<TextBox>("declared").Text = "50"; Field<DateTimePicker>("day").Value = DateTime.Today;
            InvokeAndWait("PrintClosing"); Assert.That(captured!.Difference, Is.EqualTo(5m));
            Assert.That(captured.Lines, Does.Contain("Geçerli ödeme sayısı: 1")); form.Close();
        }
        finally { field.SetValue(null, previous); }
    }
}

[TestFixture, Category("Reports")]
public class PaymentReportInputTests
{
    [TestCase(-1), TestCase(1.001), TestCase(10000000000000000d)]
    public void InvalidDeclaredAmountIsRejectedBeforeConnection(decimal value)
    {
        var reports = new PaymentReportService("invalid", new("reader", UserRole.Viewer));
        Assert.ThrowsAsync<ArgumentException>(() => reports.ClosingAsync(DateTime.Today, value));
    }
    [Test]
    public void EmptyReceiptIdentityIsRejectedBeforeConnection()
    {
        var reports = new PaymentReportService("invalid", new("reader", UserRole.Viewer));
        Assert.ThrowsAsync<ArgumentException>(() => reports.ReceiptAsync(Guid.Empty));
    }
    [TestCase(3, 8, 23), TestCase(11, 1, 25)]
    public void DayRangeUsesActualDayLengthAcrossDaylightSaving(int month, int day, int hours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var range = PaymentReportService.UtcDayRange(new DateTime(2026, month, day), zone);
        Assert.That((range.End - range.Start).TotalHours, Is.EqualTo(hours));
        Assert.That(range.Start.Kind, Is.EqualTo(DateTimeKind.Utc));
    }
}
