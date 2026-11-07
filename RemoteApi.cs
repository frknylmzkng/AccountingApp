using System.Data;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class RemoteServerIdentityException(string message) : InvalidOperationException(message);

// A token belongs to exactly one authenticated session and one frozen origin.
// No password/token persistence, redirects, automatic retries or SQL fallback.
internal sealed class RemoteApi
{
    private readonly HttpClient client;
    private string? token;
    private readonly Guid accountId;
    private Task pendingLock = Task.CompletedTask;
    internal string Connection { get; }
    internal string Scope { get; }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private RemoteApi(string connection, string accessToken, Guid id, string scope)
    { Connection = connection; token = accessToken; accountId = id; Scope = scope; client = CreateClient(new Uri(connection[4..])); }
    internal static HttpClient CreateClient(Uri uri) => new(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(45), MaxResponseContentBufferSize = 32 * 1024 * 1024 };
    internal static RemoteApi For(string connection, UserSession user) => user.Remote is { } api && api.Connection == connection && api.token != null
        ? api : throw new SessionRevokedException();
    internal static async Task<UserSession?> LoginAsync(string connection, string username, string password, CancellationToken ct)
    {
        using var http = CreateClient(new Uri(connection[4..]));
        using var response = await http.PostAsJsonAsync("api/v1/auth/login", new Wire.LoginRequest(username, password), Json, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return null;
        await CheckAsync(response, ct).ConfigureAwait(false);
        var login = await response.Content.ReadFromJsonAsync<Wire.LoginResponse>(Json, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Sunucu yanıtı okunamadı.");
        if (!Enum.IsDefined(login.User.Role) || login.User.AccountId == Guid.Empty || login.ServerScope is not { Length: 32 } || login.ServerScope.Any(c => !Uri.IsHexDigit(c)))
            throw new RemoteServerIdentityException("Sunucu kimliği doğrulanamadı.");
        var api = new RemoteApi(connection, login.AccessToken, login.User.AccountId, login.ServerScope);
        try
        {
            var prefs = UserPreferences.Load();
            prefs.CentralApiScopes ??= new();
            if (prefs.CentralApiScopes.TryGetValue(connection, out string? known) && known != login.ServerScope)
                throw new RemoteServerIdentityException("Bu sunucu adresinin veritabanı kimliği değişmiş. Bekleyen ödemeleri ve sunucu profilini yöneticiyle kontrol edin; giriş durduruldu.");
            prefs.CentralApiScopes[connection] = login.ServerScope; prefs.Save();
            return api.User(login.User);
        }
        catch { api.End(); throw; }
    }
    private UserSession User(Wire.UserInfo user) => new(user.Username, (UserRole)user.Role)
    { Remote = this, MustChangePassword = user.MustChangePassword, PaymentGranted = user.CanPay, PersonalDataExportGranted = user.CanExportPersonalData, SalaryViewGranted = user.CanViewSalary, IbanViewGranted = user.CanViewIban };
    internal void End()
    {
        string? previous = Interlocked.Exchange(ref token, null);
        if (previous == null) return;
        _ = LogoutAsync(previous);
    }
    private async Task LogoutAsync(string previous)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/logout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", previous);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await client.SendAsync(request, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or ObjectDisposedException) { }
        finally { client.Dispose(); }
    }
    private static async Task CheckAsync(HttpResponseMessage response, CancellationToken ct, bool credentialCheck = false)
    {
        if (response.IsSuccessStatusCode) return;
        Wire.Error? error = null;
        try { error = await response.Content.ReadFromJsonAsync<Wire.Error>(Json, ct).ConfigureAwait(false); } catch (JsonException) { }
        if (response.StatusCode == HttpStatusCode.Unauthorized && !credentialCheck) throw new SessionRevokedException();
        if (response.StatusCode == HttpStatusCode.Forbidden) throw new UnauthorizedAccessException(error?.Message ?? "Bu işlem için yetki gerekiyor.");
        if ((int)response.StatusCode == 423) throw new InvalidOperationException("Sunucu oturumu kilitli. Oturumu kilitleyip parolanızla yeniden açın.");
        if ((int)response.StatusCode is 400 or 409 or 429) throw new InvalidOperationException(error?.Message ?? "İstek tamamlanamadı; güncel kaydı kontrol edin.");
        throw new InvalidOperationException("Sunucu işlemi tamamlanamadı. Ödeme yaptıysanız yeni ödeme başlatmadan önce bekleyen isteğin sonucunu kontrol edin. Kod: " + (error?.TraceId ?? "HTTP-" + (int)response.StatusCode));
    }
    internal async Task<T> SendAsync<T>(string path, object? body, CancellationToken ct, bool post = false)
    {
        try
        {
            using var request = new HttpRequestMessage(post || body != null ? HttpMethod.Post : HttpMethod.Get, "api/v1/" + path);
            string current = token ?? throw new SessionRevokedException();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", current);
            if (body != null) request.Content = JsonContent.Create(body, options: Json);
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            await CheckAsync(response, ct).ConfigureAwait(false);
            if (typeof(T) == typeof(bool) && response.StatusCode == HttpStatusCode.NoContent) return (T)(object)true;
            return await response.Content.ReadFromJsonAsync<T>(Json, ct).ConfigureAwait(false) ?? throw new InvalidOperationException("Sunucu yanıtı boş.");
        }
        catch (HttpRequestException) { throw new InvalidOperationException("Merkezi sunucuya ulaşılamadı. Ödeme sonucunu kontrol etmeden yeni ödeme başlatmayın; yerel veritabanına geçilmedi."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("Sunucu yanıtı zaman aşımına uğradı. Bekleyen ödeme korunuyor; sonucunu yeniden kontrol edin."); }
    }
    internal Task Done(string path, object body, CancellationToken ct) => SendAsync<bool>(path, body, ct);
    internal async Task ValidateAsync(AccountAccess access, CancellationToken ct)
    {
        var info = access is AccountAccess.Read or AccountAccess.ChangePassword
            ? await SendAsync<Wire.UserInfo>("session/validate", null, ct).ConfigureAwait(false)
            : await SendAsync<Wire.UserInfo>("authorization", new Wire.AuthorizationRequest(access.ToString()), ct).ConfigureAwait(false);
        if (info.AccountId != accountId) throw new SessionRevokedException();
    }
    internal async Task<bool> UnlockAsync(string password, CancellationToken ct)
    {
        await pendingLock.ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/unlock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? throw new SessionRevokedException());
        request.Content = JsonContent.Create(new Wire.PasswordRequest(password), options: Json);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var error = await response.Content.ReadFromJsonAsync<Wire.Error>(Json, ct).ConfigureAwait(false);
            if (error?.Code == "INVALID_CREDENTIALS") return false;
            throw new SessionRevokedException();
        }
        await CheckAsync(response, ct).ConfigureAwait(false);
        var info = await response.Content.ReadFromJsonAsync<Wire.UserInfo>(Json, ct).ConfigureAwait(false);
        if (info?.AccountId != accountId) throw new SessionRevokedException();
        return true;
    }
    internal async Task<DataTable> TableAsync(string path, CancellationToken ct) => Decode(await SendAsync<Wire.Table>(path, null, ct).ConfigureAwait(false));
    internal void Lock() => pendingLock = LockSafelyAsync();
    private async Task LockSafelyAsync()
    {
        try { await SendAsync<bool>("auth/lock", null, CancellationToken.None, post: true).ConfigureAwait(false); }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or ObjectDisposedException) { }
        // Local windows stay hidden even if the network lock could not arrive.
        // Unlock always requires a new successful server password verification.
    }
    internal async Task<Guid> IdAsync(string path, object body, string property, CancellationToken ct) =>
        (await SendAsync<JsonElement>(path, body, ct).ConfigureAwait(false)).GetProperty(property).GetGuid();
    internal async Task<PersonnelPage> PageAsync(string path, CancellationToken ct)
    { var page = await SendAsync<Wire.Page>(path, null, ct).ConfigureAwait(false); return new(Decode(page.Data), page.Total, page.Index, page.Size); }
    internal static string Q(string value) => Uri.EscapeDataString(value);
    internal static string Day(DateTime date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    internal static string Money(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);
    internal static string Reason => AuditContext.Reason ?? throw new ArgumentException("İşlem gerekçesi gerekiyor.");
    internal static Wire.AccountTarget Target(AccountTarget? selected) { var t = AccountTarget.RequireSnapshot(selected); return new(t.Username, t.Version, t.Epoch); }
    internal static Wire.PaymentIntent Payment(PaymentRequest p) => new(p.RequestId, (Wire.PersonnelGroup)p.Group, p.Tc, p.Amount, p.ClosureId, p.FormatVersion, p.PersonVersion, p.PersonId, (Wire.PaymentPurpose)p.Purpose, p.ProjectId, p.AuditReason);
    internal static PaymentRequest Payment(Wire.PaymentIntent p) => new(p.RequestId, (PersonnelGroup)p.Group, p.Tc, p.Amount, p.ClosureId)
    { FormatVersion = p.FormatVersion, PersonVersion = p.PersonVersion, PersonId = p.PersonId, Purpose = (PaymentPurpose)p.Purpose, ProjectId = p.ProjectId, AuditReason = p.Reason };
    internal static PaymentOutcome Payment(Wire.PaymentResult p) => Enum.TryParse<PaymentStatus>(p.Status, out var status) && Enum.IsDefined(status)
        ? new(status, p.Reason) : throw new InvalidOperationException("Ödeme sonucu tanınmıyor; bekleyen istek korundu.");
    internal static PrintReport Print(Wire.PrintData p) => new(p.Title, p.Lines, p.Reference, p.Difference);
    internal static Wire.Table Encode(DataTable table)
    {
        Wire.ColumnType Kind(Type type) => type == typeof(string) ? Wire.ColumnType.String : type == typeof(bool) ? Wire.ColumnType.Boolean :
            type == typeof(int) ? Wire.ColumnType.Int32 : type == typeof(long) ? Wire.ColumnType.Int64 : type == typeof(decimal) ? Wire.ColumnType.Decimal :
            type == typeof(DateTime) ? Wire.ColumnType.DateTime : type == typeof(Guid) ? Wire.ColumnType.Guid : type == typeof(byte[]) ? Wire.ColumnType.Binary : throw new ArgumentException("Desteklenmeyen rapor sütunu.");
        return new(table.Columns.Cast<DataColumn>().Select(c => new Wire.Column(c.ColumnName, Kind(c.DataType), c.ReadOnly)).ToArray(),
            table.Rows.Cast<DataRow>().Select(r => table.Columns.Cast<DataColumn>().Select(c => JsonSerializer.SerializeToElement(r[c] switch
            { DBNull => null, long v => (object)v.ToString(CultureInfo.InvariantCulture), var v => v })).ToArray()).ToArray());
    }
    internal async Task<byte[]> ExportAsync(DataTable data, bool excel, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/exports/file");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? throw new SessionRevokedException());
        request.Content = JsonContent.Create(new Wire.ExportRequest(Encode(data), excel,
            data.ExtendedProperties["Export.ReportTitle"]?.ToString() ?? data.TableName,
            data.ExtendedProperties["Export.ReportScope"]?.ToString() ?? "Seçilen rapor"), options: Json);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        await CheckAsync(response, ct).ConfigureAwait(false);
        if (response.Content.Headers.ContentType?.MediaType != "application/octet-stream") throw new InvalidOperationException("Rapor yanıtı geçersiz.");
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
    internal async Task<byte[]> ProjectCsvAsync(Wire.ProjectExport input, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/project-access/export");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? throw new SessionRevokedException());
        request.Content = JsonContent.Create(input, options: Json);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        await CheckAsync(response, ct).ConfigureAwait(false);
        if (response.Content.Headers.ContentType?.MediaType != "application/octet-stream") throw new InvalidOperationException("Rapor yanıtı geçersiz.");
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
    internal async Task<byte[]> ReportZipAsync(Wire.ReportCenterExport input,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/v1/report-center/export");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token??throw new SessionRevokedException());
        request.Content=JsonContent.Create(input,options:Json);
        using var response=await client.SendAsync(request,ct).ConfigureAwait(false);await CheckAsync(response,ct).ConfigureAwait(false);
        if(response.Content.Headers.ContentType?.MediaType!="application/zip")throw new InvalidOperationException("Rapor arşivi yanıtı geçersiz.");
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
    internal async Task<byte[]> SafeSupportAsync(CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/v1/support/safe-package");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token??throw new SessionRevokedException());
        using var response=await client.SendAsync(request,ct).ConfigureAwait(false);await CheckAsync(response,ct).ConfigureAwait(false);
        if(response.Content.Headers.ContentType?.MediaType!="application/zip")throw new InvalidOperationException("Tanı paketi yanıtı geçersiz.");return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }
    internal static DataTable Decode(Wire.Table table)
    {
        var data = new DataTable();
        Type TypeOf(Wire.ColumnType t) => t switch { Wire.ColumnType.String => typeof(string), Wire.ColumnType.Boolean => typeof(bool), Wire.ColumnType.Int32 => typeof(int), Wire.ColumnType.Int64 => typeof(long), Wire.ColumnType.Decimal => typeof(decimal), Wire.ColumnType.DateTime => typeof(DateTime), Wire.ColumnType.Guid => typeof(Guid), Wire.ColumnType.Binary => typeof(byte[]), _ => throw new InvalidOperationException("Tablo tipi tanınmıyor.") };
        foreach (var column in table.Columns) data.Columns.Add(column.Name, TypeOf(column.Type));
        foreach (var row in table.Rows)
        {
            if (row.Length != table.Columns.Length) throw new InvalidOperationException("Tablo satırı geçersiz.");
            data.Rows.Add(row.Select((cell, i) => cell.ValueKind == JsonValueKind.Null ? DBNull.Value : table.Columns[i].Type switch
            {
                Wire.ColumnType.Int64 => (object)long.Parse(cell.GetString()!, CultureInfo.InvariantCulture),
                Wire.ColumnType.String => cell.GetString()!, Wire.ColumnType.Boolean => cell.GetBoolean(), Wire.ColumnType.Int32 => cell.GetInt32(),
                Wire.ColumnType.Decimal => cell.GetDecimal(), Wire.ColumnType.DateTime => cell.GetDateTime(), Wire.ColumnType.Guid => cell.GetGuid(),
                Wire.ColumnType.Binary => cell.GetBytesFromBase64(), _ => throw new InvalidOperationException("Tablo tipi tanınmıyor.")
            }).ToArray());
        }
        for (int i = 0; i < table.Columns.Length; i++) data.Columns[i].ReadOnly = table.Columns[i].ReadOnly;
        return data;
    }
}
