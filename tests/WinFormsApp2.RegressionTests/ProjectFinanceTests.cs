using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [Test, Category("Finance")]
    public async Task AdvanceAccrualAndOtherCostAreNotCountedTwiceAndReversalsPreserveHistory()
    {
        using var reason = AuditContext.UseReason("Sentetik finans belgesi");
        var finance = new ProjectFinanceService(connection, admin);
        Guid project = await finance.CreateProjectAsync("SITE-01", "Synthetic site");
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Guid person = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan");
        var advance = await Service.PrepareAdvanceAsync(PersonnelGroup.General, Person().Tc, 150m, await VersionAsync(), project, default);
        await Service.SubmitPaymentAsync(advance, default);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(-50m));
        var document = new FinanceDocument(project, "Accrual", "EARN-001", person, PersonnelGroup.General, Person().Tc, await VersionAsync(),
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), 200m, "Synthetic accrual");
        Guid entry = await finance.PostAsync(document);
        Assert.That(await finance.PostAsync(document), Is.EqualTo(entry));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(150m));
        await finance.PostAsync(new(project, "Expense", "EXP-001", null, null, null, null, document.Start, document.End, 30m, "Synthetic supplies"));
        var report = await finance.ProjectsAsync(); var row = report.Rows[0];
        Assert.That(row.Field<decimal>("LaborCost"), Is.EqualTo(200m)); Assert.That(row.Field<decimal>("OtherCost"), Is.EqualTo(30m));
        Assert.That(row.Field<decimal>("TotalCost"), Is.EqualTo(230m)); Assert.That(row.Field<decimal>("AdvanceCash"), Is.EqualTo(150m));
        await Service.SubmitPaymentAsync(advance, default);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(150m));
        await Service.ReversePaymentAsync(advance.RequestId);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(300m));
        var entries = await finance.EntriesAsync(project); var earned = entries.AsEnumerable().Single(r => r.Field<Guid>("EntryId") == entry);
        await finance.ReverseAsync(entry, (byte[])earned["RecordVersion"]);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        report = await finance.ProjectsAsync(); Assert.That(report.Rows[0].Field<decimal>("TotalCost"), Is.EqualTo(30m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.FinanceEntries"), Is.EqualTo(2));
    }

    [Test, Category("Finance")]
    public async Task FinanceReferenceCollisionAndStalePersonnelDoNotChangeBalance()
    {
        var finance = new ProjectFinanceService(connection, admin); Guid project = await finance.CreateProjectAsync("SITE-02", "Synthetic");
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Guid person = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan"); byte[] stale = await VersionAsync();
        await Service.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Changed" }, false, expectedVersion: stale);
        var document = new FinanceDocument(project, "Accrual", "DOC-1", person, PersonnelGroup.General, Person().Tc, stale,
            DateTime.Today, DateTime.Today, 20m, "Synthetic");
        await Assert.ThrowsAsync<InvalidOperationException>(() => finance.PostAsync(document));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        document = document with { PersonVersion = await VersionAsync() }; await finance.PostAsync(document);
        await Assert.ThrowsAsync<InvalidOperationException>(() => finance.PostAsync(document with { Amount = 25m }));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(120m));
    }

    [Test, Category("Finance")]
    public async Task FinanceAuditFailureRollsBackEntryBalanceAndLedger()
    {
        var finance = new ProjectFinanceService(connection, admin); Guid project = await finance.CreateProjectAsync("SITE-03", "Synthetic");
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Guid person = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan");
        var document = new FinanceDocument(project, "Accrual", "DOC-2", person, PersonnelGroup.General, Person().Tc, await VersionAsync(),
            DateTime.Today, DateTime.Today, 20m, "Synthetic");
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE EntityType=N'Finance.Entry') THROW 51099,N'Synthetic failure',1;");
        await Assert.ThrowsAsync<SqlException>(() => finance.PostAsync(document));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.FinanceEntries"), Is.Zero);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE FinanceEntryId IS NOT NULL"), Is.Zero);
    }
}
