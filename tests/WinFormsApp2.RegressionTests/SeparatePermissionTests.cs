using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("Roles")]
public class SeparatePermissionPolicyTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void PaymentAndExportGrantsAreIndependentOfPersonnelEditing(UserRole role)
    {
        foreach (bool pay in new[] { false, true }) foreach (bool export in new[] { false, true }) foreach (bool salary in new[] { false, true })
        {
            var user = new UserSession("sample", role) { PaymentGranted = pay, PersonalDataExportGranted = export, SalaryViewGranted = salary };
            Assert.That(user.CanEdit, Is.EqualTo(role != UserRole.Viewer));
            Assert.That(user.CanPay, Is.EqualTo(role == UserRole.Admin || (pay && salary)));
            Assert.That(user.CanExportPersonalData, Is.EqualTo(role == UserRole.Admin || export));
            var temporary = user with { MustChangePassword = true };
            Assert.That(temporary.CanEdit || temporary.CanPay || temporary.CanExportPersonalData, Is.False);
        }
    }
    [TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public async Task UngrantedPaymentExportAndPrintingAreRejectedBeforeDatabaseOrFiles(UserRole role)
    {
        var user = new UserSession("sample", role); var people = new PersonnelService("invalid", user);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => people.PayAsync(PersonnelGroup.General, 11111111111L, 1m, Guid.NewGuid(), expectedVersion: new byte[8]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => people.PreparePaymentAsync(PersonnelGroup.General, 11111111111L, 1m, new byte[8], default));
        using var data = new DataTable();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new PersonalDataExportService("invalid", user).ExportAsync(data, "not-created.csv", false));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new PaymentReportService("invalid", user).ReceiptAsync(Guid.NewGuid()));
    }
}

public partial class GuideDatabaseTests
{
    [Test, Category("Roles")]
    public async Task PersonnelEditorWithoutPaymentGrantCanEditNamesButNotBalancesOrPay()
    {
        var actor = CreateActor("personnel-only", UserRole.PersonnelManager, canViewIban: true, canViewSalary: true); var service = new PersonnelService(connection, actor);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        await service.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Updated" }, false, expectedVersion: await VersionAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await service.SaveAsync(PersonnelGroup.General, Person(amount: 1m), false, expectedVersion: await VersionAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await service.PayCurrentAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.SaveAsync(PersonnelGroup.Joker, Person(tc: 22222222222L), true, employmentStart: new DateTime(2000, 1, 1)));
        await service.SaveAsync(PersonnelGroup.Joker, Person(tc: 22222222222L, amount: 0m), true, employmentStart: new DateTime(2000, 1, 1));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT AD FROM dbo.Calisan"), Is.EqualTo("Updated"));
    }

    [Test, Category("Roles")]
    public async Task PaymentOnlyViewerCanPayWithoutEditingPeopleOrExporting()
    {
        var actor = CreateActor("payment-only", UserRole.Viewer, canPay: true, canViewSalary: true); var service = new PersonnelService(connection, actor);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        await service.PayCurrentAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await service.SaveAsync(PersonnelGroup.General, Person(), false, expectedVersion: await VersionAsync()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new PaymentReportService(connection, actor).ReceiptAsync(Guid.NewGuid()));
    }

    [Test, Category("Roles")]
    public async Task PermissionChangesInvalidateOldSessionsAndStaleTargetCannotModifyGrants()
    {
        var old = CreateActor("grant-target", UserRole.Viewer); var target = await TargetAsync(old.Username);
        var accounts = new AccountService(connection);
        await accounts.SetPermissionsAsync(admin, target, true, false, canViewSalary: true, canViewIban: null);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, old, default), Is.False);
        var current = SessionFor(old.Username); Assert.That(current.CanPay, Is.True); Assert.That(current.CanEdit || current.CanExportPersonalData, Is.False);
        await Assert.ThrowsAsync<InvalidOperationException>(() => accounts.SetPermissionsAsync(admin, target, false, true));
        var nextTarget = await TargetAsync(old.Username); await accounts.SetPermissionsAsync(admin, nextTarget, false, true);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, current, default), Is.False);
        current = SessionFor(old.Username); Assert.That(current.CanExportPersonalData, Is.True); Assert.That(current.CanPay || current.CanEdit, Is.False);
        var forged = current with { PaymentGranted = true };
        await Assert.ThrowsAsync<SessionRevokedException>(() => AccountAuthorization.ValidateAsync(connection, forged, AccountAccess.Pay, default));
    }

    [Test, Category("Roles")]
    public async Task ExportOnlyViewerCanExportButRevokedAndForgedGrantsCreateNoFile()
    {
        var actor = CreateActor("export-only", UserRole.Viewer, canExport: true, canViewSalary: true, canViewIban: true);
        using var data = new DataTable(); data.Columns.Add("Ad"); data.Rows.Add("Synthetic");
        string path = Path.Combine(directory, "authorized.csv");
        await new PersonalDataExportService(connection, actor).ExportAsync(data, path, false);
        Assert.That(File.Exists(path), Is.True);
        await new AccountService(connection).SetPermissionsAsync(admin, await TargetAsync(actor.Username), false, false);
        string denied = Path.Combine(directory, "denied.csv");
        await Assert.ThrowsAsync<SessionRevokedException>(() => new PersonalDataExportService(connection, actor).ExportAsync(data, denied, false));
        var forged = SessionFor(actor.Username) with { PersonalDataExportGranted = true };
        await Assert.ThrowsAsync<SessionRevokedException>(() => new PersonalDataExportService(connection, forged).ExportAsync(data, denied, false));
        Assert.That(File.Exists(denied), Is.False);
    }

    [Test, Category("Roles")]
    public async Task PermissionAuditFailureRollsBackGrantsAndSessionVersion()
    {
        var actor = CreateActor("grant-rollback", UserRole.PersonnelManager);
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Account.PermissionsChange') THROW 51099,N'Permission audit rollback',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await new AccountService(connection).SetPermissionsAsync(admin, await TargetAsync(actor.Username), true, true, canViewSalary: true, canViewIban: null));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.True);
        Assert.That(SessionFor(actor.Username).CanPay || SessionFor(actor.Username).CanExportPersonalData, Is.False);
    }
}
