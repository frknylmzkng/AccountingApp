using System.Data;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("LocalDb"), NonParallelizable]
public class GuideDatabaseTests
{
    private readonly string name = "AccountingAppGuideRegression_" + Guid.NewGuid().ToString("N");
    private string directory = "", connection = "", hash = "";
    private bool created;
    private readonly UserSession admin = new("test-admin", UserRole.Admin);
    private PersonnelService Service => new(connection, admin);
    private static PersonnelDetails Person(long tc = 11111111111L, decimal amount = 100m) => new(tc, "Ada", "Test", "TR000000000000000000000000", amount);
    private const string Master = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Encrypt=False;Pooling=False";
    [OneTimeSetUp]
    public async Task Create()
    {
        directory = Path.Combine(Path.GetTempPath(), "AccountingAppTests", "g" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var settings = new DatabaseSettings(@"(LocalDB)\MSSQLLocalDB", name, Path.Combine(directory, "people.mdf"), 30);
        new DatabaseInstaller(settings).Install(() => new("test-admin", "Test-only-password-123")); created = true; connection = settings.CreateConnectionString();
        await SchemaUpgrade.PrepareAsync(connection, safetyFolder: Path.Combine(directory, "upgrade-backup")); hash = PasswordHasher.Hash("Test-only-password-123");
    }
    [SetUp]
    public void Reset()
    {
        Execute("DROP TRIGGER IF EXISTS dbo.GuideBlockAudit; DELETE FROM dbo.PaymentLedger; DELETE FROM dbo.AuditLog; DELETE FROM dbo.izinler; DELETE FROM dbo.Calisan; DELETE FROM dbo.Joker; DELETE FROM dbo.admin;");
        using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand("INSERT INTO dbo.admin(username,password,password_hash,RoleName) VALUES(N'test-admin',N'',@hash,N'Admin')", c); cmd.Text("@hash", hash, 256); cmd.ExecuteNonQuery();
    }
    [OneTimeTearDown]
    public void Drop()
    {
        if (created) { SqlConnection.ClearAllPools(); using var c = new SqlConnection(Master); c.Open(); using var cmd = new SqlCommand($"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]", c); cmd.ExecuteNonQuery(); }
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
    private void Execute(string sql) { using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand(sql, c); cmd.ExecuteNonQuery(); }
    private object Scalar(string sql) { using var c = new SqlConnection(connection); c.Open(); using var cmd = new SqlCommand(sql, c); return cmd.ExecuteScalar()!; }
    [Test, Category("Migrations")]
    public async Task StartupUpgradeIsRepeatableAndPreservesExistingRows()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); await SchemaUpgrade.PrepareAsync(connection); await SchemaUpgrade.PrepareAsync(connection);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.__MigrationJournal"), Is.EqualTo(1)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.EqualTo(1));
    }
    [Test, Category("Migrations")]
    public async Task UnversionedLegacyDatabaseUpgradesWithoutFalseBaselineOrDataLoss()
    {
        string legacyName = "AccountingAppGuideRegression_" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(directory, "legacy"); Directory.CreateDirectory(folder);
        var settings = new DatabaseSettings(@"(LocalDB)\MSSQLLocalDB", legacyName, Path.Combine(folder, "legacy.mdf"), 30);
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
        var table = await accounts.ListAsync(admin, default); Assert.That(table.Columns.Cast<DataColumn>().Select(c => c.ColumnName), Is.EquivalentTo(new[] { "username", "RoleName" }));
    }
    [Test, Category("Roles")]
    public async Task LastAdminCannotBeDeletedEvenWhenViewerExists()
    {
        var accounts = new AccountService(connection); await accounts.AddAsync(admin, "viewer", "Test-only-password-123", UserRole.Viewer, default);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await accounts.DeleteAsync(new("other-admin", UserRole.Admin), "test-admin", default));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin WHERE RoleName=N'Admin'"), Is.EqualTo(1));
    }
    [Test, Category("Personnel")]
    public async Task AsyncCrudSearchAndLedgerStayConsistent()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); await Service.SaveAsync(PersonnelGroup.General, Person(amount: 125m), false, expectedAmount: 100m);
        var data = await Service.ListAsync(null, default, "11111111111", "Ada", "Test"); Assert.That(data.Rows.Count, Is.EqualTo(1)); Assert.That(data.Rows[0]["PARA"], Is.EqualTo(125m));
        Assert.That(data.Rows[0].HasVersion(DataRowVersion.Original), Is.True);
        Assert.That((await Service.ListAsync(null, default, "", "%", "")).Rows.Count, Is.Zero); // wildcard treated literally
        await Service.DeleteAsync(PersonnelGroup.General, Person().Tc); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.Calisan"), Is.Zero);
        Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(0m)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(3));
    }
    [Test, Category("Payments")]
    public async Task StalePersonnelEditCannotUndoAnInterveningPayment()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveAsync(PersonnelGroup.General, Person(), false, expectedAmount: 100m));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(70m)); Assert.That(Scalar("SELECT SUM(Delta) FROM dbo.PaymentLedger"), Is.EqualTo(70m));
    }
    [Test, Category("Leaves")]
    public async Task BulkLeavePlanRollsBackIfAnyPersonIsInvalid()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true); var leave = new LeavePeriod(new(2026, 10, 24), new(2026, 10, 26));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.SaveLeavesAsync([(Person().Tc, leave), (22222222222L, leave)]));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        await Service.SaveLeavesAsync([(Person().Tc, leave)]); Assert.That(Scalar("SELECT IzinSuresi FROM dbo.izinler"), Is.EqualTo(3));
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
    [Test, Category("Ledger")]
    public async Task ReversalRestoresBalanceExactlyOnceAndRetainsHistory()
    {
        await Service.SaveAsync(PersonnelGroup.Joker, Person(), true); Guid request = Guid.NewGuid(); await Service.PayAsync(PersonnelGroup.Joker, Person().Tc, 30m, request);
        await Service.ReversePaymentAsync(request); await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.ReversePaymentAsync(request));
        Assert.That(Scalar("SELECT PARA FROM dbo.Joker"), Is.EqualTo(100m)); var ledger = await Service.LedgerAsync(); Assert.That(ledger.Rows[^1]["RunningBalance"], Is.EqualTo(100m));
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
        string path = await backup.BackupAsync(Path.Combine(directory, "restore-source")); await Service.DeleteAsync(PersonnelGroup.General, Person().Tc);
        await backup.RestoreAsync(path, Path.Combine(directory, "restore-safety")); Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Restore'"), Is.EqualTo(1));
    }
}
