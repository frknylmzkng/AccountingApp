using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AccountingApp.Contracts;
using AccountingApp.Server;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using WinFormsApp2;

var builder = WebApplication.CreateBuilder(args);
// The desktop profile may be present through the legacy project reference.
// It is never implicitly used as the server database selection.
builder.Configuration.AddJsonFile("server.json", optional: true, reloadOnChange: false).AddEnvironmentVariables().AddCommandLine(args);
builder.Configuration["AllowedHosts"] ??= "localhost";
var options = ServerOptions.Load(builder.Configuration, builder.Environment.ContentRootPath);
builder.WebHost.UseUrls(options.ListenUrl).ConfigureKestrel(k =>
{
    // Explicit same-user installation mode: a local DB must never become a LAN listener.
    if (options.AllowUserLocalDb)
        k.ListenLocalhost(new Uri(options.ListenUrl).Port, endpoint => endpoint.UseHttps());
    k.AddServerHeader = false; k.Limits.MaxRequestBodySize = 128 * 1024;
    k.Limits.MaxRequestHeadersTotalSize = 16 * 1024; k.Limits.MaxConcurrentConnections = 100;
    k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.None);
builder.Services.ConfigureHttpJsonOptions(json =>
{
    json.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    json.SerializerOptions.RespectRequiredConstructorParameters = true;
    json.SerializerOptions.RespectNullableAnnotations = true;
    json.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new ServerRuntime(options, builder.Environment.IsDevelopment()));
builder.Services.AddSingleton<ApiSessions>(); builder.Services.AddScoped<ApiSessionFilter>();
builder.Services.AddRateLimiter(rate =>
{
    rate.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    rate.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetTokenBucketLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new TokenBucketRateLimiterOptions { TokenLimit = 120, TokensPerPeriod = 60, ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                AutoReplenishment = true, QueueLimit = 0 }));
    rate.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(new AccountingApp.Contracts.Error("RATE_LIMITED", "İstek sınırı aşıldı; daha sonra yeniden deneyin.", context.HttpContext.TraceIdentifier), token);
    };
});
var app = builder.Build();
app.Use(async (HttpContext context, RequestDelegate next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    if (!context.Request.IsHttps)
    {
        context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new AccountingApp.Contracts.Error("HTTPS_REQUIRED", "HTTPS gerekiyor.", context.TraceIdentifier)); return;
    }
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        if (context.Response.HasStarted) throw;
        var (status, code, message) = ex switch
        {
            SessionRevokedException => (401, "SESSION_REVOKED", "Hesap veya yetki değişmiş; yeniden giriş gerekiyor."),
            PasswordChangeRequiredException => (403, "PASSWORD_CHANGE_REQUIRED", "Önce parolanızı değiştirin."),
            UnauthorizedAccessException => (403, "FORBIDDEN", "Bu işlem için yetkiniz yok."),
            ApiSessionLimitException => (429, "SESSION_LIMIT", "Oturum sınırı aşıldı; kullanılmayan oturumları kapatın."),
            ServerSchemaException => (503, "SCHEMA_INCOMPATIBLE", "Sunucu şeması bu sürümle uyumlu değil; yönetici müdahalesi gerekiyor."),
            ArgumentException or JsonException or BadHttpRequestException => (400, "INVALID_REQUEST", "İstek alanları geçersiz."),
            SqlException => (503, "DATABASE_UNAVAILABLE", "Veritabanı işlemi tamamlanamadı; yazma sonucunu işlem kimliğiyle kontrol edin."),
            InvalidOperationException => (409, "CONFLICT", "İşlem mevcut kayıt veya sürümle uyuşmuyor. Listeyi/sonucu yenileyin."),
            _ => (500, "REQUEST_FAILED", "İstek tamamlanamadı; yazma sonucunu işlem kimliğiyle kontrol edin.")
        };
        if (ex is SessionRevokedException && context.Items.TryGetValue(ApiSessionFilter.ItemKey, out var value) && value is ApiSession session)
            context.RequestServices.GetRequiredService<ApiSessions>().Remove(session);
        app.Logger.LogWarning("API failure {Type} {TraceId}", ex.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status; await context.Response.WriteAsJsonAsync(new AccountingApp.Contracts.Error(code, message, context.TraceIdentifier));
    }
});
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.Ok(new { Status = "alive", ApiVersion = ApiContract.Version }));
app.MapAccountingEndpoints();
// Read-only compatibility check. This host never installs/upgrades/restores a DB.
if (options.AllowUserLocalDb)
    await LocalDbRuntime.EnsureStartedAsync(app.Services.GetRequiredService<ServerRuntime>().ConnectionString);
await app.Services.GetRequiredService<ServerRuntime>().VerifySchemaAsync(CancellationToken.None);
await app.RunAsync();
