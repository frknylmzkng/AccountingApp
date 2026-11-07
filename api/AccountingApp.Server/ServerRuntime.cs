using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using WinFormsApp2;

namespace AccountingApp.Server;

internal sealed record ServerOptions(string DatabaseProfilePath, string ListenUrl, int IdleMinutes, int AbsoluteHours, int MaxSessions, int MaxSessionsPerUser, bool AllowUserLocalDb)
{
    internal static ServerOptions Load(IConfiguration configuration, string root)
    {
        var section = configuration.GetSection("AccountingApi");
        string path = section["DatabaseProfilePath"] ?? "";
        string url = section["ListenUrl"] ?? "https://localhost:7443";
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("AccountingApi:DatabaseProfilePath must explicitly select the server database profile.");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "https" || address.UserInfo.Length != 0 ||
            address.AbsolutePath != "/" || address.Query.Length != 0 || address.Fragment.Length != 0)
            throw new InvalidOperationException("AccountingApi:ListenUrl must be an HTTPS origin.");
        int idle = section.GetValue("SessionIdleMinutes", 15), absolute = section.GetValue("SessionAbsoluteHours", 8);
        int sessions = section.GetValue("MaxSessions", 5000), perUser = section.GetValue("MaxSessionsPerUser", 8);
        bool localDb = section.GetValue("AllowUserLocalDb", false);
        if (localDb && (address.Host != "localhost" || configuration.GetSection("Kestrel:Endpoints").Exists()))
            throw new InvalidOperationException("User LocalDB mode requires localhost and cannot configure alternative Kestrel endpoints.");
        if (idle is < 5 or > 60 || absolute is < 1 or > 24 || sessions is < 1 or > 10000 || perUser is < 1 or > 32 || perUser > sessions)
            throw new InvalidOperationException("Invalid API session limits.");
        return new(Path.GetFullPath(path, root), url, idle, absolute, sessions, perUser, localDb);
    }
}

internal sealed class ServerSchemaException : Exception { }

internal sealed class ServerRuntime
{
    private static readonly HashSet<string> ExpectedSchema = ReadExpectedSchema();
    private string? scope;
    internal string Scope => scope ?? throw new ServerSchemaException();
    internal string ConnectionString { get; }
    internal ServerRuntime(ServerOptions options, bool development)
    {
        var settings = DatabaseSettings.Load(options.DatabaseProfilePath);
        if (!development && !options.AllowUserLocalDb && settings.Server.StartsWith("(LocalDB)", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Production API requires a local SQL Express profile, not a per-user LocalDB instance.");
        ConnectionString = settings.CreateConnectionString();
    }
    private static HashSet<string> ReadExpectedSchema()
    {
        var assembly = typeof(SchemaUpgrade).Assembly;
        return assembly.GetManifestResourceNames().Where(n => n.Contains(".database.upgrades.", StringComparison.Ordinal)).Select(name =>
        {
            using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new StreamReader(stream);
            return name + ":" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reader.ReadToEnd().Replace("\r\n", "\n"))));
        }).ToHashSet(StringComparer.Ordinal);
    }
    internal async Task VerifySchemaAsync(CancellationToken token)
    {
        using var connection = new SqlConnection(ConnectionString); await connection.OpenAsync(token);
        using var command = new SqlCommand("SELECT ScriptName FROM dbo.__MigrationJournal", connection);
        using var rows = await command.ExecuteReaderAsync(token);
        var actual = new HashSet<string>(StringComparer.Ordinal); while (await rows.ReadAsync(token)) actual.Add(rows.GetString(0));
        if (!ExpectedSchema.SetEquals(actual)) throw new ServerSchemaException();
        await rows.DisposeAsync();
        string currentScope = BackupScope.Read(connection).Key;
        string? previous = Interlocked.CompareExchange(ref scope, currentScope, null);
        if (previous != null && previous != currentScope) throw new ServerSchemaException();
    }
    internal async Task<Guid> VerifiedAccountIdAsync(UserSession user, CancellationToken token)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(ConnectionString, token);
        await AccountAuthorization.DemandAsync(work, user, user.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read, token);
        using var command = work.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");
        Guid id = (Guid)(await command.ExecuteScalarAsync(token))!;
        await work.CommitAsync(token); return id;
    }
}
