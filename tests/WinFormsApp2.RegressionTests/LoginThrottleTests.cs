using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    private AccountService Accounts => new(connection);
    private const string KnownPassword = "Test-only-password-123";
    private async Task FailLogins(string username, int count)
    {
        for (int i = 0; i < count; i++) Assert.That(await Accounts.AuthenticateAsync(username, "Wrong test password!"), Is.Null);
    }
    private int FailureCount => Convert.ToInt32(Scalar("SELECT COALESCE(MAX(FailureCount),0) FROM dbo.LoginThrottle"));

    [Test, Category("LoginThrottle"), Category("Migrations")]
    public async Task ThrottleUpgradePreservesAccountsSessionsAndBusinessRows()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var original = SessionFor(admin.Username);
        object originalHash = Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'test-admin'");
        Execute("DROP TABLE dbo.LoginThrottle; DELETE FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%104-login-throttle.sql:%'");
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: Path.Combine(directory, "throttle-upgrade"));
        Assert.That(Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'test-admin'"), Is.EqualTo(originalHash));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, original, default), Is.True);
        Assert.That((await Service.ListAsync(PersonnelGroup.General)).Rows.Count, Is.EqualTo(1));
        await FailLogins(admin.Username, 2);
        await SchemaUpgrade.PrepareAsync(connection);
        Assert.That(FailureCount, Is.EqualTo(2));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"), Is.EqualTo(6));
    }

    [Test, Category("LoginThrottle")]
    public async Task FifthFailureBlocksEvenCorrectPasswordWithoutChangingSessionVersion()
    {
        var actor = CreateActor("throttle-user", UserRole.Viewer);
        var version = actor.SecurityVersion!.ToArray();
        await FailLogins(actor.Username, 5);
        Assert.That(FailureCount, Is.EqualTo(5));
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, KnownPassword), Is.Null);
        Assert.That(Scalar("SELECT CASE WHEN LockedUntilUtc>DATEADD(minute,4,SYSUTCDATETIME()) AND LockedUntilUtc<=DATEADD(minute,5,SYSUTCDATETIME()) THEN 1 ELSE 0 END FROM dbo.LoginThrottle"), Is.EqualTo(1));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.True);
        Assert.That(SessionFor(actor.Username).SecurityVersion, Is.EqualTo(version));
        await Assert.DoesNotThrowAsync(async () => await new PersonnelService(connection, actor).ListAsync(PersonnelGroup.General));
    }

    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer), Category("LoginThrottle")]
    public async Task SuccessfulLoginBeforeLimitClearsFailuresForEveryRole(UserRole role)
    {
        var actor = CreateActor("throttle-user", role);
        await FailLogins(actor.Username, 4);
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, KnownPassword), Is.Not.Null);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LoginThrottle"), Is.Zero);
        await FailLogins(actor.Username, 1); Assert.That(FailureCount, Is.EqualTo(1));
    }

    [Test, Category("LoginThrottle")]
    public async Task CounterSurvivesServiceRecreationAndCanonicalUsernameVariants()
    {
        CreateActor("throttle-user", UserRole.Viewer);
        await FailLogins("throttle-user", 2);
        AppDatabase.Reset();
        await FailLogins("  THROTTLE-USER  ", 3);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LoginThrottle"), Is.EqualTo(1));
        Assert.That(await Accounts.AuthenticateAsync("throttle-user", KnownPassword), Is.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task LockedAttemptsDoNotExtendDeadlineOrBlockOtherAccounts()
    {
        CreateActor("throttle-user", UserRole.Viewer);
        await FailLogins("throttle-user", 5);
        object deadline = Scalar("SELECT LockedUntilUtc FROM dbo.LoginThrottle");
        await FailLogins("throttle-user", 2);
        Assert.That(await Accounts.AuthenticateAsync("throttle-user", KnownPassword), Is.Null);
        Assert.That(Scalar("SELECT LockedUntilUtc FROM dbo.LoginThrottle"), Is.EqualTo(deadline));
        Assert.That(FailureCount, Is.EqualTo(5));
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Not.Null);
    }

    [TestCase(true), TestCase(false), Category("LoginThrottle")]
    public async Task ExpiredLockStartsFreshAndAcceptsCorrectPassword(bool correct)
    {
        await FailLogins(admin.Username, 5);
        Execute("UPDATE dbo.LoginThrottle SET LockedUntilUtc=DATEADD(second,-1,SYSUTCDATETIME())");
        if (correct)
        {
            Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Not.Null);
            Assert.That(FailureCount, Is.Zero);
        }
        else { await FailLogins(admin.Username, 1); Assert.That(FailureCount, Is.EqualTo(1)); }
    }

    [Test, Category("LoginThrottle")]
    public async Task FifteenMinuteObservationWindowStartsNewCount()
    {
        await FailLogins(admin.Username, 4);
        Execute("UPDATE dbo.LoginThrottle SET WindowStartedUtc=DATEADD(minute,-16,SYSUTCDATETIME())");
        await FailLogins(admin.Username, 1);
        Assert.That(FailureCount, Is.EqualTo(1));
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Not.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task ConcurrentFailuresAcrossInstancesStopAtFive()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => new AccountService(connection).AuthenticateAsync(admin.Username, "Wrong test password!")));
        Assert.That(results.All(r => r == null), Is.True);
        Assert.That(FailureCount, Is.EqualTo(5));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LoginThrottle"), Is.EqualTo(1));
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task LegacyBooleanAuthenticationCannotBypassThrottle()
    {
        var legacy = new AdminStore(connection);
        for (int i = 0; i < 5; i++) Assert.That(legacy.Authenticate(admin.Username, "Wrong test password!"), Is.False);
        Assert.That(legacy.Authenticate(admin.Username, KnownPassword), Is.False);
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Null);
        Assert.That(FailureCount, Is.EqualTo(5));
    }

    [Test, Category("LoginThrottle")]
    public async Task UnknownInactiveAndExpiredResetLoginsDoNotCreateThrottleRows()
    {
        await FailLogins("missing-account", 6);
        var inactive = CreateActor("inactive-user", UserRole.Viewer);
        await Accounts.SetActiveAsync(admin, await TargetAsync(inactive.Username), false, default);
        await FailLogins(inactive.Username, 5);
        var actor = CreateActor("reset-user", UserRole.Viewer);
        var reset = await Accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), KnownPassword);
        Execute("UPDATE dbo.admin SET PasswordResetExpiresUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE username=N'reset-user'");
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, reset.TemporaryPassword), Is.Null);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LoginThrottle"), Is.Zero);
    }

    [Test, Category("LoginThrottle")]
    public async Task PasswordReauthenticationUsesSameBudgetAndLockAsLogin()
    {
        var actor = CreateActor("throttle-user", UserRole.Viewer);
        await FailLogins(actor.Username, 4);
        const string next = "A different password phrase";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Accounts.ChangePasswordAsync(actor, "Wrong test password!", next, next));
        Assert.That(FailureCount, Is.EqualTo(5));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Accounts.ChangePasswordAsync(actor, KnownPassword, next, next));
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, KnownPassword), Is.Null);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.True);
    }

    [Test, Category("LoginThrottle")]
    public async Task AdminResetReauthenticationAlsoLocksWithoutChangingTarget()
    {
        var target = CreateActor("reset-user", UserRole.Viewer);
        var selected = await TargetAsync(target.Username);
        for (int i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Accounts.ResetPasswordAsync(admin, selected, "Wrong admin password!"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await Accounts.ResetPasswordAsync(admin, selected, KnownPassword));
        Assert.That(SessionFor(target.Username).SecurityVersion, Is.EqualTo(target.SecurityVersion));
        Assert.That(await Accounts.AuthenticateAsync(target.Username, KnownPassword), Is.Not.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task AdminResetClearsLockedTargetAndNewTemporaryPasswordCanLogin()
    {
        var actor = CreateActor("reset-user", UserRole.Viewer);
        await FailLogins(actor.Username, 5);
        var reset = await Accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), KnownPassword);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LoginThrottle"), Is.Zero);
        var restricted = await Accounts.AuthenticateAsync(actor.Username, reset.TemporaryPassword);
        Assert.That(restricted, Is.Not.Null); Assert.That(restricted!.MustChangePassword, Is.True);
        await FailLogins(actor.Username, 5);
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, reset.TemporaryPassword), Is.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task DeletedAndRecreatedAccountDoesNotInheritOldFailures()
    {
        var actor = CreateActor("replace-user", UserRole.Viewer);
        await FailLogins(actor.Username, 5);
        await Accounts.DeleteAsync(admin, await TargetAsync(actor.Username), default);
        Assert.That(FailureCount, Is.Zero);
        await Accounts.AddAsync(admin, actor.Username, KnownPassword, UserRole.Viewer, default);
        Assert.That(await Accounts.AuthenticateAsync(actor.Username, KnownPassword), Is.Not.Null);
    }

    [Test, Category("LoginThrottle")]
    public async Task LoginAuditFailureRollsBackSuccessfulCounterClear()
    {
        await FailLogins(admin.Username, 3);
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS THROW 51090,N'Test audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await Accounts.AuthenticateAsync(admin.Username, KnownPassword));
        Assert.That(FailureCount, Is.EqualTo(3));
        Execute("DROP TRIGGER dbo.GuideBlockAudit");
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Not.Null);
        Assert.That(FailureCount, Is.Zero);
    }

    [Test, Category("LoginThrottle")]
    public async Task CancelledGateWaitDoesNotLeavePooledConnectionLocked()
    {
        var gate = await LoginAttemptGate.OpenAsync(connection, admin.SecurityEpoch, default);
        try
        {
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.CatchAsync<OperationCanceledException>(async () => await LoginAttemptGate.OpenAsync(connection, admin.SecurityEpoch, cancel.Token));
        }
        finally { await gate.DisposeAsync(); }
        Assert.That(await Accounts.AuthenticateAsync(admin.Username, KnownPassword), Is.Not.Null);
        Assert.That(FailureCount, Is.Zero);
    }
}
