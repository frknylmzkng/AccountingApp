using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using Wire = AccountingApp.Contracts;

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
        var workforce = new ProjectWorkforceService(connection, admin);
        await workforce.AssignAsync(new(project, (byte[])Scalar("SELECT RecordVersion FROM dbo.FinanceProjects"), null, null,
            person, Wire.PersonnelGroup.General, Person().Tc, await VersionAsync(),
            new DateTime(2000, 1, 1), null, "Sentetik proje ataması"), default);
        var preparer = new ProjectPaymentService(connection, admin);
        Guid advanceId = Guid.NewGuid();
        await preparer.PrepareAsync(new(advanceId, project, person, Wire.PersonnelGroup.General,
            await VersionAsync(), 150m, Wire.PaymentPurpose.Advance, "Sentetik proje avansı"), default);
        var approver = new ProjectPaymentService(connection, CreateActor("finance-approver", UserRole.Admin));
        var decision = new Wire.ProjectPaymentDecide(project, advanceId,
            (byte[])Scalar("SELECT RecordVersion FROM dbo.ProjectPaymentProposals"), true, "Sentetik avans onayı");
        await approver.DecideAsync(decision, default);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(-50m));
        var document = new FinanceDocument(project, "Accrual", "EARN-001", person, PersonnelGroup.General, Person().Tc, await VersionAsync(),
            new DateTime(2026, 10, 1), new DateTime(2026, 10, 31), 200m, "Synthetic accrual");
        Guid entry = await finance.PostAsync(document);
        Assert.That(await finance.PostAsync(document), Is.EqualTo(entry));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(150m));
        await finance.PostAsync(new(project, "Expense", "EXP-001", null, null, null, null, document.Start, document.End, 30m, "Synthetic supplies"));
        Assert.That(Scalar("SELECT EffectiveAmount FROM dbo.FinanceEffectiveEntries WHERE Reference=N'EARN-001'"), Is.EqualTo(200m));
        var report = await finance.ProjectsAsync();
        var row = report.AsEnumerable().Single(item => item.Field<Guid>("ProjectId") == project);
        Assert.That(row.Field<decimal>("LaborCost"), Is.EqualTo(200m)); Assert.That(row.Field<decimal>("OtherCost"), Is.EqualTo(30m));
        Assert.That(row.Field<decimal>("TotalCost"), Is.EqualTo(230m)); Assert.That(row.Field<decimal>("AdvanceCash"), Is.EqualTo(150m));
        await approver.DecideAsync(decision, default);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(150m));
        await Service.ReversePaymentAsync(advanceId);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(300m));
        var entries = await finance.EntriesAsync(project); var earned = entries.AsEnumerable().Single(r => r.Field<Guid>("EntryId") == entry);
        await finance.ReverseAsync(entry, (byte[])earned["RecordVersion"]);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        report = await finance.ProjectsAsync();
        Assert.That(report.AsEnumerable().Single(item => item.Field<Guid>("ProjectId") == project).Field<decimal>("TotalCost"), Is.EqualTo(30m));
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
