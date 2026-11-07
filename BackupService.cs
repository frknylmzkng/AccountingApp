using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class BackupService(string connectionString, UserSession user)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private SqlConnectionStringBuilder Builder => new(connectionString);
    private string Catalog => Builder.InitialCatalog;
    private static string Literal(string value) => "N'" + value.Replace("'", "''") + "'";
    private static string Identifier(string value) => "[" + value.Replace("]", "]]") + "]";
    internal static string DefaultFolder => Path.Combine(UserPreferences.DirectoryPath, "backups");
    public async Task<string> CatalogAsync(CancellationToken token)
    {
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(token).ConfigureAwait(false);
        using var cmd = new SqlCommand("SELECT DB_NAME()", c); return (string)(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))!;
    }
    public async Task<string> BackupAsync(string folder, string mirror = "", CancellationToken token = default)
    {
        user.DemandAdmin(); await Gate.WaitAsync(token).ConfigureAwait(false);
        try { return await BackupCoreAsync(folder, mirror, null, token).ConfigureAwait(false); }
        finally { Gate.Release(); }
    }
    // Caller holds Gate; restore uses the same gate for validation, safety backup and restore.
    private async Task<string> BackupCoreAsync(string folder, string mirror, string? protectedBackupPath, CancellationToken token)
    {
        await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
        string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
        var scope = await BackupScope.ReadAsync(connectionString, token).ConfigureAwait(false);
        if(BackupVault.Policy(scope.Key).Mirror is{Length:>0} configuredMirror)mirror=configuredMirror;
        if(!Builder.DataSource.StartsWith(@"(LocalDB)\",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Şifreli uygulama yedeği aynı Windows hesabının LocalDB sunucusunu gerektirir. Ayrı SQL Server için servis hesabına özel yedekleme kurulumu kullanın.");
        using var stage=new BackupVault.Stage();
        Directory.CreateDirectory(folder); string path = Path.GetFullPath(Path.Combine(stage.Folder, scope.Prefix + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")[..12] + ".bak"));
        if (path.Length > 259) throw new ArgumentException("SQL Server yedek yolu 259 karakteri geçemez. Daha kısa bir yedek klasörü seçin.");
        await using var c = new SqlConnection(Master()); await c.OpenAsync(token).ConfigureAwait(false);
        using var cmd = new SqlCommand($"BACKUP DATABASE {Identifier(name)} TO DISK={Literal(path)} WITH COPY_ONLY,INIT,CHECKSUM,NAME={Literal(scope.BackupName)}", c) { CommandTimeout = 180 };
        try
        {
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(path)} WITH CHECKSUM", c) { CommandTimeout = 180 }; await verify.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await scope.WriteManifestAsync(path, c, token).ConfigureAwait(false);
            AtomicFile.Write(path + ".verified", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        path=await BackupVault.SealAsync(path,folder,scope,token).ConfigureAwait(false);
        // Local success is recorded independently of the optional external copy.
        await RecordAuditAsync("Backup.Create", token).ConfigureAwait(false);
        Prune(folder, scope.Prefix, BackupVault.Policy(scope.Key).Keep, protectedBackupPath);
        if (!string.IsNullOrWhiteSpace(mirror)) await MirrorCoreAsync(path, mirror, scope, token).ConfigureAwait(false);
        return path;
    }
    internal static void Prune(string folder, string prefix, int keep, string? protectedBackupPath = null)
    {
        if (keep < 1 || !System.Text.RegularExpressions.Regex.IsMatch(prefix, "\\AAccountingApp-v2-[0-9A-F]{32}-\\z")) throw new ArgumentException("Yedek kapsamı geçersiz.");
        if(!BackupVault.KeyStatus().ExportedUtc.HasValue)return; // Never prune before a recovery-key export is recorded.
        string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string? protectedPath = protectedBackupPath == null ? null : Path.GetFullPath(protectedBackupPath);
        foreach (var path in Directory.EnumerateFiles(root, prefix + "*.bak.abk").Where(p => BackupVault.Owns(p,prefix)).OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(keep))
        {
            string absolute = Path.GetFullPath(path);
            if (string.Equals(absolute, protectedPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(Path.GetDirectoryName(absolute), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(absolute) & FileAttributes.ReparsePoint) == 0)
            {
                try{File.Delete(absolute);}catch(IOException){continue;} // An in-use recovery source is retained.
                if (File.Exists(absolute + ".verified")) File.Delete(absolute + ".verified");
                if (File.Exists(absolute + ".mirror.json")) File.Delete(absolute + ".mirror.json");
                File.Delete(absolute + BackupScope.ManifestSuffix);
            }
        }
    }
    public Task<bool> EnsureDailyAsync(CancellationToken token = default)
    {
        var preferences = UserPreferences.Load();
        return EnsureDailyAsync(string.IsNullOrWhiteSpace(preferences.BackupFolder) ? DefaultFolder : preferences.BackupFolder, preferences.MirrorFolder, token);
    }
    internal async Task<bool> EnsureDailyAsync(string folder, string mirror, CancellationToken token = default)
    {
        user.DemandAdmin(); await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
            string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
            var scope = await BackupScope.ReadAsync(connectionString, token).ConfigureAwait(false);
            if(BackupVault.Policy(scope.Key).Mirror is{Length:>0} configuredMirror)mirror=configuredMirror;
            string? recent = LatestVerified(folder, scope, DateTime.UtcNow.AddHours(-24));
            if (recent == null) { await BackupCoreAsync(folder, mirror, null, token).ConfigureAwait(false); return true; }
            // A local success must not suppress retrying a missing/failed mirror.
            if (!string.IsNullOrWhiteSpace(mirror)) await MirrorCoreAsync(recent, mirror, scope, token).ConfigureAwait(false);
            return false; // No new local backup was necessary, even if its mirror was repaired.
        }
        finally { Gate.Release(); }
    }
    public async Task RestoreAsync(string backupPath, string safetyFolder, CancellationToken token = default)
    {
        if(backupPath.EndsWith(".abk",StringComparison.OrdinalIgnoreCase))
        {
            await AccountAuthorization.ValidateAsync(connectionString,user,AccountAccess.Admin,token).ConfigureAwait(false);
            var scope=await BackupScope.ReadAsync(connectionString,token).ConfigureAwait(false);
            using var selectedLease=new FileStream(backupPath,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var opened=await BackupVault.OpenAsync(backupPath,scope,token).ConfigureAwait(false);
            await RestoreLegacyAsync(opened.BackupPath!,safetyFolder,token).ConfigureAwait(false);return;
        }
        await RestoreLegacyAsync(backupPath,safetyFolder,token).ConfigureAwait(false);
    }
    private async Task RestoreLegacyAsync(string backupPath, string safetyFolder, CancellationToken token)
    {
        user.DemandAdmin();
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
            string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
            var scope = await BackupScope.ReadAsync(connectionString, token).ConfigureAwait(false);
            backupPath = Path.GetFullPath(backupPath);
            if (!scope.Owns(backupPath)) throw new InvalidOperationException("Yedek kimliği/kapsamı doğrulanamadı. Bu hedefin v2 yedeğini ve .backup.json dosyasını birlikte seçin. Eski kimliksiz yedekler otomatik kabul edilmez; ayrı doğrulama/kurtarma gerekir.");
            // Prevent replacement/deletion between identity verification and RESTORE.
            await using var sourceLease = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            SqlConnection.ClearAllPools(); await using var master = new SqlConnection(Master()); await master.OpenAsync(token).ConfigureAwait(false);
            using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(Path.GetFullPath(backupPath))} WITH CHECKSUM", master) { CommandTimeout = 180 }; await verify.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            using var header = new SqlCommand($"RESTORE HEADERONLY FROM DISK={Literal(Path.GetFullPath(backupPath))}", master);
            var headers = await header.TableAsync(token).ConfigureAwait(false);
            var backupHeader = scope.ValidateHeader(headers);
            var manifest = BackupScope.ReadOwned(backupPath, scope.Prefix)!;
            if (manifest == null || manifest.BackupSetId != (Guid)backupHeader["BackupSetGUID"])
                throw new InvalidOperationException("Yedek manifesti ile SQL yedek kümesi eşleşmiyor.");
            using var files = new SqlCommand("SELECT type,physical_name FROM sys.master_files WHERE database_id=DB_ID(@name) ORDER BY type", master); files.Text("@name", name, 128); var destinations = await files.TableAsync(token).ConfigureAwait(false);
            using var source = new SqlCommand($"RESTORE FILELISTONLY FROM DISK={Literal(Path.GetFullPath(backupPath))}", master); var sources = await source.TableAsync(token).ConfigureAwait(false);
            if (destinations.Rows.Count != 2 || sources.Rows.Count != 2 || Convert.ToInt32(destinations.Rows[0]["type"]) != 0 || Convert.ToInt32(destinations.Rows[1]["type"]) != 1) throw new InvalidOperationException("Yalnızca tek veri ve tek günlük dosyalı uygulama yedeği desteklenir.");
            var data = sources.AsEnumerable().Single(r => r["Type"].ToString() == "D"); var log = sources.AsEnumerable().Single(r => r["Type"].ToString() == "L");
            // Validate the selected source before creating/pruning backups. Retain it
            // even when the safety folder is the source folder and retention is full.
            await BackupCoreAsync(safetyFolder, "", backupPath, token).ConfigureAwait(false);
            await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
            SqlConnection.ClearAllPools();
            bool single = false;
            try
            {
                using var mode = new SqlCommand($"ALTER DATABASE {Identifier(name)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE", master); await mode.ExecuteNonQueryAsync(token).ConfigureAwait(false); single = true;
                using var restore = new SqlCommand($"RESTORE DATABASE {Identifier(name)} FROM DISK={Literal(Path.GetFullPath(backupPath))} WITH REPLACE,CHECKSUM,RECOVERY,MOVE {Literal(data["LogicalName"].ToString()!)} TO {Literal(destinations.Rows[0]["physical_name"].ToString()!)},MOVE {Literal(log["LogicalName"].ToString()!)} TO {Literal(destinations.Rows[1]["physical_name"].ToString()!)}", master) { CommandTimeout = 300 };
                // Once restore starts it must finish; cancelling it can leave the database unavailable.
                await restore.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (single)
                {
                    // A restored journal may be incompatible. Revoke before schema checks
                    // and before reopening normal access, independently of the audit write.
                    try { await RevokeRestoredSessionsAsync(master, name).ConfigureAwait(false); }
                    catch
                    {
                        // Never expose restored credentials if revocation itself fails.
                        using var offline = new SqlCommand($"ALTER DATABASE {Identifier(name)} SET OFFLINE WITH ROLLBACK IMMEDIATE", master);
                        await offline.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                        throw;
                    }
                    using var multi = new SqlCommand($"ALTER DATABASE {Identifier(name)} SET MULTI_USER WITH ROLLBACK IMMEDIATE", master);
                    await multi.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                }
                SqlConnection.ClearAllPools();
            }
            await SchemaUpgrade.PrepareAsync(connectionString, CancellationToken.None, safetyFolder, backupPath).ConfigureAwait(false);
            await RecordAuditAsync("Backup.Restore", CancellationToken.None).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }
    private string Master() { var b = Builder; b.InitialCatalog = "master"; b.AttachDBFilename = ""; b.Pooling = false; return b.ConnectionString; }
    private static void ValidateCatalog(string name) { if (string.IsNullOrWhiteSpace(name) || new[] { "master", "model", "msdb", "tempdb" }.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Uygulama veritabanı seçilmedi."); }
    private static async Task RevokeRestoredSessionsAsync(SqlConnection master, string name)
    {
        // Dynamic scope returns this non-pooled connection to master. Legacy backups
        // without SessionEpoch cannot validate any current session; migration adds it.
        string sql = $@"USE {Identifier(name)};
            SET XACT_ABORT ON;
            IF OBJECT_ID(N'dbo.admin',N'U') IS NULL THROW 51025,N'Geri yüklenen hesap tablosu bulunamadı.',1;
            BEGIN TRANSACTION;
            IF COL_LENGTH(N'dbo.admin',N'SessionEpoch') IS NOT NULL EXEC(N'UPDATE dbo.admin SET SessionEpoch=NEWID()');
            IF OBJECT_ID(N'dbo.LoginThrottle',N'U') IS NOT NULL DELETE FROM dbo.LoginThrottle;
            COMMIT;";
        using var revoke = new SqlCommand("EXEC(" + Literal(sql) + ")", master);
        await revoke.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
    }
    private async Task RecordAuditAsync(string action, CancellationToken token)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        // Restore already authenticated before replacing the database and revokes
        // that very session. Its final maintenance audit must survive revocation.
        if(action!="Backup.Restore")await AccountAuthorization.DemandAsync(work,user,AccountAccess.Admin,token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, action, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }
}
