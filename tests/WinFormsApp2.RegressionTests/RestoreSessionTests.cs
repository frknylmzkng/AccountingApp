using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [TestCase(false), TestCase(true), Category("FullRestore")]
    public async Task RestoreSchemaFailureNeverRevivesBackupSessionsEvenOnRepeatedRestore(bool brokenMigration)
    {
        var backupAdmin = admin;
        var backupViewer = CreateActor("backup-viewer", UserRole.Viewer);
        string folder = Path.Combine(directory, "restore-schema-" + Guid.NewGuid().ToString("N"));
        void Repair()
        {
            if (brokenMigration) Execute("ALTER TABLE dbo.izinler DROP COLUMN LeaveVersion;");
            else Execute("DELETE dbo.__MigrationJournal WHERE ScriptName=N'future-restore-test';");
        }
        if (brokenMigration)
        {
            RemoveLeaveVersionAndDependentIndexes();
            Execute("ALTER TABLE dbo.izinler ADD LeaveVersion binary(8) NULL;");
        }
        else Execute("INSERT dbo.__MigrationJournal(ScriptName,Applied) VALUES(N'future-restore-test',GETUTCDATE());");
        try
        {
            string path = await new BackupService(connection, admin).BackupAsync(folder);
            Repair();
            await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder);
            Execute("UPDATE dbo.admin SET SessionEpoch=NEWID();");
            admin = SessionFor("test-admin");
            Assert.That(await AccountAuthorization.IsCurrentAsync(connection, backupAdmin, default), Is.False);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var caller = admin;
                await Assert.ThrowsAsync<InvalidOperationException>(() => new BackupService(connection, caller).RestoreAsync(path, folder));
                Assert.That(await AccountAuthorization.IsCurrentAsync(connection, backupAdmin, default), Is.False);
                Assert.That(await AccountAuthorization.IsCurrentAsync(connection, backupViewer, default), Is.False);
                Assert.That(await AccountAuthorization.IsCurrentAsync(connection, caller, default), Is.False);
                await Assert.ThrowsAsync<SessionRevokedException>(() => new PersonnelService(connection, backupAdmin).ListAsync(null));
                await Assert.ThrowsAsync<SessionRevokedException>(() => new PersonnelService(connection, backupViewer).ListAsync(null));
                Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Backup.Restore'"), Is.Zero);
                admin = SessionFor("test-admin");
                Assert.That(admin.SecurityEpoch, Is.Not.EqualTo(caller.SecurityEpoch));
                Repair();
                await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder);
                // Startup retry must not restore any previous security epoch.
                Assert.That(await AccountAuthorization.IsCurrentAsync(connection, backupAdmin, default), Is.False);
            }
        }
        finally
        {
            if (brokenMigration)
            {
                Execute("IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.izinler') AND name=N'LeaveVersion' AND system_type_id<>189) ALTER TABLE dbo.izinler DROP COLUMN LeaveVersion;");
            }
            else Execute("DELETE dbo.__MigrationJournal WHERE ScriptName=N'future-restore-test';");
            await SchemaUpgrade.PrepareAsync(connection, safetyFolder: folder);
        }
    }

    [Test, Category("FullRestore")]
    public async Task RestoreRevocationFailureKeepsDatabaseOfflineInsteadOfExposingOldSessions()
    {
        string folder = Path.Combine(directory, "restore-revoke-failure");
        Execute("CREATE TRIGGER dbo.GuideBlockRevocation ON dbo.admin AFTER UPDATE AS THROW 51099,N'Test revocation failure',1;");
        try
        {
            var service = new BackupService(connection, admin);
            string path = await service.BackupAsync(folder);
            await Assert.ThrowsAsync<SqlException>(() => service.RestoreAsync(path, folder));
            using var master = new SqlConnection(Master); master.Open();
            using var state = new SqlCommand("SELECT state_desc FROM sys.databases WHERE name=@name", master);
            state.Text("@name", name, 128);
            Assert.That(state.ExecuteScalar(), Is.EqualTo("OFFLINE"));
            await Assert.ThrowsAsync<SqlException>(() => AccountAuthorization.IsCurrentAsync(connection, admin, default));
        }
        finally
        {
            using var master = new SqlConnection(Master); master.Open();
            using var online = new SqlCommand($"ALTER DATABASE [{name}] SET ONLINE; ALTER DATABASE [{name}] SET MULTI_USER WITH ROLLBACK IMMEDIATE;", master);
            online.ExecuteNonQuery();
            // The deliberate OFFLINE login failure is cached by SqlClient's pool.
            SqlConnection.ClearAllPools();
            Execute("DROP TRIGGER IF EXISTS dbo.GuideBlockRevocation; UPDATE dbo.admin SET SessionEpoch=NEWID();");
            admin = SessionFor("test-admin");
        }
    }
}
