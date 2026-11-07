using System.Security.Cryptography;
using System.Text;
using WinFormsApp2;

namespace AccountingApp.Server;

internal sealed class ApiSession(string key, UserSession user, Guid accountId, long timestamp, DateTimeOffset expires)
{
    internal string Key { get; } = key;
    internal UserSession User { get; } = user;
    internal Guid AccountId { get; } = accountId;
    internal long Created { get; } = timestamp;
    internal long LastUsed = timestamp;
    internal DateTimeOffset AbsoluteExpiryUtc { get; } = expires;
    internal bool Locked;
}
internal sealed class ApiSessionLimitException : Exception { }

internal sealed class ApiSessions(TimeProvider clock, ServerOptions options)
{
    private readonly object gate = new();
    private readonly Dictionary<string, ApiSession> sessions = new(StringComparer.Ordinal);
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    private bool Expired(ApiSession entry) =>
        clock.GetElapsedTime(entry.Created) >= TimeSpan.FromHours(options.AbsoluteHours) ||
        clock.GetElapsedTime(entry.LastUsed) >= TimeSpan.FromMinutes(options.IdleMinutes);
    internal (string Token, ApiSession Session) Issue(UserSession user, Guid accountId)
    {
        lock (gate)
        {
            foreach (string key in sessions.Where(p => Expired(p.Value)).Select(p => p.Key).ToArray()) sessions.Remove(key);
            if (sessions.Count >= options.MaxSessions || sessions.Values.Count(s => string.Equals(s.User.Username, user.Username, StringComparison.OrdinalIgnoreCase)) >= options.MaxSessionsPerUser)
                throw new ApiSessionLimitException();
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var entry = new ApiSession(Hash(token), user with { SecurityVersion = user.SecurityVersion?.ToArray() }, accountId,
                clock.GetTimestamp(), clock.GetUtcNow().AddHours(options.AbsoluteHours));
            sessions.Add(entry.Key, entry); return (token, entry);
        }
    }
    internal ApiSession? Resolve(string authorization)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        string token = authorization[7..];
        if (token.Length != 43 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) return null;
        lock (gate)
        {
            string key = Hash(token);
            if (!sessions.TryGetValue(key, out var entry)) return null;
            if (Expired(entry)) { sessions.Remove(key); return null; }
            return entry;
        }
    }
    internal void Touch(ApiSession entry) { lock (gate) { if (sessions.ContainsKey(entry.Key)) entry.LastUsed = clock.GetTimestamp(); } }
    internal void Remove(ApiSession entry) { lock (gate) sessions.Remove(entry.Key); }
    internal void SetLocked(ApiSession entry, bool value) { lock (gate) { entry.Locked = value; } }
    internal bool IsLocked(ApiSession entry) { lock (gate) return entry.Locked; }
    internal void RevokeUser(string username)
    { lock (gate) foreach (string key in sessions.Where(p => string.Equals(p.Value.User.Username, username, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToArray()) sessions.Remove(key); }
}

internal sealed record SessionAccess(bool AllowLocked = false, bool AllowTemporary = false, bool Touch = true);
internal sealed class ApiSessionFilter(ApiSessions sessions, ServerRuntime runtime) : IEndpointFilter
{
    internal const string ItemKey = "AccountingApi.Session";
    internal static ApiSession Current(HttpContext context) => (ApiSession)context.Items[ItemKey]!;
    internal static IResult Failure(HttpContext context, int status, string code, string message) =>
        Results.Json(new Contracts.Error(code, message, context.TraceIdentifier), statusCode: status);
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var entry = sessions.Resolve(http.Request.Headers.Authorization.ToString());
        if (entry == null) return Failure(http, 401, "UNAUTHENTICATED", "Yeniden giriş gerekiyor.");
        var access = http.GetEndpoint()?.Metadata.GetMetadata<SessionAccess>() ?? new();
        try
        {
            await runtime.VerifySchemaAsync(http.RequestAborted);
            await AccountAuthorization.ValidateAsync(runtime.ConnectionString, entry.User,
                entry.User.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read, http.RequestAborted);
        }
        catch (SessionRevokedException) { sessions.Remove(entry); return Failure(http, 401, "SESSION_REVOKED", "Hesap veya yetki değişmiş; yeniden giriş gerekiyor."); }
        if (sessions.IsLocked(entry) && !access.AllowLocked) return Failure(http, 423, "SESSION_LOCKED", "Oturum kilitli.");
        if (entry.User.MustChangePassword && !access.AllowTemporary) return Failure(http, 403, "PASSWORD_CHANGE_REQUIRED", "Önce parolanızı değiştirin.");
        if (access.Touch) sessions.Touch(entry);
        http.Items[ItemKey] = entry;
        return await next(context);
    }
}
