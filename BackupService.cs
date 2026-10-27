using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class BackupService(string connectionString, UserSession user)
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private SqlConnectionStringBuilder Builder => new(connectionString);
    private string Catalog => Builder.InitialCatalog;
    internal static string Prefix(string databaseName) => "AccountingApp-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(databaseName.ToUpperInvariant())))[..16] + "-";
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
        Directory.CreateDirectory(folder); string path = Path.GetFullPath(Path.Combine(folder, Prefix(name) + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")[..12] + ".bak"));
        if (path.Length > 259) throw new ArgumentException("SQL Server yedek yolu 259 karakteri geçemez. Daha kısa bir yedek klasörü seçin.");
        await using var c = new SqlConnection(Master()); await c.OpenAsync(token).ConfigureAwait(false);
        using var cmd = new SqlCommand($"BACKUP DATABASE {Identifier(name)} TO DISK={Literal(path)} WITH COPY_ONLY,INIT,CHECKSUM", c) { CommandTimeout = 180 };
        try
        {
            await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(path)} WITH CHECKSUM", c) { CommandTimeout = 180 }; await verify.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            AtomicFile.Write(path + ".verified", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        // Local success is recorded independently of the optional external copy.
        await RecordAuditAsync("Backup.Create", token).ConfigureAwait(false);
        Prune(folder, Prefix(name), 14, protectedBackupPath);
        if (!string.IsNullOrWhiteSpace(mirror)) await MirrorCoreAsync(path, mirror, name, token).ConfigureAwait(false);
        return path;
    }
    internal static void Prune(string folder, string prefix, int keep, string? protectedBackupPath = null)
    {
        if (keep < 1 || !System.Text.RegularExpressions.Regex.IsMatch(prefix, "\\AAccountingApp-[0-9A-F]{16}-\\z")) throw new ArgumentException("Yedek kapsamı geçersiz.");
        string root = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string? protectedPath = protectedBackupPath == null ? null : Path.GetFullPath(protectedBackupPath);
        foreach (var path in Directory.EnumerateFiles(root, prefix + "*.bak").OrderByDescending(Path.GetFileName, StringComparer.Ordinal).Skip(keep))
        {
            string absolute = Path.GetFullPath(path);
            if (string.Equals(absolute, protectedPath, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(Path.GetDirectoryName(absolute), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) && (File.GetAttributes(absolute) & FileAttributes.ReparsePoint) == 0)
            {
                File.Delete(absolute);
                if (File.Exists(absolute + ".verified")) File.Delete(absolute + ".verified");
                if (File.Exists(absolute + ".mirror.json")) File.Delete(absolute + ".mirror.json");
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
            string? recent = LatestVerified(folder, Prefix(name), DateTime.UtcNow.AddHours(-24));
            if (recent == null) { await BackupCoreAsync(folder, mirror, null, token).ConfigureAwait(false); return true; }
            // A local success must not suppress retrying a missing/failed mirror.
            if (!string.IsNullOrWhiteSpace(mirror)) await MirrorCoreAsync(recent, mirror, name, token).ConfigureAwait(false);
            return false; // No new local backup was necessary, even if its mirror was repaired.
        }
        finally { Gate.Release(); }
    }
    public async Task RestoreAsync(string backupPath, string safetyFolder, CancellationToken token = default)
    {
        user.DemandAdmin();
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
            string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
            backupPath = Path.GetFullPath(backupPath);
            if (!File.Exists(backupPath) || !Path.GetFileName(backupPath).StartsWith(Prefix(name), StringComparison.Ordinal) || !backupPath.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Bu veritabanına ait bir uygulama yedeği seçin.");
            SqlConnection.ClearAllPools(); await using var master = new SqlConnection(Master()); await master.OpenAsync(token).ConfigureAwait(false);
            using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(Path.GetFullPath(backupPath))} WITH CHECKSUM", master) { CommandTimeout = 180 }; await verify.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            using var header = new SqlCommand($"RESTORE HEADERONLY FROM DISK={Literal(Path.GetFullPath(backupPath))}", master);
            var headers = await header.TableAsync(token).ConfigureAwait(false);
            if (headers.Rows.Count != 1 || !string.Equals(headers.Rows[0]["DatabaseName"].ToString(), name, StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(headers.Rows[0]["BackupType"]) != 1) throw new InvalidOperationException("Yedek başka bir veritabanına ait veya tam yedek değil.");
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
                if (single) { using var multi = new SqlCommand($"ALTER DATABASE {Identifier(name)} SET MULTI_USER WITH ROLLBACK IMMEDIATE", master); await multi.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false); }
                SqlConnection.ClearAllPools();
            }
            await SchemaUpgrade.PrepareAsync(connectionString, CancellationToken.None, safetyFolder, backupPath).ConfigureAwait(false);
            await RecordAuditAsync("Backup.Restore", CancellationToken.None, revokeSessions: true).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }
    private string Master() { var b = Builder; b.InitialCatalog = "master"; b.AttachDBFilename = ""; b.Pooling = false; return b.ConnectionString; }
    private static void ValidateCatalog(string name) { if (string.IsNullOrWhiteSpace(name) || new[] { "master", "model", "msdb", "tempdb" }.Contains(name, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Uygulama veritabanı seçilmedi."); }
    private async Task RecordAuditAsync(string action, CancellationToken token, bool revokeSessions = false)
    {
        if (revokeSessions)
        {
            // Restoring a backup can rewind rowversion counters and account rows.
            // A new random epoch prevents sessions from before the restore reviving.
            // Commit revocation before audit: a failed audit must not revive sessions.
            await using var revocation = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
            using var revoke = revocation.Command("UPDATE dbo.admin SET SessionEpoch=NEWID()");
            using var throttle = revocation.Command("IF OBJECT_ID(N'dbo.LoginThrottle') IS NOT NULL DELETE FROM dbo.LoginThrottle");
            await revoke.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await throttle.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            await revocation.CommitAsync(token).ConfigureAwait(false);
        }
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, action, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }
}
