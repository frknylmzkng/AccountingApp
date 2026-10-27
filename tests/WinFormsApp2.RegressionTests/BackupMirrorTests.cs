using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    private (string Local, string Mirror) MirrorFolders()
    {
        string root = Path.Combine(directory, "mirror-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); return (Path.Combine(root, "local"), Path.Combine(root, "external"));
    }
    private static string Digest(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
    private static BackupMirrorState MirrorState(string file) => JsonSerializer.Deserialize<BackupMirrorState>(File.ReadAllText(file + ".mirror.json"))!;
    private int BackupAudit(string action) => Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'" + action + "'"));

    [Test, Category("Backup")]
    public async Task FailedMirrorIsRetriedInsideTwentyFourHoursWithoutAnotherLocalBackup()
    {
        var paths = MirrorFolders(); File.WriteAllText(paths.Mirror, "Unavailable drive simulated by a file");
        var service = new BackupService(connection, admin);
        var error = await Assert.ThrowsAsync<BackupMirrorException>(() => service.EnsureDailyAsync(paths.Local, paths.Mirror));
        string local = error!.LocalBackupPath, hash = Digest(local); DateTime verified = File.GetLastWriteTimeUtc(local + ".verified");
        Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1)); Assert.That(MirrorState(local).Failure, Is.Not.Null);
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("Harici kopya bekliyor"));
        File.Delete(paths.Mirror);
        // A new instance simulates a later scheduled check or application restart.
        Assert.That(await new BackupService(connection, admin).EnsureDailyAsync(paths.Local, paths.Mirror), Is.False);
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(Digest(local), Is.EqualTo(hash)); Assert.That(File.GetLastWriteTimeUtc(local + ".verified"), Is.EqualTo(verified));
        Assert.That(Digest(Path.Combine(paths.Mirror, Path.GetFileName(local))), Is.EqualTo(hash));
        Assert.That(MirrorState(local).CompletedUtc, Is.Not.Null); Assert.That(MirrorState(local).Failure, Is.Null);
        Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1)); Assert.That(BackupAudit("Backup.Mirror"), Is.EqualTo(1));
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("Son başarılı aynalama"));
        DateTime completed = MirrorState(local).CompletedUtc!.Value;
        Assert.That(await service.EnsureDailyAsync(paths.Local, paths.Mirror), Is.False);
        Assert.That(MirrorState(local).CompletedUtc, Is.EqualTo(completed)); Assert.That(BackupAudit("Backup.Mirror"), Is.EqualTo(1));
    }
    [Test, Category("Backup")]
    public async Task ManualBackupPartialSuccessCanBeMirroredWithoutTakingAnotherBackup()
    {
        var paths = MirrorFolders(); File.WriteAllText(paths.Mirror, "blocked"); var service = new BackupService(connection, admin);
        var error = await Assert.ThrowsAsync<BackupMirrorException>(() => service.BackupAsync(paths.Local, paths.Mirror));
        File.Delete(paths.Mirror); await service.RetryMirrorAsync(paths.Local, paths.Mirror);
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(File.Exists(Path.Combine(paths.Mirror, Path.GetFileName(error!.LocalBackupPath))), Is.True);
        Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1));
    }
    [TestCase(false), TestCase(true), Category("Backup")]
    public async Task DeletedOrCorruptMirrorIsRepairedDespiteCompletedMarker(bool corrupt)
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local, paths.Mirror);
        string target = Path.Combine(paths.Mirror, Path.GetFileName(local));
        if (corrupt) File.WriteAllText(target, "corrupt"); else File.Delete(target);
        await service.EnsureDailyAsync(paths.Local, paths.Mirror);
        Assert.That(Digest(target), Is.EqualTo(Digest(local))); Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(BackupAudit("Backup.Mirror"), Is.EqualTo(2));
    }
    [Test, Category("Backup")]
    public async Task FailedAtomicReplacementPreservesTargetAndRemovesTemporaryCopy()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local, paths.Mirror);
        string target = Path.Combine(paths.Mirror, Path.GetFileName(local)); File.WriteAllText(target, "existing target");
        using (var locked = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            await Assert.ThrowsAsync<BackupMirrorException>(() => service.EnsureDailyAsync(paths.Local, paths.Mirror));
            Assert.That(File.ReadAllText(target), Is.EqualTo("existing target"));
            Assert.That(Directory.GetFiles(paths.Mirror, "*.copying"), Is.Empty);
        }
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("Harici kopya bekliyor").And.Contain("Son başarılı aynalama"));
        await service.RetryMirrorAsync(paths.Local, paths.Mirror); Assert.That(Digest(target), Is.EqualTo(Digest(local)));
    }
    [Test, Category("Backup")]
    public async Task ChangingMirrorDestinationRetriesOnlyToCurrentDestination()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local, paths.Mirror);
        string other = paths.Mirror + "-new"; Assert.That(await service.StatusAsync(paths.Local, other), Does.Contain("Harici kopya bekliyor"));
        await service.EnsureDailyAsync(paths.Local, other);
        Assert.That(Digest(Path.Combine(other, Path.GetFileName(local))), Is.EqualTo(Digest(local)));
        Assert.That(MirrorState(local).Destination, Is.EqualTo(other)); Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1));
    }
    [TestCase("missing"), TestCase("corrupt"), TestCase("pending"), Category("Backup")]
    public async Task LegacyMissingOrInterruptedMirrorStateCanRecover(string kind)
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local);
        if (kind == "corrupt") File.WriteAllText(local + ".mirror.json", "invalid json");
        if (kind == "pending") File.WriteAllText(local + ".mirror.json", JsonSerializer.Serialize(new BackupMirrorState(paths.Mirror, null, DateTime.UtcNow, null, "Pending")));
        await service.EnsureDailyAsync(paths.Local, paths.Mirror);
        Assert.That(MirrorState(local).Failure, Is.Null); Assert.That(Digest(Path.Combine(paths.Mirror, Path.GetFileName(local))), Is.EqualTo(Digest(local)));
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
    }
    [Test, Category("Backup")]
    public async Task ConcurrentDailyChecksCreateOneLocalAndOneMirror()
    {
        var paths = MirrorFolders(); var checks = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => new BackupService(connection, admin).EnsureDailyAsync(paths.Local, paths.Mirror)));
        Assert.That(checks.Count(b => b), Is.EqualTo(1)); Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(paths.Mirror, "*.bak"), Has.Length.EqualTo(1)); Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1)); Assert.That(BackupAudit("Backup.Mirror"), Is.EqualTo(1));
    }
    [Test, Category("Backup")]
    public async Task MirrorRetryDoesNotExtendLocalBackupAgeAndNextDayStillCreatesNewBackup()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string old = await service.BackupAsync(paths.Local);
        var expired = DateTime.UtcNow.AddHours(-25); File.SetLastWriteTimeUtc(old + ".verified", expired);
        await service.RetryMirrorAsync(paths.Local, paths.Mirror);
        Assert.That(File.GetLastWriteTimeUtc(old + ".verified"), Is.EqualTo(expired));
        Assert.That(await service.EnsureDailyAsync(paths.Local, paths.Mirror), Is.True);
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(2)); Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(2));
    }
    [Test, Category("Backup")]
    public async Task CorruptedLocalBackupIsNotCopiedAndExistingMirrorSurvives()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local, paths.Mirror);
        string target = Path.Combine(paths.Mirror, Path.GetFileName(local)), hash = Digest(target); File.WriteAllText(local, "corrupt SQL backup");
        await Assert.ThrowsAsync<SqlException>(() => service.RetryMirrorAsync(paths.Local, paths.Mirror));
        Assert.That(Digest(target), Is.EqualTo(hash)); Assert.That(MirrorState(local).Failure, Is.EqualTo("SourceVerificationFailed"));
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("yeni yerel yedek oluşturun"));
    }
    [Test, Category("Backup")]
    public async Task MirrorAuditFailureIsPendingAndRetryDoesNotDuplicateLocalBackup()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local);
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Backup.Mirror') THROW 51099,N'Mirror audit test',1;");
        await Assert.ThrowsAsync<SqlException>(() => service.RetryMirrorAsync(paths.Local, paths.Mirror));
        Assert.That(MirrorState(local).Failure, Is.EqualTo("AuditFailed"));
        Execute("DROP TRIGGER dbo.GuideBlockAudit"); await service.RetryMirrorAsync(paths.Local, paths.Mirror);
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1)); Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1)); Assert.That(BackupAudit("Backup.Mirror"), Is.EqualTo(1));
    }
    [TestCase(UserRole.Viewer), TestCase(UserRole.PersonnelManager), Category("Backup")]
    public async Task NonAdminCannotRetryDailyOrViewBackupStatus(UserRole role)
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, CreateActor("non-backup-admin", role));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.EnsureDailyAsync(paths.Local, paths.Mirror));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RetryMirrorAsync(paths.Local, paths.Mirror));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.StatusAsync(paths.Local, paths.Mirror));
        Assert.That(Directory.Exists(paths.Local), Is.False); Assert.That(Directory.Exists(paths.Mirror), Is.False);
    }
    [Test, Category("Backup")]
    public async Task RevokedAdminCannotRetryEvenAnAlreadyCompletedMirror()
    {
        var paths = MirrorFolders(); var actor = CreateActor("backup-admin", UserRole.Admin); var service = new BackupService(connection, actor);
        string local = await service.BackupAsync(paths.Local, paths.Mirror); string state = File.ReadAllText(local + ".mirror.json");
        Execute("UPDATE dbo.admin SET IsActive=0 WHERE username=N'backup-admin'");
        await Assert.ThrowsAsync<SessionRevokedException>(() => service.RetryMirrorAsync(paths.Local, paths.Mirror));
        await Assert.ThrowsAsync<SessionRevokedException>(() => service.EnsureDailyAsync(paths.Local, paths.Mirror));
        Assert.That(File.ReadAllText(local + ".mirror.json"), Is.EqualTo(state));
    }
    [Test, Category("Backup")]
    public async Task CancelledRetryPreservesLocalAndSuccessfulMirrorState()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local, paths.Mirror);
        string state = File.ReadAllText(local + ".mirror.json"); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.That(async () => await service.RetryMirrorAsync(paths.Local, paths.Mirror, cancel.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(File.ReadAllText(local + ".mirror.json"), Is.EqualTo(state)); Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1));
    }
    [Test, Category("Backup")]
    public async Task MirrorRetryWithNoLocalBackupOrNoDestinationCreatesNothing()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RetryMirrorAsync(paths.Local, paths.Mirror));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RetryMirrorAsync(paths.Local, ""));
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("yerel yedek bulunamadı"));
        Assert.That(Directory.Exists(paths.Local), Is.False); Assert.That(Directory.Exists(paths.Mirror), Is.False);
    }
    [Test, Category("Backup")]
    public async Task DisabledMirrorAndSameFolderAreHandledWithoutDuplicateLocalBackups()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin);
        Assert.That(await service.EnsureDailyAsync(paths.Local, ""), Is.True);
        Assert.That(await service.EnsureDailyAsync(paths.Local, ""), Is.False);
        Assert.That(await service.StatusAsync(paths.Local, ""), Does.Contain("aynalama seçilmemiş"));
        string local = Directory.GetFiles(paths.Local, "*.bak").Single(), hash = Digest(local);
        await service.RetryMirrorAsync(paths.Local, Path.Combine(paths.Local, "."));
        Assert.That(Digest(local), Is.EqualTo(hash)); Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(await service.StatusAsync(paths.Local, paths.Local), Does.Contain("ayrı bir harici kopya oluşturulmadı"));
    }
    [Test, Category("Backup")]
    public async Task StatusReadDoesNotCopyBackupsOrWriteAudit()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local);
        int audit = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"));
        Assert.That(await service.StatusAsync(paths.Local, paths.Mirror), Does.Contain("Harici kopya bekliyor"));
        Assert.That(Directory.Exists(paths.Mirror), Is.False); Assert.That(File.Exists(local + ".mirror.json"), Is.False);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog"), Is.EqualTo(audit));
    }
    [Test, Category("Backup")]
    public async Task RepeatedUnavailableMirrorChecksKeepOneVerifiedLocalBackup()
    {
        var paths = MirrorFolders(); File.WriteAllText(paths.Mirror, "unavailable"); var service = new BackupService(connection, admin);
        string? original = null;
        for (int i = 0; i < 3; i++)
        {
            var error = await Assert.ThrowsAsync<BackupMirrorException>(() => service.EnsureDailyAsync(paths.Local, paths.Mirror));
            original ??= error!.LocalBackupPath; Assert.That(error!.LocalBackupPath, Is.EqualTo(original));
        }
        Assert.That(Directory.GetFiles(paths.Local, "*.bak"), Has.Length.EqualTo(1));
        Assert.That(File.Exists(original + ".verified"), Is.True); Assert.That(MirrorState(original!).Failure, Is.Not.Null);
        Assert.That(BackupAudit("Backup.Create"), Is.EqualTo(1)); Assert.That(BackupAudit("Backup.Mirror"), Is.Zero);
    }
    [Test, Category("Backup")]
    public async Task RetryingOlderBackupPreservesPublishedCopyDuringMirrorRetention()
    {
        var paths = MirrorFolders(); var service = new BackupService(connection, admin); string local = await service.BackupAsync(paths.Local);
        Directory.CreateDirectory(paths.Mirror);
        for (int i = 0; i < 14; i++) File.WriteAllText(Path.Combine(paths.Mirror, BackupService.Prefix(name) + "9999-" + i.ToString("D2") + ".bak"), "retention fixture");
        await service.RetryMirrorAsync(paths.Local, paths.Mirror);
        string target = Path.Combine(paths.Mirror, Path.GetFileName(local));
        Assert.That(File.Exists(target), Is.True); Assert.That(Digest(target), Is.EqualTo(Digest(local)));
        Assert.That(MirrorState(local).CompletedUtc, Is.Not.Null); Assert.That(Directory.GetFiles(paths.Mirror, "*.bak"), Has.Length.EqualTo(15));
    }
}

[TestFixture, Category("Backup")]
public class BackupMirrorRetentionTests
{
    [Test]
    public void RetentionRemovesOwnMirrorMetadataButProtectsSelectedBackupMetadata()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "mirror-retention-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string prefix = BackupService.Prefix("MirrorRetention");
            foreach (string number in new[] { "0000", "0001", "0002", "0003" })
            {
                string path = Path.Combine(root, prefix + number + ".bak"); File.WriteAllText(path, "backup"); File.WriteAllText(path + ".verified", "verified"); File.WriteAllText(path + ".mirror.json", "state");
            }
            string selected = Path.Combine(root, prefix + "0000.bak"), deleted = Path.Combine(root, prefix + "0001.bak");
            BackupService.Prune(root, prefix, 2, selected);
            Assert.That(File.Exists(selected + ".mirror.json"), Is.True); Assert.That(File.Exists(selected + ".verified"), Is.True);
            Assert.That(File.Exists(deleted), Is.False); Assert.That(File.Exists(deleted + ".mirror.json"), Is.False); Assert.That(File.Exists(deleted + ".verified"), Is.False);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
