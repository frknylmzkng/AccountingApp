using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Roles")]
public class AccountSelectionTests
{
    private static Task Act(AccountService service, UserSession user, AccountTarget? target, int operation) => operation switch
    { 0 => service.DeleteAsync(user, target, default), 1 => service.SetActiveAsync(user, target, false, default), _ => service.SetActiveAsync(user, target, true, default) };
    [TestCase(-1, 0), TestCase(0, 0), TestCase(7, 0), TestCase(9, 0)]
    [TestCase(-1, 1), TestCase(0, 1), TestCase(7, 1), TestCase(9, 1)]
    [TestCase(-1, 2), TestCase(0, 2), TestCase(7, 2), TestCase(9, 2)]
    public async Task InvalidAccountVersionIsRejectedBeforeDatabaseAccess(int length, int operation)
    {
        byte[]? version = length < 0 ? null : new byte[length];
        var target = new AccountTarget("target", version!, Guid.NewGuid());
        var error = await Assert.ThrowsAsync<ArgumentException>(() => Act(new("invalid connection string"), new("admin", UserRole.Admin), target, operation));
        Assert.That(error!.Message, Is.EqualTo(AccountTarget.SelectionMessage));
    }
    [Test]
    public async Task MissingOrMalformedTargetIdentityIsRejectedBeforeDatabaseAccess()
    {
        AccountTarget?[] invalid = [null, new(" ", new byte[8], Guid.NewGuid()), new(new string('x', 51), new byte[8], Guid.NewGuid()), new("target", new byte[8], Guid.Empty)];
        foreach (var target in invalid) foreach (int operation in new[] { 0, 1, 2 })
            await Assert.ThrowsAsync<ArgumentException>(() => Act(new("invalid connection string"), new("admin", UserRole.Admin), target, operation));
    }
    [Test]
    public void SortedSelectionKeepsOriginalAccountIdentityAndDefensiveVersionCopy()
    {
        var table = new DataTable(); table.Columns.Add("username"); table.Columns.Add(AccountTarget.VersionColumn, typeof(byte[])); table.Columns.Add(AccountTarget.EpochColumn, typeof(Guid));
        byte[] first = [0,0,0,0,0,0,0,1], second = [0,0,0,0,0,0,0,2]; Guid epoch = Guid.NewGuid();
        table.Rows.Add("alpha", first, Guid.NewGuid()); table.Rows.Add("beta", second, epoch); table.AcceptChanges();
        using var grid = new DataGridView { BindingContext = new BindingContext() }; RecordSelection.Configure(grid); grid.DataSource = table;
        table.DefaultView.Sort = "username DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        table.Rows[1]["username"] = "changed-display"; table.Rows[1][AccountTarget.VersionColumn] = first; table.Rows[1][AccountTarget.EpochColumn] = Guid.NewGuid();
        var target = AccountTarget.FromGrid(grid);
        Assert.That(target.Username, Is.EqualTo("beta")); Assert.That(target.Epoch, Is.EqualTo(epoch)); Assert.That(target.Version, Is.EqualTo(second));
        target.Version[0] = 99; Assert.That(AccountTarget.FromGrid(grid).Version, Is.EqualTo(second));
        RecordSelection.HideInternalColumns(grid); Assert.That(grid.Columns[AccountTarget.VersionColumn]!.Visible, Is.False); Assert.That(grid.Columns[AccountTarget.EpochColumn]!.Visible, Is.False);
    }
}

public partial class GuideDatabaseTests
{
    [Test, Category("Roles")]
    public async Task LegacyAccountDeleteUsesTheSameSelfProtection()
    {
        var target = await TargetAsync(admin.Username);
        Assert.Throws<InvalidOperationException>(() => new AdminStore(connection).Delete(admin, target));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.Zero);
    }
    [Test, Category("Roles")]
    public async Task ConcurrentLegacyAccountDeletesKeepOneActiveAdministrator()
    {
        var other = CreateActor("second-admin", UserRole.Admin);
        var firstTarget = await TargetAsync(admin.Username); var otherTarget = await TargetAsync(other.Username);
        bool Try(UserSession actor, AccountTarget target)
        {
            try { new AdminStore(connection).Delete(actor, target); return true; }
            catch (SessionRevokedException) { return false; }
        }
        bool[] results = await Task.WhenAll(Task.Run(() => Try(admin, otherTarget)), Task.Run(() => Try(other, firstTarget)));
        Assert.That(results.Count(won => won), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE IsActive=1 AND RoleName=N'Admin'"), Is.EqualTo(1));
    }
    private Task ChangeSelectedAccount(AccountTarget target, int operation, UserSession? actor = null) => operation switch
    { 0 => Accounts.DeleteAsync(actor ?? admin, target, default), 1 => Accounts.SetActiveAsync(actor ?? admin, target, false, default), _ => Accounts.SetActiveAsync(actor ?? admin, target, true, default) };
    private void SeedTargetThrottle(AccountTarget target) => Execute($"INSERT dbo.LoginThrottle(AccountEpoch,FailureCount,WindowStartedUtc) VALUES('{target.Epoch:D}',2,SYSUTCDATETIME())");
    private async Task AssertTargetUnchanged(AccountTarget target, bool active, int auditCount)
    {
        var latest = await TargetAsync(target.Username);
        Assert.That(latest.Version, Is.EqualTo(target.Version)); Assert.That(latest.Epoch, Is.EqualTo(target.Epoch));
        using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand("SELECT IsActive FROM dbo.admin WHERE username=@name", c); cmd.Text("@name", target.Username);
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(active)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(auditCount)); Assert.That(FailureCount, Is.EqualTo(2));
    }

    [TestCase(0, 0), TestCase(1, 0), TestCase(2, 0)]
    [TestCase(0, 1), TestCase(1, 1), TestCase(2, 1)]
    [TestCase(0, 2), TestCase(1, 2), TestCase(2, 2)]
    [Category("Roles")]
    public async Task RecreatedAccountRequiresBothCurrentVersionAndEpoch(int operation, int stalePart)
    {
        var actor = CreateActor("same-name", UserRole.Viewer);
        var stale = await TargetAsync(actor.Username);
        await Accounts.DeleteAsync(admin, stale, default); CreateActor(actor.Username, UserRole.PersonnelManager);
        if (operation == 2) await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var current = await TargetAsync(actor.Username); SeedTargetThrottle(current);
        var submitted = stalePart switch { 1 => current with { Epoch = stale.Epoch }, 2 => current with { Version = stale.Version }, _ => stale };
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ChangeSelectedAccount(submitted, operation));

        Assert.That(error!.Message, Is.EqualTo(AccountTarget.ConflictMessage));
        await AssertTargetUnchanged(current, operation != 2, audits);
        Assert.That(Scalar("SELECT RoleName FROM dbo.admin WHERE username=N'same-name'"), Is.EqualTo("PersonnelManager"));
    }

    [TestCase(0, "Role"), TestCase(1, "Role"), TestCase(2, "Role")]
    [TestCase(0, "State"), TestCase(1, "State"), TestCase(2, "State")]
    [TestCase(0, "Password"), TestCase(1, "Password"), TestCase(2, "Password")]
    [Category("Roles")]
    public async Task OldSelectionCannotManageAnInterveningAccountChange(int operation, string change)
    {
        var actor = CreateActor("changed-target", UserRole.PersonnelManager);
        bool initiallyActive = operation != 2;
        if (!initiallyActive) await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var stale = await TargetAsync(actor.Username); bool currentActive = initiallyActive;
        if (change == "Role") Execute("UPDATE dbo.admin SET RoleName=N'Viewer' WHERE username=N'changed-target'");
        else if (change == "State") { currentActive = !initiallyActive; await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), currentActive, default); }
        else
        {
            // A password reset requires an active account; it also changes the version and epoch.
            if (!initiallyActive) { await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), true, default); currentActive = true; }
            await Accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), KnownPassword);
        }
        var current = await TargetAsync(actor.Username); SeedTargetThrottle(current);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ChangeSelectedAccount(stale, operation));
        Assert.That(error!.Message, Is.EqualTo(AccountTarget.ConflictMessage));
        await AssertTargetUnchanged(current, currentActive, audits);
    }

    [TestCase(0), TestCase(1), TestCase(2), Category("Roles")]
    public async Task MissingAccountCannotBeManagedUsingADeletedSelection(int operation)
    {
        CreateActor("deleted-target", UserRole.Viewer); var stale = await TargetAsync("deleted-target");
        await Accounts.DeleteAsync(admin, stale, default); int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => ChangeSelectedAccount(stale, operation));
        Assert.That(error!.Message, Is.EqualTo(AccountTarget.ConflictMessage)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits));
    }

    [TestCase(0), TestCase(1), TestCase(2), Category("Roles")]
    public async Task CurrentAccountTargetCommitsOnlyTheRequestedChange(int operation)
    {
        var actor = CreateActor("current-target", UserRole.Viewer);
        if (operation == 2) await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var target = await TargetAsync(actor.Username); SeedTargetThrottle(target);
        await ChangeSelectedAccount(target, operation);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        if (operation == 0) { Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE username=N'current-target'"), Is.Zero); Assert.That(FailureCount, Is.Zero); }
        else { Assert.That(Scalar("SELECT IsActive FROM dbo.admin WHERE username=N'current-target'"), Is.EqualTo(operation == 2)); Assert.That(FailureCount, Is.EqualTo(2)); }
        string action = operation switch { 0 => "Account.Delete", 1 => "Account.Deactivate", _ => "Account.Activate" };
        Assert.That(Scalar($"SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'{action}'"), Is.EqualTo(1));
    }

    [TestCase(0), TestCase(1), TestCase(2), Category("Roles")]
    public async Task AccountManagementAuditFailureRollsBackStatusDeletionAndThrottle(int operation)
    {
        var actor = CreateActor("rollback-target", UserRole.Viewer);
        if (operation == 2) await Accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var target = await TargetAsync(actor.Username); SeedTargetThrottle(target);
        int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action IN(N'Account.Delete',N'Account.Deactivate',N'Account.Activate')) THROW 51099,N'Test account audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(() => ChangeSelectedAccount(target, operation));
        await AssertTargetUnchanged(target, operation != 2, audits);
    }

    [Test, Category("Roles")]
    public async Task ConcurrentDeleteAndDeactivateOnTheSameSelectionHaveOneWinner()
    {
        CreateActor("race-target", UserRole.Viewer); var target = await TargetAsync("race-target");
        var other = CreateActor("other-admin", UserRole.Admin); int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        static async Task<bool> Try(Task action) { try { await action; return true; } catch (InvalidOperationException) { return false; } }
        bool[] results = await Task.WhenAll(Try(ChangeSelectedAccount(target, 0)), Try(ChangeSelectedAccount(target, 1, other)));
        Assert.That(results.Count(won => won), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE username=N'race-target'"), Is.EqualTo(results[0] ? 0 : 1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE username=N'race-target' AND IsActive=1"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audits + 1));
    }
}
