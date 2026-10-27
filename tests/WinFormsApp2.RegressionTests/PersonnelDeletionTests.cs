using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task StaleDeleteCannotRemoveRecreatedPersonOrItsLeaves(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] staleVersion = await VersionAsync(group);
        await Service.DeleteAsync(group, Person().Tc, staleVersion);
        await Service.SaveAsync(group, Person() with { FirstName = "Yeni", Amount = 250m }, true);
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        byte[] currentVersion = await VersionAsync(group);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, staleVersion));

        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(Scalar($"SELECT AD FROM {PersonnelRepository.Table(group)}"), Is.EqualTo("Yeni"));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(250m));
        Assert.That(await VersionAsync(group), Is.EqualTo(currentVersion));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits));
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
        await Service.SaveAsync(group, Person(), true);
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        byte[] staleVersion = await VersionAsync(group);
        var other = new PersonnelService(connection, CreateActor("second-user", UserRole.PersonnelManager));
        if (change == "Payment") await other.PayAsync(group, Person().Tc, 30m, Guid.NewGuid());
        else await other.SaveAsync(group, change == "Name" ? Person() with { FirstName = "Yeni" } : Person() with { Iban = "TR111111111111111111111111" }, false, expectedVersion: staleVersion);
        byte[] latestVersion = await VersionAsync(group);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, staleVersion));

        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(await VersionAsync(group), Is.EqualTo(latestVersion));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Payment" ? 70m : 100m));
        Assert.That(Scalar($"SELECT AD FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Name" ? "Yeni" : "Ada"));
        Assert.That(Scalar($"SELECT IBAN FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(change == "Iban" ? "TR111111111111111111111111" : Person().Iban));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task CurrentVersionDeleteClosesBalanceAndRemovesLeavesAtomically(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true);
        await Service.PayAsync(group, Person().Tc, 30m, Guid.NewGuid());
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group));
        Assert.That(Scalar($"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)}"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(0m));
        Assert.That(Scalar("SELECT Delta FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(-70m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Delete'"), Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task DeleteAuditFailureRestoresPersonLeavesAndLedger(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true);
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 10, 1), new(2026, 10, 3)))]);
        byte[] version = await VersionAsync(group);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Person.Delete') THROW 51099,N'Test delete audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await Service.DeleteAsync(group, Person().Tc, version));
        Assert.That(await VersionAsync(group), Is.EqualTo(version));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task AlreadyDeletedSelectionCannotWriteAnotherClosure(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] version = await VersionAsync(group);
        await Service.DeleteAsync(group, Person().Tc, version);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.DeleteAsync(group, Person().Tc, version));
        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Delete'"), Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task SimultaneousEditAndDeleteUsingSameVersionHaveOnlyOneWinner(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] version = await VersionAsync(group);
        var other = new PersonnelService(connection, CreateActor("second-user", UserRole.PersonnelManager));
        static async Task<bool> Try(Task task) { try { await task; return true; } catch (InvalidOperationException) { return false; } }
        bool[] results = await Task.WhenAll(Try(Service.DeleteAsync(group, Person().Tc, version)), Try(other.SaveAsync(group, Person(amount: 125m), false, expectedVersion: version)));
        Assert.That(results.Count(won => won), Is.EqualTo(1));
        Assert.That(Scalar($"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(results[0] ? 0 : 1));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(results[0] ? 0m : 125m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.EqualTo(results[0] ? 1 : 0));
    }
}
