using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DbUp;
using DbUp.Engine;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal static class SchemaUpgrade
{
    public static async Task PrepareAsync(string connectionString, CancellationToken token = default, string? safetyFolder = null, string? protectedBackupPath = null)
    {
        await LocalDbRuntime.EnsureStartedAsync(connectionString, token).ConfigureAwait(false);
        // DbUp exposes a synchronous upgrade API. Keep this short, bounded schema operation off the UI thread.
        await Task.Run(() => Upgrade(connectionString, safetyFolder, protectedBackupPath), token).ConfigureAwait(false);
        await Task.Run(() => new AdminStore(connectionString).MigrateLegacyPasswords(), token).ConfigureAwait(false);
    }
    internal static void Upgrade(string connectionString, string? safetyFolder = null, string? protectedBackupPath = null)
    {
        using var connection = new SqlConnection(connectionString); connection.Open();
        using var gate = new SqlCommand(@"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AccountingApp.Features.v1',
            @LockMode='Exclusive',@LockOwner='Session',@LockTimeout=30000; IF @r<0 THROW 51020,N'Şema kilidi alınamadı.',1;", connection);
        gate.ExecuteNonQuery();
        try
        {
            using var compatible = new SqlCommand(@"IF OBJECT_ID(N'dbo.Calisan') IS NULL OR OBJECT_ID(N'dbo.Joker') IS NULL
                OR OBJECT_ID(N'dbo.izinler') IS NULL OR OBJECT_ID(N'dbo.admin') IS NULL
                THROW 51021,N'Önce veritabanı kurulumunu çalıştırın.',1;", connection); compatible.ExecuteNonQuery();
            var assembly = typeof(SchemaUpgrade).Assembly;
            var scripts = assembly.GetManifestResourceNames().Where(n => n.Contains(".database.upgrades.", StringComparison.Ordinal)).OrderBy(n => n, StringComparer.Ordinal)
                .Select(n => { using var r = new StreamReader(assembly.GetManifestResourceStream(n)!); string sql = r.ReadToEnd().Replace("\r\n", "\n"); return new SqlScript(n + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))), sql); }).ToArray();
            var executed = new HashSet<string>(StringComparer.Ordinal);
            using (var history = new SqlCommand("IF OBJECT_ID(N'dbo.__MigrationJournal') IS NOT NULL SELECT ScriptName FROM dbo.__MigrationJournal", connection))
            using (var reader = history.ExecuteReader())
                while (reader.Read())
                {
                    string name = reader.GetString(0);
                    if (!scripts.Any(s => s.Name == name)) throw new InvalidOperationException("Uygulanmış şema betiği değişmiş veya uygulama sürümü eski. Yükseltme durduruldu.");
                    executed.Add(name);
                }
            if (scripts.Any(s => !executed.Contains(s.Name))) CreateSafetyCopy(connection, safetyFolder, protectedBackupPath);
            var result = DeployChanges.To.SqlDatabase(connectionString).WithScripts(scripts).JournalToSqlTable("dbo", "__MigrationJournal")
                .WithTransaction().WithExecutionTimeout(TimeSpan.FromSeconds(90)).LogToNowhere().Build().PerformUpgrade();
            if (!result.Successful) throw new InvalidOperationException("Şema yükseltilemedi; yapılan değişiklikler geri alındı.", result.Error);
        }
        finally { using var release = new SqlCommand("EXEC sys.sp_releaseapplock @Resource=N'AccountingApp.Features.v1',@LockOwner='Session'", connection); release.ExecuteNonQuery(); }
    }
    private static void CreateSafetyCopy(SqlConnection connection, string? folder, string? protectedBackupPath)
    {
        using var catalog = new SqlCommand("SELECT DB_NAME()", connection); string name = (string)catalog.ExecuteScalar()!;
        string root = folder ?? Path.Combine(BackupService.DefaultFolder, "schema-upgrades"); Directory.CreateDirectory(root);
        string prefix = BackupService.Prefix(name); string path = Path.GetFullPath(Path.Combine(root, prefix + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffff") + "-" + Guid.NewGuid().ToString("N")[..12] + ".bak"));
        if (path.Length > 259) throw new ArgumentException("Şema yedeği yolu çok uzun; daha kısa bir yedek klasörü seçin.");
        static string Literal(string s) => "N'" + s.Replace("'", "''") + "'";
        using var backup = new SqlCommand($"BACKUP DATABASE [{name.Replace("]", "]]")}] TO DISK={Literal(path)} WITH COPY_ONLY,INIT,CHECKSUM", connection) { CommandTimeout = 180 };
        backup.ExecuteNonQuery(); using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(path)} WITH CHECKSUM", connection) { CommandTimeout = 180 }; verify.ExecuteNonQuery(); BackupService.Prune(root, prefix, 14, protectedBackupPath);
    }
}
