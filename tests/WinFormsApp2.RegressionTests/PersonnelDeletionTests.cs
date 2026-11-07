using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task StaleDeleteCannotRemoveReactivatedPersonOrItsLeaves(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1)); byte[] staleVersion = await VersionAsync(group);
        await Service.DeleteAsync(group, Person().Tc, staleVersion, employmentEnd: new DateTime(2099, 12, 31));
        await ReactivateAndEditAsync(group, Person() with { FirstName = "Yeni", Amount = 250m });
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        byte[] currentVersion = await VersionAsync(group);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, staleVersion, employmentEnd: new DateTime(2099, 12, 31)));

        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(Scalar($"SELECT AD FROM {PersonnelRepository.Table(group)}"), Is.EqualTo("Yeni"));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(250m));
        Assert.That(await VersionAsync(group), Is.EqualTo(currentVersion));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
    }

    [TestCase(0, "Name")]
    [TestCase(1, "Name")]
    [TestCase(0, "Iban")]
    [TestCase(1, "Iban")]
    [TestCase(0, "Payment")]
    [TestCase(1, "Payment")]
    [Category("Personnel")]
    public async Task StaleDeleteCannotRemoveAnotherUsersUpdatedRecord(int groupId, string change)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        byte[] staleVersion = await VersionAsync(group);
        var other = new PersonnelService(connection, CreateActor("second-user", UserRole.PersonnelManager, canPay: true));
        if (change == "Payment") await other.PayCurrentAsync(group, Person().Tc, 30m, Guid.NewGuid());
        else await other.SaveAsync(group, change == "Name" ? Person() with { FirstName = "Yeni" } : Person() with { Iban = "TR330006100519786457841326" }, false, expectedVersion: staleVersion);
        byte[] latestVersion = await VersionAsync(group);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, staleVersion, employmentEnd: new DateTime(2099, 12, 31)));

        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(await VersionAsync(group), Is.EqualTo(latestVersion));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Payment" ? 70m : 100m));
        Assert.That(Scalar($"SELECT AD FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Name" ? "Yeni" : "Ada"));
        Assert.That(Scalar($"SELECT IBAN FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Iban" ? "TR330006100519786457841326" : Person().Iban));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task CurrentVersionArchivePreservesBalanceAndLeavesAtomically(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.PayCurrentAsync(group, Person().Tc, 30m, Guid.NewGuid());
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group), employmentEnd: new DateTime(2099, 12, 31));
        Assert.That(Scalar($"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)} WHERE ArchivedUtc IS NOT NULL"), Is.EqualTo(1));
        Assert.That((await Service.ListAsync(group)).Rows.Count, Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(70m));
        Assert.That(Scalar("SELECT Delta FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(0m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Archive'"), Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task DeleteAuditFailureRestoresPersonLeavesAndLedger(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        byte[] version = await VersionAsync(group);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Person.Archive') THROW 51099,N'Test delete audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await Service.DeleteAsync(group, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31)));
        Assert.That(await VersionAsync(group), Is.EqualTo(version));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task AlreadyDeletedSelectionCannotWriteAnotherClosure(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1)); byte[] version = await VersionAsync(group);
        await Service.DeleteAsync(group, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31)));
        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Archive'"), Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task SimultaneousEditAndDeleteUsingSameVersionHaveOnlyOneWinner(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1)); byte[] version = await VersionAsync(group);
        var other = new PersonnelService(connection, CreateActor("second-user", UserRole.PersonnelManager, canPay: true));
        static async Task<bool> Try(Task task) { try { await task; return true; } catch (InvalidOperationException) { return false; } }
        bool[] results = await Task.WhenAll(Try(Service.DeleteAsync(group, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31))), Try(other.SaveAsync(group, Person(amount: 125m), false, expectedVersion: version)));
        Assert.That(results.Count(won => won), Is.EqualTo(1));
        Assert.That(Scalar($"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)} WHERE ArchivedUtc IS NULL"), Is.EqualTo(results[0] ? 0 : 1));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(results[0] ? 100m : 125m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(results[0] ? 1 : 0));
    }
}
