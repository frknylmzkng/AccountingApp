using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed class DatabaseSetupException : Exception
    {
        public DatabaseSetupException(string message) : base(message) { }
    }

    internal sealed record InitialAdminAccount(string Username, string Password);
    internal sealed record DatabaseMigration(int Version, string Script)
    {
        public string Checksum => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            Script.Replace("\r\n", "\n").Replace("\r", "\n"))));
    }
    internal sealed record DatabaseSetupResult(bool DatabaseCreated, int AppliedMigrations, bool AdminCreated);

    internal sealed class DatabaseInstaller
    {
        private readonly DatabaseSettings settings;
        private readonly IReadOnlyList<DatabaseMigration> migrations;

        public DatabaseInstaller(DatabaseSettings settings, IReadOnlyList<DatabaseMigration>? migrations = null)
        {
            this.settings = settings;
            this.migrations = migrations ?? LoadMigrations();
            if (this.migrations.Count == 0 || this.migrations.Any(m => m.Version < 1) ||
                this.migrations.Select(m => m.Version).Distinct().Count() != this.migrations.Count)
                throw new ArgumentException("Geçersiz veritabanı sürüm listesi.", nameof(migrations));
        }

        internal static IReadOnlyList<DatabaseMigration> LoadMigrations()
        {
            var assembly = typeof(DatabaseInstaller).Assembly;
            const string prefix = "WinFormsApp2.database.migrations.";
            var result = new List<DatabaseMigration>();
            foreach (string resource in assembly.GetManifestResourceNames()
                .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal)))
            {
                string fileName = resource[prefix.Length..];
                int separator = fileName.IndexOf('-');
                if (separator < 1 || !int.TryParse(fileName.AsSpan(0, separator), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int version))
                    throw new InvalidOperationException("Kurulum betiğinin sürüm adı geçersiz.");
                using Stream stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                result.Add(new DatabaseMigration(version, reader.ReadToEnd()));
            }
            return result.OrderBy(m => m.Version).ToArray();
        }

        public DatabaseSetupResult Install(Func<InitialAdminAccount> requestInitialAdmin)
        {
            if (string.IsNullOrWhiteSpace(settings.Name))
                throw new DatabaseSetupException("Yeni kurulum için Database.Name gereklidir. Yeni kurulum örnek ayar dosyasını kullanın.");

            var masterSettings = new SqlConnectionStringBuilder(settings.CreateConnectionString(requireExistingFile: false))
            {
                AttachDBFilename = "", InitialCatalog = "master", Pooling = false
            };
            using var master = new SqlConnection(masterSettings.ConnectionString);
            master.Open();
            // A non-pooled session lock also protects CREATE DATABASE, which cannot run in a user transaction.
            using (var setupLock = new SqlCommand(@"
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive',
                    @LockOwner='Session', @LockTimeout=10000;
                IF @result < 0 THROW 51010, N'Veritabanı kurulumu başka bir işlem tarafından kullanılıyor.', 1;", master))
            {
                setupLock.Parameters.Add("@Resource", SqlDbType.NVarChar, 255).Value =
                    "AccountingApp.Setup." + settings.Name.ToUpperInvariant();
                setupLock.ExecuteNonQuery();
            }

            bool created = EnsureDatabase(master);
            int applied = 0;
            bool adminCreated = false;
            DatabaseTransaction.Run(settings.CreateConnectionString(), (connection, transaction) =>
            {
                using (var check = new SqlCommand(@"
                    IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
                    BEGIN
                        IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped=0)
                            THROW 51011, N'Mevcut şema bu kurulum aracıyla yönetilmiyor; değişiklik yapılmadı.', 1;
                        CREATE TABLE dbo.SchemaMigrations
                        (
                            Version int NOT NULL PRIMARY KEY,
                            ScriptSha256 char(64) NOT NULL,
                            AppliedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME()
                        );
                    END;", connection, transaction))
                    check.ExecuteNonQuery();

                var history = new Dictionary<int, string>();
                using (var command = new SqlCommand("SELECT Version, ScriptSha256 FROM dbo.SchemaMigrations WITH (UPDLOCK,HOLDLOCK)",
                    connection, transaction))
                using (var reader = command.ExecuteReader())
                    while (reader.Read()) history.Add(reader.GetInt32(0), reader.GetString(1));
                if (history.Keys.Any(version => !migrations.Any(m => m.Version == version)))
                    throw new DatabaseSetupException("Veritabanının sürümü bu kurulum paketinden farklı. Uyumlu güncel paketi kullanın.");

                foreach (DatabaseMigration migration in migrations.OrderBy(m => m.Version))
                {
                    if (history.TryGetValue(migration.Version, out string? checksum))
                    {
                        if (!string.Equals(checksum, migration.Checksum, StringComparison.Ordinal))
                            throw new DatabaseSetupException("Daha önce uygulanmış bir veritabanı betiği değiştirilmiş. Kurulum durduruldu.");
                        continue;
                    }
                    if (history.Keys.Any(version => version > migration.Version))
                        throw new DatabaseSetupException("Veritabanı sürüm sırası geçersiz. Kurulum durduruldu.");
                    using var apply = new SqlCommand(migration.Script, connection, transaction);
                    apply.ExecuteNonQuery();
                    using var track = new SqlCommand(
                        "INSERT INTO dbo.SchemaMigrations(Version,ScriptSha256) VALUES(@Version,@Hash)", connection, transaction);
                    track.Parameters.Add("@Version", SqlDbType.Int).Value = migration.Version;
                    track.Parameters.Add("@Hash", SqlDbType.Char, 64).Value = migration.Checksum;
                    track.ExecuteNonQuery();
                    applied++;
                }

                using var count = new SqlCommand("SELECT COUNT(*) FROM dbo.admin WITH (UPDLOCK,HOLDLOCK)", connection, transaction);
                if (Convert.ToInt32(count.ExecuteScalar()) == 0)
                {
                    InitialAdminAccount account = requestInitialAdmin();
                    string username = AdminStore.ValidateNewAccount(account.Username, account.Password);
                    string hash = PasswordHasher.Hash(account.Password);
                    using var insert = new SqlCommand(
                        "INSERT INTO dbo.admin(username,password,password_hash) VALUES(@Username,N'',@Hash)", connection, transaction);
                    insert.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
                    insert.Parameters.Add("@Hash", SqlDbType.NVarChar, 256).Value = hash;
                    insert.ExecuteNonQuery();
                    adminCreated = true;
                }
            });
            return new DatabaseSetupResult(created, applied, adminCreated);
        }

        private bool EnsureDatabase(SqlConnection master)
        {
            using var existing = new SqlCommand(@"
                SELECT F.physical_name FROM sys.databases D JOIN sys.master_files F ON D.database_id=F.database_id
                WHERE D.name=@Name AND F.file_id=1", master);
            existing.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = settings.Name!;
            if (existing.ExecuteScalar() is string existingFile)
            {
                if (!string.Equals(Path.GetFullPath(existingFile), settings.FilePath, StringComparison.OrdinalIgnoreCase))
                    throw new DatabaseSetupException("Database.Name başka bir dosyaya bağlı. Ad veya dosya ayarını düzeltin; mevcut veritabanına dokunulmadı.");
                return false;
            }

            string logFile = Path.Combine(Path.GetDirectoryName(settings.FilePath)!,
                Path.GetFileNameWithoutExtension(settings.FilePath) + "_log.ldf");
            if (File.Exists(settings.FilePath) || File.Exists(logFile))
                throw new DatabaseSetupException("Hedef MDF veya LDF dosyası zaten var. Mevcut dosyalar üzerine kurulum yapılmaz.");
            Directory.CreateDirectory(Path.GetDirectoryName(settings.FilePath)!);
            using var create = new SqlCommand($@"
                CREATE DATABASE {QuoteIdentifier(settings.Name!)}
                ON PRIMARY (NAME=N'AccountingApp_Data', FILENAME={QuoteLiteral(settings.FilePath)})
                LOG ON (NAME=N'AccountingApp_Log', FILENAME={QuoteLiteral(logFile)})", master);
            create.ExecuteNonQuery();
            return true;
        }

        private static string QuoteIdentifier(string value) => "[" + value.Replace("]", "]]") + "]";
        private static string QuoteLiteral(string value) => "N'" + value.Replace("'", "''") + "'";
    }
}
