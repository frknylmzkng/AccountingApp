using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

// FamilyGUID is SQL Server's persistent database lineage identity. Endpoint and
// physical location also separate independently used clones of that lineage.
internal sealed record BackupScope(string Catalog, Guid FamilyId, string Source, string DataFile)
{
    internal string Key => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[]
        { FamilyId.ToString("N"), Catalog.ToUpperInvariant(), Source.ToUpperInvariant(), DataFile.ToUpperInvariant() })))[..32];
    internal string Prefix => "AccountingApp-v2-" + Key + "-";
    internal string BackupName => "AccountingApp.v2." + Key;
    internal const string ManifestSuffix = ".backup.json";

    internal static BackupScope Read(SqlConnection connection)
    {
        using var cmd = new SqlCommand(@"SELECT DB_NAME(),R.family_guid,
            CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),F.physical_name
            FROM sys.database_recovery_status R CROSS JOIN sys.database_files F
            WHERE R.database_id=DB_ID() AND F.file_id=1 AND F.type=0", connection);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(1) || reader.IsDBNull(2) || reader.IsDBNull(3))
            throw new InvalidOperationException("Veritabanı yedek kimliği okunamadı; işlem durduruldu.");
        var builder = new SqlConnectionStringBuilder(connection.ConnectionString);
        // LocalDB's internal SQL instance name can change. Use the stable named
        // user instance instead; ordinary SQL Express aliases use its server name.
        string source = builder.DataSource.StartsWith(@"(LocalDB)\", StringComparison.OrdinalIgnoreCase)
            ? Environment.MachineName + ":" + Environment.UserDomainName + @"\" + Environment.UserName + ":" + builder.DataSource.Trim()
            : reader.GetString(2);
        var scope = new BackupScope(reader.GetString(0), reader.GetGuid(1), source, reader.GetString(3));
        if (scope.FamilyId == Guid.Empty || string.IsNullOrWhiteSpace(scope.DataFile) || reader.Read())
            throw new InvalidOperationException("Veritabanı yedek kimliği tekil değil.");
        return scope;
    }

    internal static async Task<BackupScope> ReadAsync(string connectionString, CancellationToken token)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        return Read(connection);
    }

    internal DataRow ValidateHeader(DataTable headers)
    {
        if (headers.Rows.Count != 1) throw new InvalidOperationException("Yedek tek bir tam yedek içermelidir.");
        var row = headers.Rows[0];
        if (!string.Equals(row["DatabaseName"].ToString(), Catalog, StringComparison.OrdinalIgnoreCase) ||
            Convert.ToInt32(row["BackupType"]) != 1 || row["FamilyGUID"] is not Guid family || family != FamilyId ||
            row["BackupName"].ToString() != BackupName || row["BackupSetGUID"] is not Guid set || set == Guid.Empty)
            throw new InvalidOperationException("Yedeğin veritabanı kimliği veya kaynak kapsamı bu hedefle eşleşmiyor. Başka veritabanına ait yedek kullanılamaz.");
        return row;
    }

    internal async Task WriteManifestAsync(string path, SqlConnection master, CancellationToken token)
    {
        using var header = new SqlCommand($"RESTORE HEADERONLY FROM DISK=N'{path.Replace("'", "''")}'", master);
        var row = ValidateHeader(await header.TableAsync(token).ConfigureAwait(false));
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        string hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
        var manifest = new BackupManifest(2, FamilyId, Key, Catalog, Source, DataFile, (Guid)row["BackupSetGUID"], stream.Length, hash);
        AtomicFile.Write(path + ManifestSuffix, JsonSerializer.SerializeToUtf8Bytes(manifest), durable: true);
    }

    internal bool Owns(string path, bool verifyPayload = true) => path.EndsWith(".abk",StringComparison.OrdinalIgnoreCase)?BackupVault.Owns(path,Prefix):ReadOwned(path, Prefix, verifyPayload) is { } manifest &&
        manifest.FamilyId == FamilyId && manifest.ScopeKey == Key &&
        string.Equals(manifest.Catalog, Catalog, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(manifest.Source, Source, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(manifest.DataFile, DataFile, StringComparison.OrdinalIgnoreCase);

    internal static BackupManifest? ReadOwned(string path, string prefix, bool verifyPayload = true)
    {
        try
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(prefix, @"\AAccountingApp-v2-[0-9A-F]{32}-\z") ||
                !Path.GetFileName(path).StartsWith(prefix, StringComparison.Ordinal) ||
                !path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(path + ManifestSuffix) & FileAttributes.ReparsePoint) != 0) return null;
            if (new FileInfo(path + ManifestSuffix).Length > 65536) return null;
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path + ManifestSuffix));
            if (manifest == null || manifest.FormatVersion != 2 || manifest.FamilyId == Guid.Empty || manifest.BackupSetId == Guid.Empty ||
                string.IsNullOrWhiteSpace(manifest.Catalog) || string.IsNullOrWhiteSpace(manifest.Source) || string.IsNullOrWhiteSpace(manifest.DataFile) ||
                new BackupScope(manifest.Catalog, manifest.FamilyId, manifest.Source, manifest.DataFile).Prefix != prefix ||
                manifest.ScopeKey != prefix[17..^1]) return null;
            if (!verifyPayload) return manifest;
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return file.Length == manifest.Length && Convert.ToHexString(SHA256.HashData(file)) == manifest.Sha256 ? manifest : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { return null; }
    }
}

internal sealed record BackupManifest(int FormatVersion, Guid FamilyId, string ScopeKey, string Catalog,
    string Source, string DataFile, Guid BackupSetId, long Length, string Sha256);
