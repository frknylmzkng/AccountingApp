using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed class DatabaseConfigurationException : Exception
    {
        public DatabaseConfigurationException(string message) : base(message) { }
    }

    internal sealed record DatabaseSettings(string Server, string? Name, string FilePath, int ConnectTimeoutSeconds)
    {
        private sealed class Configuration
        {
            public DatabaseOptions? Database { get; set; }
        }

        private sealed class DatabaseOptions
        {
            public string? Server { get; set; }
            public string? Name { get; set; }
            public string? FilePath { get; set; }
            public int ConnectTimeoutSeconds { get; set; } = 30;
        }

        public static DatabaseSettings Load(string configurationPath)
        {
            Configuration? configuration;
            try
            {
                configuration = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(configurationPath),
                    new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                throw new DatabaseConfigurationException("appsettings.json okunamadı. Dosya yolunu, JSON biçimini ve örnekteki alan adlarını kontrol edin.");
            }

            DatabaseOptions? options = configuration?.Database;
            if (options == null || string.IsNullOrWhiteSpace(options.Server) || string.IsNullOrWhiteSpace(options.FilePath))
                throw new DatabaseConfigurationException("appsettings.json içindeki Database.Server ve Database.FilePath alanları gereklidir.");
            string server = options.Server.Trim();
            if (!server.StartsWith(@"(LocalDB)\", StringComparison.OrdinalIgnoreCase) ||
                server.Length <= @"(LocalDB)\".Length || server.Any(char.IsControl))
                throw new DatabaseConfigurationException("Database.Server yerel bir LocalDB örneği olmalıdır.");
            string? name = string.IsNullOrWhiteSpace(options.Name) ? null : options.Name.Trim();
            if (name is { Length: > 128 } || name?.Any(char.IsControl) == true ||
                new[] { "master", "model", "msdb", "tempdb" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                throw new DatabaseConfigurationException("Database.Name geçerli bir uygulama veritabanı adı olmalıdır.");
            if (options.ConnectTimeoutSeconds is < 1 or > 120)
                throw new DatabaseConfigurationException("Database.ConnectTimeoutSeconds 1 ile 120 arasında olmalıdır.");

            string filePath;
            try
            {
                string expanded = Environment.ExpandEnvironmentVariables(options.FilePath);
                if (expanded.Contains('%'))
                    throw new ArgumentException();
                filePath = Path.GetFullPath(expanded, Path.GetDirectoryName(Path.GetFullPath(configurationPath))!);
                if (!string.Equals(Path.GetExtension(filePath), ".mdf", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException();
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            {
                throw new DatabaseConfigurationException("Database.FilePath geçerli bir MDF dosya yolu olmalıdır; ortam değişkenlerini kontrol edin.");
            }
            return new DatabaseSettings(server, name, filePath, options.ConnectTimeoutSeconds);
        }

        public string CreateConnectionString(bool requireExistingFile = true)
        {
            if (requireExistingFile && !File.Exists(FilePath))
                throw new DatabaseConfigurationException("Database.FilePath dosyası bulunamadı. Ayarı kontrol edin veya yeni veritabanı kurulumunu çalıştırın.");
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = Server,
                AttachDBFilename = FilePath,
                IntegratedSecurity = true,
                ConnectTimeout = ConnectTimeoutSeconds,
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = false
            };
            if (Name != null) builder.InitialCatalog = Name;
            return builder.ConnectionString;
        }
    }

    internal static class AppDatabase
    {
        private static readonly object Gate = new();
        private static DatabaseSettings? settings;

        public static string ConnectionString
        {
            get
            {
                lock (Gate)
                {
                    // Freeze a valid configuration for this process; a failed read can be retried.
                    if (settings == null)
                    {
                        DatabaseSettings loaded = DatabaseSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
                        string connection = loaded.CreateConnectionString();
                        settings = loaded;
                        return connection;
                    }
                    return settings.CreateConnectionString();
                }
            }
        }
    }
}
