using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("LocalDb"), NonParallelizable]
public partial class GuideDatabaseTests
{
    private readonly string name = "AccountingAppGuideRegression_" + Guid.NewGuid().ToString("N");
    private string directory = "", connection = "", hash = "";
    private bool created;
    private UserSession admin = null!;
    private PersonnelService Service => new(connection, admin);
    private static PersonnelDetails Person(long tc = 11111111111L, decimal amount = 100m) => new(tc, "Ada", "Test", "TR000000000000000000000000", amount);
    private static string Master => TestDatabaseServer.Master;
    [OneTimeSetUp]
    public async Task Create()
    {
        directory = Path.Combine(Path.GetTempPath(), "AccountingAppTests", "g" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var settings = new DatabaseSettings(TestDatabaseServer.Server, name, Path.Combine(directory, "people.mdf"), 30);
        new DatabaseInstaller(settings).Install(() => new("test-admin", "Test-only-password-123")); created = true; connection = settings.CreateConnectionString();
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: Path.Combine(directory, "upgrade-backup")); hash = PasswordHasher.Hash("Test-only-password-123");
    }
    [SetUp]
    public void Reset()
    {
        Execute("DROP TRIGGER IF EXISTS dbo.GuideBlockAudit; DELETE FROM dbo.LoginThrottle; DELETE FROM dbo.PaymentLedger; DELETE FROM dbo.AuditLog; DELETE FROM dbo.izinler; DELETE FROM dbo.Calisan; DELETE FROM dbo.Joker; DELETE FROM dbo.admin;");
        using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand("INSERT INTO dbo.admin(username,password,password_hash,RoleName) VALUES(N'test-admin',N'',@hash,N'Admin')", c); cmd.Text("@hash", hash, 256); cmd.ExecuteNonQuery();
        admin = SessionFor("test-admin");
    }
    [OneTimeTearDown]
    public void Drop()
    {
        if (created) { SqlConnection.ClearAllPools(); using var c = new SqlConnection(Master); c.Open(); using var cmd = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", c); cmd.ExecuteNonQuery(); }
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    private void Execute(string sql) { using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand(sql, c); cmd.ExecuteNonQuery(); }
    private object Scalar(string sql) { using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand(sql, c); return cmd.ExecuteScalar()!; }
    // Fixture-only sessions come from real rows, without adding a login audit to every setup.
    private UserSession SessionFor(string username)
    {
        using var c = new SqlConnection(connection); c.Open();
        using var cmd = new SqlCommand("SELECT username,RoleName,SessionVersion,SessionEpoch,MustChangePassword FROM dbo.admin WHERE username=@name", c); cmd.Text("@name", username);
        using var reader = cmd.ExecuteReader(); Assert.That(reader.Read(), Is.True);
        return new(reader.GetString(0), Enum.Parse<UserRole>(reader.GetString(1))) { SecurityVersion = reader.GetFieldValue<byte[]>(2).ToArray(), SecurityEpoch = reader.GetGuid(3), MustChangePassword = reader.GetBoolean(4) };
    }
    private UserSession CreateActor(string username, UserRole role)
    {
        using var c = new SqlConnection(connection); c.Open();
        using var cmd = new SqlCommand("INSERT INTO dbo.admin(username,password,password_hash,RoleName) VALUES(@name,N'',@hash,@role)", c);
        cmd.Text("@name", username); cmd.Text("@hash", hash, 256); cmd.Text("@role", role.ToString(), 24); cmd.ExecuteNonQuery();
        return SessionFor(username);
    }
    private async Task<byte[]> VersionAsync(PersonnelGroup group = PersonnelGroup.General, long tc = 11111111111L) =>
        ((byte[])(await Service.ListAsync(group, tc: tc.ToString(System.Globalization.CultureInfo.InvariantCulture))).Rows[0][PersonnelConcurrency.ColumnName]).ToArray();
    private async Task<LeaveTarget> LeaveSelectionAsync(long tc = 11111111111L) =>
        LeaveTarget.FromRow((await Service.ListAsync(PersonnelGroup.General, tc: tc.ToString(System.Globalization.CultureInfo.InvariantCulture))).Rows[0]);
    private async Task<AccountTarget> TargetAsync(string username)
    {
        var row = (await new AccountService(connection).ListAsync(admin, default)).AsEnumerable().Single(r => r.Field<string>("username") == username);
        return new(username, ((byte[])row[AccountTarget.VersionColumn]).ToArray(), (Guid)row[AccountTarget.EpochColumn]);
    }
    private sealed class LostReplyGateway(IPaymentGateway inner, bool afterCommit) : IPaymentGateway
    {
        public Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, CancellationToken token) => inner.PreparePaymentAsync(group, tc, amount, token);
        public Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token) => inner.InspectPaymentAsync(request, token);
        public async Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token)
        {
            if (afterCommit) await inner.SubmitPaymentAsync(request, token);
            throw new IOException("Simulated lost connection");
        }
    }
    [Test, Category("Migrations")]
    public async Task StartupUpgradeIsRepeatableAndPreservesExistingRows()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); await SchemaUpgrade.PrepareAsync(connection); await SchemaUpgrade.PrepareAsync(connection);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"), Is.EqualTo(6)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.EqualTo(1));
    }
    [Test, Category("Migrations")]
    public async Task UnversionedLegacyDatabaseUpgradesWithoutFalseBaselineOrDataLoss()
    {
        string legacyName = "AccountingAppGuideRegression_" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(directory, "legacy"); Directory.CreateDirectory(folder);
        var settings = new DatabaseSettings(TestDatabaseServer.Server, legacyName, Path.Combine(folder, "legacy.mdf"), 30);
        bool exists = false;
        try
        {
            new DatabaseInstaller(settings).Install(() => new("legacy-admin", "Test-only-password-123")); exists = true;
            string legacyConnection = settings.CreateConnectionString();
            using (var c = new SqlConnection(legacyConnection))
            {
                c.Open(); using var cmd = new SqlCommand(@"DROP TABLE dbo.SchemaMigrations; DROP TABLE dbo.admin;
                    CREATE TABLE dbo.admin(username nvarchar(50),password nvarchar(50),password_hash nvarchar(256) NULL);
                    INSERT INTO dbo.admin(username,password) VALUES(N'legacy-admin',N'Test-only-password-123');
                    INSERT INTO dbo.Calisan(TC,AD,SOYAD,IBAN,PARA) VALUES(11111111111,N'Ada',N'Test',N'TR000000000000000000000000',1500.50);", c); cmd.ExecuteNonQuery();
            }
            await SchemaUpgrade.PrepareAsync(legacyConnection, safetyFolder: Path.Combine(folder, "safety"));
            using var verify = new SqlConnection(legacyConnection); verify.Open();
            using var check = new SqlCommand("SELECT RoleName,password,password_hash,(SELECT PARA FROM dbo.Calisan),(SELECT SUM(Delta) FROM dbo.PaymentLedger),OBJECT_ID(N'dbo.SchemaMigrations') FROM dbo.admin", verify);
            using var reader = check.ExecuteReader(); Assert.That(reader.Read(), Is.True); Assert.That(reader.GetString(0), Is.EqualTo("Admin")); Assert.That(reader.GetString(1), Is.Empty);
            Assert.That(PasswordHasher.Verify("Test-only-password-123", reader.GetString(2)), Is.True); Assert.That(reader.GetDecimal(3), Is.EqualTo(1500.50m)); Assert.That(reader.GetDecimal(4), Is.EqualTo(1500.50m)); Assert.That(reader.IsDBNull(5), Is.True);
            Assert.That(Directory.GetFiles(Path.Combine(folder, "safety"), "*.bak"), Has.Length.EqualTo(1));
        }
        finally
        {
            if (exists) { SqlConnection.ClearAllPools(); using var c = new SqlConnection(Master); c.Open(); using var cmd = new SqlCommand($"ALTER DATABASE [{legacyName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{legacyName}]", c); cmd.ExecuteNonQuery(); }
        }
    }
    [Test, Category("UnitOfWork")]
    public async Task DisposeWithoutCommitRollsBackAndCommittedWorkCannotBeReused()
    {
        await using (var work = await SqlUnitOfWork.OpenAsync(connection, default)) { using var cmd = work.Command("INSERT INTO dbo.AuditLog(Actor,Action) VALUES(N'test',N'Rollback')"); await cmd.ExecuteNonQueryAsync(); }
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(0));
        await using var committed = await SqlUnitOfWork.OpenAsync(connection, default); await committed.CommitAsync(default); Assert.Throws<InvalidOperationException>(() => committed.Command("SELECT 1"));
    }
    [Test, Category("Roles")]
    public async Task AuthenticationReturnsStoredRoleAndViewerCannotWriteViaService()
    {
        var accounts = new AccountService(connection); await accounts.AddAsync(admin, "viewer", "Test-only-password-123", UserRole.Viewer, default);
        var user = await accounts.AuthenticateAsync("viewer", "Test-only-password-123"); Assert.That(user?.Role, Is.EqualTo(UserRole.Viewer));
        Assert.That(await accounts.AuthenticateAsync("viewer", "incorrect"), Is.Null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await new PersonnelService(connection, user!).SaveAsync(PersonnelGroup.General, Person(), true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await accounts.ListAsync(user!, default));
        var table = await accounts.ListAsync(admin, default); Assert.That(table.Columns.Cast<DataColumn>().Where(c => !RecordSelection.IsInternalColumnName(c.ColumnName)).Select(c => c.ColumnName), Is.EquivalentTo(new[] { "username", "RoleName", "IsActive", "MustChangePassword", "PasswordResetExpiresUtc" }));
    }
    [Test, Category("Roles")]
    public async Task LastAdminCannotBeDeletedEvenWhenViewerExists()
    {
        var accounts = new AccountService(connection); await accounts.AddAsync(admin, "viewer", "Test-only-password-123", UserRole.Viewer, default);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.DeleteAsync(admin, await TargetAsync("test-admin"), default));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE RoleName=N'Admin'"), Is.EqualTo(1));
    }
    [Test, Category("Roles")]
    public async Task DeactivatedAccountCannotLoginOrUseAnyCachedPersonnelPermission()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager);
        var accounts = new AccountService(connection);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); byte[] personVersion = await VersionAsync();
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var stale = new PersonnelService(connection, actor);
        int audit = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledger = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Blocked" }, false, expectedVersion: personVersion));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.SaveAsync(PersonnelGroup.Joker, Person(tc: 22222222222L), true));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.DeleteAsync(PersonnelGroup.General, Person().Tc, new byte[8]));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 1, 1), new(2026, 1, 3)))]));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid()));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.ListAsync(null));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await stale.LedgerAsync());
        Assert.That(await accounts.AuthenticateAsync(actor.Username, "Test-only-password-123"), Is.Null);
        Assert.That(new AdminStore(connection).Authenticate(actor.Username, "Test-only-password-123"), Is.False);
        Assert.That(Scalar("SELECT AD FROM dbo.Calisan"), Is.EqualTo("Ada")); Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audit)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
    }
    [Test, Category("Roles")]
    public async Task ReactivationRequiresNewLoginAndDoesNotReviveOldSession()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), true, default);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        var fresh = await accounts.AuthenticateAsync(actor.Username, "Test-only-password-123");
        Assert.That(fresh, Is.Not.Null); Assert.That(fresh!.SecurityVersion, Is.Not.EqualTo(actor.SecurityVersion));
        await new PersonnelService(connection, fresh).SaveAsync(PersonnelGroup.General, Person(), true);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
    }
    [Test, Category("Roles")]
    public async Task DeletedAndRecreatedUsernameCannotReuseOldSession()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        await accounts.DeleteAsync(admin, await TargetAsync(actor.Username), default);
        var replacement = CreateActor(actor.Username, UserRole.PersonnelManager);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, replacement, default), Is.True);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new PersonnelService(connection, actor).SaveAsync(PersonnelGroup.General, Person(), true));
    }
    [TestCase(true)]
    [TestCase(false)]
    [Category("Roles")]
    public async Task RoleOrPasswordChangeInvalidatesExistingSession(bool roleChanged)
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager);
        if (roleChanged) Execute("UPDATE dbo.admin SET RoleName=N'Viewer' WHERE username=N'operator'");
        else
        {
            using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand("UPDATE dbo.admin SET password_hash=@hash WHERE username=N'operator'", c);
            cmd.Text("@hash", PasswordHasher.Hash("Changed-test-password-456"), 256); cmd.ExecuteNonQuery();
        }
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new PersonnelService(connection, actor).SaveAsync(PersonnelGroup.General, Person(), true));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        var fresh = await new AccountService(connection).AuthenticateAsync(actor.Username, roleChanged ? "Test-only-password-123" : "Changed-test-password-456");
        Assert.That(fresh?.Role, Is.EqualTo(roleChanged ? UserRole.Viewer : UserRole.PersonnelManager));
    }
    [Test, Category("Roles")]
    public async Task RevokedAdminCannotManageAccountsReversePaymentsOrCreateBackup()
    {
        var actor = CreateActor("second-admin", UserRole.Admin); var accounts = new AccountService(connection);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        Guid payment = await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.AddAsync(actor, "blocked", "Test-only-password-123", UserRole.Admin, default));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.DeleteAsync(actor, await TargetAsync(admin.Username), default));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.SetActiveAsync(actor, await TargetAsync(admin.Username), false, default));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.ListAsync(actor, default));
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new PersonnelService(connection, actor).ReversePaymentAsync(payment));
        string folder = Path.Combine(directory, "blocked-backup");
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new BackupService(connection, actor).BackupAsync(folder));
        Assert.That(Directory.Exists(folder), Is.False); Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m));
    }
    [Test, Category("Roles")]
    public async Task OwnAccountCannotBeDeactivatedAndNoopActivationKeepsSessionValid()
    {
        var accounts = new AccountService(connection);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.SetActiveAsync(admin, await TargetAsync(admin.Username), false, default));
        await accounts.SetActiveAsync(admin, await TargetAsync(admin.Username), true, default);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, admin, default), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE IsActive=1 AND RoleName=N'Admin'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.Zero);
    }
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [Category("Passwords")]
    public async Task EveryRoleCanChangeOwnPasswordAndAllOldSessionsAreRevoked(int roleId)
    {
        var actor = CreateActor("password-user", (UserRole)roleId); var accounts = new AccountService(connection);
        const string next = " Yeni parola cümlesi 2026! ";
        await accounts.ChangePasswordAsync(actor, "Test-only-password-123", next, next);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        Assert.That(await accounts.AuthenticateAsync(actor.Username, "Test-only-password-123"), Is.Null);
        Assert.That(await accounts.AuthenticateAsync(actor.Username, next.Trim()), Is.Null);
        var fresh = await accounts.AuthenticateAsync(actor.Username, next);
        Assert.That(fresh?.Role, Is.EqualTo((UserRole)roleId)); Assert.That(fresh!.MustChangePassword, Is.False);
        Assert.That(Scalar("SELECT password FROM dbo.admin WHERE username=N'password-user'"), Is.EqualTo(""));
        Assert.That(Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'password-user'"), Is.Not.EqualTo(next));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Account.PasswordChange'"), Is.EqualTo(1));
    }
    [Test, Category("Passwords")]
    public async Task WrongCurrentPasswordDoesNotChangeCredentialsOrAudit()
    {
        var actor = CreateActor("password-user", UserRole.PersonnelManager);
        const string next = "Replacement password 2026!";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await new AccountService(connection).ChangePasswordAsync(actor, "wrong-password", next, next));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.Zero);
        Assert.That((await new AccountService(connection).AuthenticateAsync(actor.Username, "Test-only-password-123")), Is.Not.Null);
    }
    [Test, Category("Passwords")]
    public async Task SamePasswordCannotBeReusedAndLegacyShortPasswordCanBeReplaced()
    {
        var actor = CreateActor("password-user", UserRole.Viewer); var accounts = new AccountService(connection);
        await Assert.ThrowsAsync<ArgumentException>(async () => await accounts.ChangePasswordAsync(actor, "Test-only-password-123", "Test-only-password-123", "Test-only-password-123"));
        using (var c = new SqlConnection(connection))
        {
            c.Open(); using var cmd = new SqlCommand("UPDATE dbo.admin SET password_hash=@hash WHERE username=N'password-user'", c); cmd.Text("@hash", PasswordHasher.Hash("eski12"), 256); cmd.ExecuteNonQuery();
        }
        actor = SessionFor(actor.Username);
        const string next = "A long new password phrase";
        await accounts.ChangePasswordAsync(actor, "eski12", next, next);
        Assert.That(await accounts.AuthenticateAsync(actor.Username, next), Is.Not.Null);
    }
    [Test, Category("Passwords")]
    public async Task AdminResetCreatesExpiringRestrictedLoginUntilPasswordIsChanged()
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        var result = await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123");
        Assert.That(Convert.FromBase64String(result.TemporaryPassword), Has.Length.EqualTo(24));
        Assert.That(result.ExpiresUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(result.ExpiresUtc, Is.InRange(DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31)));
        Assert.That(result.ToString(), Does.Not.Contain(result.TemporaryPassword));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
        Assert.That(await accounts.AuthenticateAsync(actor.Username, "Test-only-password-123"), Is.Null);
        var restricted = (await accounts.AuthenticateAsync(actor.Username, result.TemporaryPassword))!;
        Assert.That(restricted.MustChangePassword, Is.True); Assert.That(restricted.CanEdit, Is.False);
        Assert.That(new AdminStore(connection).Authenticate(actor.Username, result.TemporaryPassword), Is.False);
        var blocked = new PersonnelService(connection, restricted);
        await Assert.ThrowsAsync<PasswordChangeRequiredException>(async () => await blocked.ListAsync(null));
        await Assert.ThrowsAsync<PasswordChangeRequiredException>(async () => await blocked.SaveAsync(PersonnelGroup.General, Person(), true));
        await Assert.ThrowsAsync<PasswordChangeRequiredException>(async () => await blocked.PayAsync(PersonnelGroup.General, Person().Tc, 10m, Guid.NewGuid()));
        const string next = "My private new password 2026!";
        await accounts.ChangePasswordAsync(restricted, result.TemporaryPassword, next, next);
        Assert.That(await accounts.AuthenticateAsync(actor.Username, result.TemporaryPassword), Is.Null);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, restricted, default), Is.False);
        var fresh = (await accounts.AuthenticateAsync(actor.Username, next))!;
        Assert.That(fresh.MustChangePassword, Is.False);
        Assert.That(Scalar("SELECT PasswordResetExpiresUtc FROM dbo.admin WHERE username=N'reset-user'"), Is.EqualTo(DBNull.Value));
        await new PersonnelService(connection, fresh).SaveAsync(PersonnelGroup.General, Person(), true);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
    }
    [Test, Category("Passwords")]
    public async Task TemporaryPasswordExpiryIsEnforcedByDatabaseTime()
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        var result = await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123");
        Execute("UPDATE dbo.admin SET PasswordResetExpiresUtc=DATEADD(second,-1,SYSUTCDATETIME()) WHERE username=N'reset-user'");
        Assert.That(await accounts.AuthenticateAsync(actor.Username, result.TemporaryPassword), Is.Null);
        var expired = SessionFor(actor.Username);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, expired, default), Is.False);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.ChangePasswordAsync(expired, result.TemporaryPassword, "A different password phrase", "A different password phrase"));
    }
    [Test, Category("Passwords")]
    public async Task SecondResetInvalidatesFirstTemporaryPassword()
    {
        var actor = CreateActor("reset-user", UserRole.Viewer); var accounts = new AccountService(connection);
        var first = await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123");
        var second = await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123");
        Assert.That(second.TemporaryPassword, Is.Not.EqualTo(first.TemporaryPassword));
        Assert.That(await accounts.AuthenticateAsync(actor.Username, first.TemporaryPassword), Is.Null);
        Assert.That((await accounts.AuthenticateAsync(actor.Username, second.TemporaryPassword))!.MustChangePassword, Is.True);
    }
    [Test, Category("Passwords")]
    public async Task ResetRequiresCorrectAdministratorPasswordAndRefusesSelfOrInactiveTarget()
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "wrong-password"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await accounts.ResetPasswordAsync(actor, await TargetAsync(admin.Username), "Test-only-password-123"));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.ResetPasswordAsync(admin, await TargetAsync(admin.Username), "Test-only-password-123"));
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Account.PasswordReset'"), Is.Zero);
        Assert.That(Scalar("SELECT MustChangePassword FROM dbo.admin WHERE username=N'reset-user'"), Is.EqualTo(false));
    }
    [Test, Category("Passwords")]
    public async Task ResetCannotTargetRecreatedAccountOrUseRevokedAdministratorSession()
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        var oldTarget = await TargetAsync(actor.Username);
        await accounts.DeleteAsync(admin, await TargetAsync(actor.Username), default); CreateActor(actor.Username, UserRole.PersonnelManager);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.ResetPasswordAsync(admin, oldTarget, "Test-only-password-123"));
        var otherAdmin = CreateActor("second-admin", UserRole.Admin); await accounts.SetActiveAsync(admin, await TargetAsync(otherAdmin.Username), false, default);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await accounts.ResetPasswordAsync(otherAdmin, await TargetAsync(actor.Username), "Test-only-password-123"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Account.PasswordReset'"), Is.Zero);
    }
    [TestCase(true)]
    [TestCase(false)]
    [Category("Passwords")]
    public async Task PasswordMutationAuditFailureRollsBackHashFlagsAndSessions(bool reset)
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection); var target = await TargetAsync(actor.Username);
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS THROW 51099,N'Test password audit rollback',1;");
        if (reset) await Assert.ThrowsAsync<SqlException>(async () => await accounts.ResetPasswordAsync(admin, target, "Test-only-password-123"));
        else await Assert.ThrowsAsync<SqlException>(async () => await accounts.ChangePasswordAsync(actor, "Test-only-password-123", "A replacement password phrase", "A replacement password phrase"));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.True);
        Assert.That(Scalar("SELECT MustChangePassword FROM dbo.admin WHERE username=N'reset-user'"), Is.EqualTo(false));
        Assert.That(Scalar("SELECT PasswordResetExpiresUtc FROM dbo.admin WHERE username=N'reset-user'"), Is.EqualTo(DBNull.Value));
        Assert.That(PasswordHasher.Verify("Test-only-password-123", (string)Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'reset-user'")), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.Zero);
    }
    [Test, Category("Passwords")]
    public async Task ConcurrentPasswordChangesHaveOnlyOneWinner()
    {
        var actor = CreateActor("password-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        async Task<bool> Change(string next)
        {
            try { await accounts.ChangePasswordAsync(actor, "Test-only-password-123", next, next); return true; }
            catch (SessionRevokedException) { return false; }
        }
        var outcomes = await Task.WhenAll(Change("First replacement password"), Change("Second replacement password"));
        Assert.That(outcomes.Count(x => x), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Account.PasswordChange'"), Is.EqualTo(1));
    }
    [Test, Category("Passwords")]
    public async Task PasswordMetadataAndSecretsAreNotExportedOrLogged()
    {
        var actor = CreateActor("reset-user", UserRole.PersonnelManager); var accounts = new AccountService(connection);
        var result = await accounts.ResetPasswordAsync(admin, await TargetAsync(actor.Username), "Test-only-password-123");
        var data = await accounts.ListAsync(admin, default);
        Assert.That(data.Columns.Contains("password_hash"), Is.False); Assert.That(data.Columns.Contains("password"), Is.False);
        string csv = Path.Combine(directory, "accounts.csv"); ReportExport.Csv(data, csv);
        string text = File.ReadAllText(csv);
        Assert.That(text, Does.Not.Contain(AccountTarget.VersionColumn)); Assert.That(text, Does.Not.Contain(AccountTarget.EpochColumn));
        Assert.That(text, Does.Not.Contain(result.TemporaryPassword));
        NLog.LogManager.Flush();
        using var stream = new FileStream(Path.Combine(UserPreferences.DirectoryPath, "logs", "application.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream); Assert.That(reader.ReadToEnd(), Does.Not.Contain(result.TemporaryPassword));
    }
    [Test, Category("Roles")]
    public async Task ConcurrentAdminDeactivationsCannotRemoveAllActiveAdmins()
    {
        var other = CreateActor("second-admin", UserRole.Admin); var accounts = new AccountService(connection);
        var otherTarget = await TargetAsync(other.Username); var ownTarget = await TargetAsync(admin.Username);
        async Task<bool> Deactivate(UserSession user, AccountTarget selected)
        {
            try { await accounts.SetActiveAsync(user, selected, false, default); return true; }
            catch (SessionRevokedException) { return false; }
        }
        bool[] results = await Task.WhenAll(Deactivate(admin, otherTarget), Deactivate(other, ownTarget));
        Assert.That(results.Count(r => r), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE IsActive=1 AND RoleName=N'Admin'"), Is.EqualTo(1));
    }
    [Test, Category("Payments")]
    public async Task RevocationPreservesUncertainPaymentAndFreshLoginCanRetrySameRequest()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager); var service = new PersonnelService(connection, actor);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "revoked-pending"));
        var interrupted = new PaymentWorkflow(journal, new LostReplyGateway(service, false), actor);
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.BeginAsync(PersonnelGroup.General, Person().Tc, 30m));
        byte[] saved = File.ReadAllBytes(journal.PathName);
        var accounts = new AccountService(connection); await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        var old = new PaymentWorkflow(journal, service, actor);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await old.RetryAsync());
        Assert.That(File.ReadAllBytes(journal.PathName), Is.EqualTo(saved));
        await accounts.SetActiveAsync(admin, await TargetAsync(actor.Username), true, default);
        var fresh = (await accounts.AuthenticateAsync(actor.Username, "Test-only-password-123"))!;
        var reopened = new PaymentWorkflow(journal, new PersonnelService(connection, fresh), fresh);
        Guid original = (await reopened.InspectAsync())!.Request.RequestId;
        Assert.That((await reopened.RetryAsync()).Request.RequestId, Is.EqualTo(original));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(1));
    }
    [Test, Category("Roles")]
    public async Task AuthorizationRowLockPreventsRevocationInMiddleOfTransaction()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager);
        await using (var work = await SqlUnitOfWork.OpenAsync(connection, default))
        {
            await AccountAuthorization.DemandAsync(work, actor, AccountAccess.Edit, default);
            using var c = new SqlConnection(connection); await c.OpenAsync();
            using var cmd = new SqlCommand("SET LOCK_TIMEOUT 250; UPDATE dbo.admin SET IsActive=0 WHERE username=N'operator'", c);
            var failure = await Assert.ThrowsAsync<SqlException>(async () => await cmd.ExecuteNonQueryAsync());
            Assert.That(failure!.Number, Is.EqualTo(1222));
            await work.CommitAsync(default);
        }
        await new AccountService(connection).SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, actor, default), Is.False);
    }
    [TestCase(false)]
    [TestCase(true)]
    [Category("Roles")]
    public async Task CachedUsernameAndRoleCannotReplaceAuthenticatedSession(bool copyVersion)
    {
        var forged = new UserSession(admin.Username, admin.Role) { SecurityVersion = copyVersion ? admin.SecurityVersion!.ToArray() : null };
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new PersonnelService(connection, forged).SaveAsync(PersonnelGroup.General, Person(), true));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.Zero);
    }
    [Test, Category("Roles")]
    public async Task DifferentEpochInvalidatesSessionEvenWhenRowVersionMatches()
    {
        var wrongEpoch = admin with { SecurityEpoch = Guid.NewGuid() };
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, wrongEpoch, default), Is.False);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await new PersonnelService(connection, wrongEpoch).ListAsync(null));
    }
    [Test, Category("Payments")]
    public async Task RevokedUserCannotAcknowledgeCachedRejectedPayment()
    {
        var actor = CreateActor("operator", UserRole.PersonnelManager); var service = new PersonnelService(connection, actor);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "rejected-pending"));
        await Assert.ThrowsAsync<IOException>(async () => await new PaymentWorkflow(journal, new LostReplyGateway(service, false), actor).BeginAsync(PersonnelGroup.General, Person().Tc, 80m));
        await Service.PayAsync(PersonnelGroup.General, Person().Tc, 50m, Guid.NewGuid());
        var flow = new PaymentWorkflow(journal, service, actor);
        Assert.That((await flow.RetryAsync()).Outcome.Status, Is.EqualTo(PaymentStatus.Rejected));
        byte[] saved = File.ReadAllBytes(journal.PathName);
        await new AccountService(connection).SetActiveAsync(admin, await TargetAsync(actor.Username), false, default);
        await Assert.ThrowsAsync<SessionRevokedException>(async () => await flow.AcknowledgeAsync());
        Assert.That(File.ReadAllBytes(journal.PathName), Is.EqualTo(saved));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(50m));
    }
    [Test, Category("Migrations")]
    public async Task AccountSessionUpgradePreservesExistingAccountsRolesHashesAndBusinessRows()
    {
        CreateActor("viewer", UserRole.Viewer);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        string beforeHash = (string)Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'test-admin'");
        var previous = admin;
        Execute("ALTER TABLE dbo.admin DROP CONSTRAINT DF_AdminIsActive; ALTER TABLE dbo.admin DROP CONSTRAINT DF_AdminSessionEpoch; ALTER TABLE dbo.admin DROP COLUMN IsActive,SessionVersion,SessionEpoch; DELETE FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%102-account-session-version.sql:%'");
        string folder = Path.Combine(directory, "account-upgrade");
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder);
        admin = SessionFor("test-admin"); var epoch = admin.SecurityEpoch; byte[] version = admin.SecurityVersion!.ToArray();
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE IsActive=1"), Is.EqualTo(2));
        Assert.That(Scalar("SELECT RoleName FROM dbo.admin WHERE username=N'viewer'"), Is.EqualTo("Viewer"));
        Assert.That(Scalar("SELECT password_hash FROM dbo.admin WHERE username=N'test-admin'"), Is.EqualTo(beforeHash));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(1));
        Assert.That(Directory.GetFiles(folder, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, previous, default), Is.False);
        Assert.That(SessionFor("test-admin").SecurityVersion, Is.EqualTo(version));
        Assert.That(SessionFor("test-admin").SecurityEpoch, Is.EqualTo(epoch));
    }
    [Test, Category("Personnel")]
    public async Task AsyncCrudSearchAndLedgerStayConsistent()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); await Service.SaveAsync(PersonnelGroup.General, Person(amount: 125m), false, expectedVersion: await VersionAsync());
        var data = await Service.ListAsync(null, default, "11111111111", "Ada", "Test"); Assert.That(data.Rows.Count, Is.EqualTo(1)); Assert.That(data.Rows[0]["PARA"], Is.EqualTo(125m));
        Assert.That(data.Rows[0].HasVersion(DataRowVersion.Original), Is.True);
        Assert.That((await Service.ListAsync(null, default, "", "%", "")).Rows.Count, Is.Zero); // wildcard treated literally
        await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync()); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.Zero);
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(0m)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(3));
    }
    [Test, Category("Payments")]
    public async Task StalePersonnelEditCannotUndoAnInterveningPayment()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); byte[] version = await VersionAsync();
        await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveAsync(PersonnelGroup.General, Person(), false, expectedVersion: version));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m)); Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(70m));
    }
    [TestCase(0, true)]
    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(1, false)]
    [Category("Personnel")]
    public async Task StaleNameOrIbanEditCannotOverwriteAnotherUsersChanges(int groupId, bool nameChanged)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] version = await VersionAsync(group);
        var winner = nameChanged ? Person() with { FirstName = "Ayşe" } : Person() with { Iban = "TR111111111111111111111111" };
        var stale = nameChanged ? Person() with { Iban = "TR222222222222222222222222" } : Person() with { LastName = "Başka" };
        var anotherUser = new PersonnelService(connection, CreateActor("second-user", UserRole.PersonnelManager));
        await anotherUser.SaveAsync(group, winner, false, expectedVersion: version);
        byte[] currentVersion = await VersionAsync(group);
        int auditCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledgerCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveAsync(group, stale, false, expectedVersion: version));

        Assert.That(error!.Message, Is.EqualTo(PersonnelConcurrency.ConflictMessage));
        var row = (await Service.ListAsync(group)).Rows[0];
        Assert.That(row["AD"], Is.EqualTo(winner.FirstName)); Assert.That(row["SOYAD"], Is.EqualTo(winner.LastName));
        Assert.That(row["IBAN"], Is.EqualTo(winner.Iban)); Assert.That(row["PARA"], Is.EqualTo(100m));
        Assert.That(await VersionAsync(group), Is.EqualTo(currentVersion)); Assert.That(currentVersion, Is.Not.EqualTo(version));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(auditCount));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledgerCount));
    }
    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task TwoSimultaneousEditsWithSameVersionHaveExactlyOneWinner(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] version = await VersionAsync(group);
        var actors = new Dictionary<string, UserSession> { ["Birinci"] = CreateActor("Birinci", UserRole.PersonnelManager), ["İkinci"] = CreateActor("İkinci", UserRole.PersonnelManager) };
        async Task<bool> Edit(string first)
        {
            try { await new PersonnelService(connection, actors[first]).SaveAsync(group, Person() with { FirstName = first }, false, expectedVersion: version); return true; }
            catch (InvalidOperationException) { return false; }
        }
        bool[] results = await Task.WhenAll(Edit("Birinci"), Edit("İkinci"));
        Assert.That(results.Count(r => r), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Update'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(2));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(100m));
    }
    [TestCase(0)]
    [TestCase(1)]
    [Category("Personnel")]
    public async Task OldVersionCannotEditRecreatedIdenticalPersonnel(int groupId)
    {
        var group = (PersonnelGroup)groupId;
        await Service.SaveAsync(group, Person(), true); byte[] oldVersion = await VersionAsync(group);
        await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group)); await Service.SaveAsync(group, Person(), true);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveAsync(group, Person() with { FirstName = "Eski ekran" }, false, expectedVersion: oldVersion));
        Assert.That((await Service.ListAsync(group)).Rows[0]["AD"], Is.EqualTo("Ada"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Person.Update'"), Is.Zero);
    }
    [Test, Category("Personnel")]
    public async Task RefreshedVersionAllowsUpdateAndMultipleLeavesRemainUnchanged()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        Execute("INSERT INTO dbo.izinler(TC,IZINBASLANGIC,IZINBITIS) VALUES(11111111111,'20260101','20260103'),(11111111111,'20260201','20260203')");
        var before = await Service.ListAsync(PersonnelGroup.General);
        Assert.That(before.Rows[0][PersonnelConcurrency.ColumnName], Is.EqualTo(before.Rows[1][PersonnelConcurrency.ColumnName]));
        await Service.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Yeni" }, false, expectedVersion: await VersionAsync());
        await Service.SaveAsync(PersonnelGroup.General, Person() with { FirstName = "Yeni", Iban = "TR111111111111111111111111" }, false, expectedVersion: await VersionAsync());
        Assert.That(Scalar("SELECT AD FROM dbo.Calisan"), Is.EqualTo("Yeni"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(2));
        Assert.That(Scalar("SELECT MIN(IZINBASLANGIC) FROM dbo.izinler"), Is.EqualTo(new DateTime(2026, 1, 1)));
        Assert.That(Scalar("SELECT MAX(IZINBITIS) FROM dbo.izinler"), Is.EqualTo(new DateTime(2026, 2, 3)));
    }
    [Test, Category("Migrations")]
    public async Task RowVersionUpgradeBacksUpAndPreservesExistingPeopleMoneyLeavesAndAudit()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        await Service.SaveAsync(PersonnelGroup.Joker, Person(tc: 22222222222L, amount: 200m), true);
        await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new DateTime(2026, 1, 1), new DateTime(2026, 1, 3)))]);
        await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        int auditCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        Execute("ALTER TABLE dbo.Calisan DROP COLUMN RecordVersion; ALTER TABLE dbo.Joker DROP COLUMN RecordVersion; DELETE FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%101-personnel-rowversion.sql:%'");
        string backups = Path.Combine(directory, "version-upgrade");

        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: backups);
        byte[] version = await VersionAsync();
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: backups);

        Assert.That(version, Has.Length.EqualTo(8)); Assert.That(await VersionAsync(), Is.EqualTo(version));
        Assert.That(await VersionAsync(PersonnelGroup.Joker, 22222222222L), Has.Length.EqualTo(8));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m)); Assert.That(Scalar("SELECT PARA FROM dbo.Joker"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT IBAN FROM dbo.Calisan"), Is.EqualTo(Person().Iban));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(270m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(auditCount));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"), Is.EqualTo(6));
        Assert.That(Directory.GetFiles(backups, "*.bak"), Has.Length.EqualTo(1));
    }
    [Test, Category("Migrations")]
    public async Task FailedRowVersionUpgradeRollsBackDdlAndLeavesBusinessDataUntouched()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        Execute("ALTER TABLE dbo.Calisan DROP COLUMN RecordVersion; ALTER TABLE dbo.Joker DROP COLUMN RecordVersion; ALTER TABLE dbo.Joker ADD RecordVersion binary(8) NULL; DELETE FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%101-personnel-rowversion.sql:%'");
        string backups = Path.Combine(directory, "version-rollback");
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await SchemaUpgrade.PrepareAsync(connection, safetyFolder: backups));
            Assert.That(Scalar("SELECT COALESCE(COL_LENGTH(N'dbo.Calisan',N'RecordVersion'),-1)"), Is.EqualTo(-1));
            Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%101-personnel-rowversion.sql:%'"), Is.Zero);
            Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
            Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(1));
            Assert.That(Directory.GetFiles(backups, "*.bak"), Has.Length.EqualTo(1));
        }
        finally
        {
            Execute("ALTER TABLE dbo.Joker DROP COLUMN RecordVersion");
            await SchemaUpgrade.PrepareAsync(connection, safetyFolder: backups);
        }
    }
    [Test, Category("Leaves")]
    public async Task BulkLeavePlanRollsBackIfAnyPersonIsInvalid()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); var leave = new LeavePeriod(new(2026, 10, 24), new(2026, 10, 26));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), leave), (new LeaveTarget(22222222222L, new byte[8], null), leave)]));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), leave)]); Assert.That(Scalar("SELECT IzinSuresi FROM dbo.izinler"), Is.EqualTo(3));
    }
    [Test, Category("Payments")]
    public async Task ConcurrentPaymentsCannotExceedBalanceAndRetriesAreIdempotent()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        async Task<bool> Pay() { try { await Service.PayAsync(PersonnelGroup.General, Person().Tc, 80m, Guid.NewGuid()); return true; } catch (InvalidOperationException) { return false; } }
        var results = await Task.WhenAll(Pay(), Pay()); Assert.That(results.Count(r => r), Is.EqualTo(1)); Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(20m));
        Guid request = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 10m, request); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 10m, request);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(10m)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(2));
    }
    [Test, Category("Payments")]
    public async Task DurableRetryAfterLostCommitReplyDoesNotDebitTwice()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N")));
        var interrupted = new PaymentWorkflow(journal, new LostReplyGateway(Service, afterCommit: true), admin);
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.BeginAsync(PersonnelGroup.General, Person().Tc, 30m));
        var reopened = new PaymentWorkflow(new PaymentJournal(connection, Path.GetDirectoryName(journal.PathName)), Service, admin);

        Assert.That((await reopened.InspectAsync())!.Outcome.Status, Is.EqualTo(PaymentStatus.Recorded));
        await reopened.RetryAsync(); await reopened.RetryAsync();

        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Payment.Record'"), Is.EqualTo(1));
        await reopened.AcknowledgeAsync(); Assert.That(await reopened.InspectAsync(), Is.Null);
    }
    [Test, Category("Payments")]
    public async Task DurableUnsentRequestCannotDebitRecreatedPersonnel()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N")));
        var interrupted = new PaymentWorkflow(journal, new LostReplyGateway(Service, afterCommit: false), admin);
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.BeginAsync(PersonnelGroup.General, Person().Tc, 30m));
        await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync());
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 200m), true);

        var reopened = new PaymentWorkflow(journal, Service, admin);
        Assert.That((await reopened.RetryAsync()).Outcome.Status, Is.EqualTo(PaymentStatus.Rejected));

        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.Zero);
        await reopened.AcknowledgeAsync(); Assert.That(await reopened.InspectAsync(), Is.Null);
    }
    [Test, Category("Payments")]
    public async Task ChangedBalanceRejectsDurableRetryWithoutBlockingSubsequentPayment()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "pending-" + Guid.NewGuid().ToString("N")));
        var interrupted = new PaymentWorkflow(journal, new LostReplyGateway(Service, afterCommit: false), admin);
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.BeginAsync(PersonnelGroup.General, Person().Tc, 80m));
        await Service.PayAsync(PersonnelGroup.General, Person().Tc, 50m, Guid.NewGuid());
        var reopened = new PaymentWorkflow(journal, Service, admin);
        Assert.That((await reopened.RetryAsync()).Outcome.Status, Is.EqualTo(PaymentStatus.Rejected));
        await reopened.AcknowledgeAsync();
        await reopened.BeginAsync(PersonnelGroup.General, Person().Tc, 10m);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(40m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(2));
    }
    [Test, Category("Payments")]
    public async Task PreparedConcurrentRetriesAreIdempotentAndReversedPaymentIsRecognized()
    {
        await Service.SaveAsync(PersonnelGroup.Joker, Person(), true);
        var request = await Service.PreparePaymentAsync(PersonnelGroup.Joker, Person().Tc, 30m, default);
        var outcomes = await Task.WhenAll(Service.SubmitPaymentAsync(request, default), Service.SubmitPaymentAsync(request, default));
        Assert.That(outcomes.All(o => o.Status == PaymentStatus.Recorded), Is.True);
        Assert.That(Scalar("SELECT PARA FROM dbo.Joker"), Is.EqualTo(70m));
        await Service.ReversePaymentAsync(request.RequestId);
        Assert.That((await Service.InspectPaymentAsync(request, default)).Status, Is.EqualTo(PaymentStatus.Reversed));
        Assert.That((await Service.SubmitPaymentAsync(request, default)).Status, Is.EqualTo(PaymentStatus.Reversed));
        Assert.That(Scalar("SELECT PARA FROM dbo.Joker"), Is.EqualTo(100m));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.InspectPaymentAsync(request with { Amount = 40m }, default));
    }
    [Test, Category("Exports")]
    public async Task ActualMultipleLeaveRowsExportWithOneBalancePerPerson()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 1000m), true);
        await Service.SaveAsync(PersonnelGroup.General, Person(tc: 22222222222L, amount: 1000m), true);
        Execute("INSERT INTO dbo.izinler(TC,IZINBASLANGIC,IZINBITIS) VALUES(11111111111,'20260101','20260103'),(11111111111,'20260201','20260203')");
        var table = await Service.ListAsync(PersonnelGroup.General);
        Assert.That(table.Rows.Count, Is.EqualTo(3));
        string path = Path.Combine(directory, "multiple-leaves.xlsx"); ReportExport.Excel(table, path);
        using var book = new ClosedXML.Excel.XLWorkbook(path);
        Assert.That(book.Worksheet(1).Cell(table.Rows.Count + 2, table.Columns["PARA"]!.Ordinal + 1).GetValue<decimal>(), Is.EqualTo(2000m));
    }
    [Test, Category("Ledger")]
    public async Task ReversalRestoresBalanceExactlyOnceAndRetainsHistory()
    {
        await Service.SaveAsync(PersonnelGroup.Joker, Person(), true); Guid request = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.Joker, Person().Tc, 30m, request);
        await Service.ReversePaymentAsync(request); await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.ReversePaymentAsync(request));
        Assert.That(Scalar("SELECT PARA FROM dbo.Joker"), Is.EqualTo(100m)); var ledger = await Service.LedgerAsync(); Assert.That(ledger.Rows[^1]["RunningBalance"], Is.EqualTo(100m));
    }
    [TestCase((int)PersonnelGroup.General, 30)]
    [TestCase((int)PersonnelGroup.General, 100)]
    [TestCase((int)PersonnelGroup.Joker, 30)]
    [TestCase((int)PersonnelGroup.Joker, 100)]
    [Category("Ledger")]
    public async Task OldPaymentCannotBeReversedIntoRecreatedPersonnel(int groupValue, int paid)
    {
        var group = (PersonnelGroup)groupValue;
        await Service.SaveAsync(group, Person(), true);
        Guid oldPayment = await Service.PayAsync(group, Person().Tc, paid, Guid.NewGuid());
        await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group));
        await Service.SaveAsync(group, Person(amount: 200m), true);
        int auditCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        int ledgerCount = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.ReversePaymentAsync(oldPayment));

        Assert.That(error!.Message, Does.Contain("önceki personel kaydına"));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment' AND ReversedUtc IS NULL"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledgerCount));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(auditCount));
    }
    [TestCase((int)PersonnelGroup.General)]
    [TestCase((int)PersonnelGroup.Joker)]
    [Category("Ledger")]
    public async Task PaymentInCurrentPersonnelRecordCanBeReversedAfterEarlierClosure(int groupValue)
    {
        var group = (PersonnelGroup)groupValue;
        await Service.SaveAsync(group, Person(), true);
        await Service.PayAsync(group, Person().Tc, 30m, Guid.NewGuid());
        await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group));
        await Service.SaveAsync(group, Person(amount: 200m), true);
        Guid currentPayment = await Service.PayAsync(group, Person().Tc, 40m, Guid.NewGuid());

        await Service.ReversePaymentAsync(currentPayment);

        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(200m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Reversal'"), Is.EqualTo(1));
    }
    [Test, Category("Ledger")]
    public async Task AnotherPersonClosureDoesNotBlockPaymentReversal()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        Guid payment = await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        await Service.SaveAsync(PersonnelGroup.General, Person(tc: 22222222222L), true);
        await Service.DeleteAsync(PersonnelGroup.General, 22222222222L, await VersionAsync(tc: 22222222222L));

        await Service.ReversePaymentAsync(payment);

        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan WHERE TC=11111111111"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(100m));
    }
    [Test, Category("Audit")]
    public async Task AuditFailureRollsBackPersonAndLedgerTogether()
    {
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS THROW 51099,N'Test rollback',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await Service.SaveAsync(PersonnelGroup.General, Person(), true));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.Zero); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.Zero); await Task.CompletedTask;
    }
    [Test, Category("Backup")]
    public async Task BackupIsVerifiedAndMirrored()
    {
        var service = new BackupService(connection, admin); string folder = Path.Combine(directory, "backups"), mirror = Path.Combine(directory, "mirror");
        string path = await service.BackupAsync(folder, mirror); Assert.That(File.Exists(path), Is.True); Assert.That(File.ReadAllBytes(Path.Combine(mirror, Path.GetFileName(path))), Is.EqualTo(File.ReadAllBytes(path)));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Create'"), Is.EqualTo(1));
    }
    [Test, Category("FullRestore")]
    public async Task RestoreOnlyDisposableDatabaseAndProveRestoredContent()
    {
        Assert.That(name, Does.StartWith("AccountingAppGuideRegression_")); // Full mode never receives a production connection.
        var backup = new BackupService(connection, admin); await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        string path = await backup.BackupAsync(Path.Combine(directory, "restore-source")); await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync());
        var beforeRestore = admin;
        await backup.RestoreAsync(path, Path.Combine(directory, "restore-safety")); Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, beforeRestore, default), Is.False);
        admin = SessionFor("test-admin"); Assert.That(admin.SecurityEpoch, Is.Not.EqualTo(beforeRestore.SecurityEpoch));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Restore'"), Is.EqualTo(1));
    }
    [Test, Category("FullRestore")]
    public async Task RestoreAuditFailureDoesNotUndoSessionRevocation()
    {
        var previous = admin;
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Backup.Restore') THROW 51099,N'Test restore audit failure',1;");
        var backup = new BackupService(connection, admin);
        string path = await backup.BackupAsync(Path.Combine(directory, "restore-audit-source"));
        await Assert.ThrowsAsync<SqlException>(async () => await backup.RestoreAsync(path, Path.Combine(directory, "restore-audit-safety")));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, previous, default), Is.False);
        admin = SessionFor("test-admin");
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, admin, default), Is.True);
    }
    [Test, Category("FullRestore")]
    public async Task RestoreOldestOfFourteenBackupsInSameFolderPreservesSelectedSource()
    {
        Assert.That(name, Does.StartWith("AccountingAppGuideRegression_"));
        var backup = new BackupService(connection, admin);
        string folder = Path.Combine(directory, "restore-retention-" + Guid.NewGuid().ToString("N"));
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        string original = await backup.BackupAsync(folder);
        string prefix = BackupService.Prefix(name);
        string selected = Path.Combine(folder, prefix + "0000.bak");
        File.Move(original, selected);
        File.Move(original + ".verified", selected + ".verified");
        for (int i = 1; i < 14; i++) File.Copy(selected, Path.Combine(folder, prefix + i.ToString("D4") + ".bak"));
        byte[] digest = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(selected));
        await Service.DeleteAsync(PersonnelGroup.General, Person().Tc, await VersionAsync());

        await backup.RestoreAsync(selected, folder);
        admin = SessionFor("test-admin");

        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(File.Exists(selected), Is.True);
        Assert.That(File.Exists(selected + ".verified"), Is.True);
        Assert.That(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(selected)), Is.EqualTo(digest));
        Assert.That(Directory.GetFiles(folder, prefix + "*.bak").Length, Is.InRange(14, 15));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Restore'"), Is.EqualTo(1));
    }
    [Test, Category("FullRestore")]
    public async Task RestoreOldSchemaPreservesSelectedBackupDuringFollowingVersionUpgrade()
    {
        Assert.That(name, Does.StartWith("AccountingAppGuideRegression_"));
        var backup = new BackupService(connection, admin);
        string folder = Path.Combine(directory, "restore-old-schema-" + Guid.NewGuid().ToString("N")[..8]);
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);
        Execute("ALTER TABLE dbo.Calisan DROP COLUMN RecordVersion; ALTER TABLE dbo.Joker DROP COLUMN RecordVersion; DELETE FROM dbo.__MigrationJournal WHERE ScriptName LIKE N'%101-personnel-rowversion.sql:%'");
        try
        {
            string original = await backup.BackupAsync(folder);
            string prefix = BackupService.Prefix(name), selected = Path.Combine(folder, prefix + "0000.bak");
            File.Move(original, selected); File.Move(original + ".verified", selected + ".verified");
            for (int i = 1; i < 14; i++) File.Copy(selected, Path.Combine(folder, prefix + i.ToString("D4") + ".bak"));
            byte[] digest = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(selected));
            Execute("UPDATE dbo.Calisan SET PARA=70");

            await backup.RestoreAsync(selected, folder);
            admin = SessionFor("test-admin");

            Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
            Assert.That(await VersionAsync(), Has.Length.EqualTo(8));
            Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"), Is.EqualTo(6));
            Assert.That(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(selected)), Is.EqualTo(digest));
            Assert.That(File.Exists(selected + ".verified"), Is.True);
            Assert.That(Directory.GetFiles(folder, prefix + "*.bak"), Has.Length.EqualTo(15));
        }
        finally { await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder); }
    }
    [Test, Category("Backup")]
    public async Task InvalidRestoreSourceDoesNotCreateSafetyBackupOrPruneExistingBackups()
    {
        var backup = new BackupService(connection, admin);
        string folder = Path.Combine(directory, "invalid-restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string prefix = BackupService.Prefix(name);
        for (int i = 0; i < 14; i++) File.WriteAllText(Path.Combine(folder, prefix + i.ToString("D4") + ".bak"), "invalid backup");
        string selected = Path.Combine(folder, prefix + "0000.bak");
        await Service.SaveAsync(PersonnelGroup.General, Person(), true);

        await Assert.ThrowsAsync<SqlException>(async () => await backup.RestoreAsync(selected, folder));

        Assert.That(Directory.GetFiles(folder, prefix + "*.bak"), Has.Length.EqualTo(14));
        Assert.That(File.ReadAllText(selected), Is.EqualTo("invalid backup"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Create'"), Is.Zero);
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        // A failed restore must release the gate so an ordinary backup can still run.
        Assert.That(File.Exists(await backup.BackupAsync(Path.Combine(folder, "after-failure"))), Is.True);
    }
}
