using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal static class BuildDiagnostics
{
    internal static Wire.BuildInfo Local
    {
        get
        {
            var assembly = typeof(BuildDiagnostics).Assembly;
            string info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
            string Metadata(string key) => assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(m => m.Key == key)?.Value ?? "unknown";
            return new(info.Split('+')[0], assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "unknown", info,
                info.Contains('+') ? info[(info.IndexOf('+') + 1)..] : "unknown", Metadata("SourceTreeState"), Metadata("AccountingSchemaVersion"), Wire.ApiContract.Version);
        }
    }
    internal static async Task<Wire.Diagnostics> ReadAsync(string connectionString, UserSession user, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).SendAsync<Wire.Diagnostics>("diagnostics", null, token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, user.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read, token).ConfigureAwait(false);
        using var command = work.Command("SELECT ScriptName FROM dbo.__MigrationJournal");
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var applied = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(token).ConfigureAwait(false)) applied.Add(reader.GetString(0));
        var assembly = typeof(SchemaUpgrade).Assembly;
        var expected = assembly.GetManifestResourceNames().Where(n => n.Contains(".database.upgrades.", StringComparison.Ordinal)).Select(n =>
        {
            using var source = new StreamReader(assembly.GetManifestResourceStream(n)!);
            return n + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(source.ReadToEnd().Replace("\r\n", "\n"))));
        }).ToHashSet(StringComparer.Ordinal);
        await reader.DisposeAsync().ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false);
        return new(Local, applied.Count, expected.SetEquals(applied));
    }
}

internal sealed class AboutForm : Form
{
    internal event EventHandler? SessionRevoked;
    private readonly TextBox details = new() { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
    private readonly CancellationTokenSource lifetime = new();
    internal AboutForm(UserSession user)
    {
        Text = "Muhasebe — Hakkında"; ClientSize = new Size(650, 400); MinimumSize = new Size(520, 300); StartPosition = FormStartPosition.CenterParent;
        var close = new Button { Text = "Kapat", Dock = DockStyle.Bottom, Height = 32, DialogResult = DialogResult.Cancel };
        Controls.Add(details); Controls.Add(close); CancelButton = close; AppTheme.Apply(this);
        details.Text = Describe("Masaüstü", BuildDiagnostics.Local) + "\r\nVeritabanı/sunucu bilgisi alınıyor…";
        Shown += async (_, _) =>
        {
            try
            {
                var info = await BuildDiagnostics.ReadAsync(AppDatabase.ConnectionString, user, lifetime.Token);
                if (!IsDisposed) details.Text = Describe("Masaüstü", BuildDiagnostics.Local) + "\r\n" + Describe(user.Remote == null ? "Veritabanı istemcisi" : "API sunucusu", info.Build) +
                    $"\r\nUygulanmış yükseltme sayısı: {info.AppliedUpgrades}\r\nŞema: {(info.SchemaMatches ? "Paketle uyumlu" : "Paketle uyuşmuyor")}";
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex)
            {
                if (!IsDisposed) details.AppendText("\r\nSunucu/şema bilgisi alınamadı. Eski API sürümünde bu bilgi bulunmayabilir.");
                if (ex is SessionRevokedException) { Close(); SessionRevoked?.Invoke(this, EventArgs.Empty); }
            }
        };
        FormClosed += (_, _) => lifetime.Cancel();
        Disposed += (_, _) => lifetime.Dispose();
    }
    private static string Describe(string title, Wire.BuildInfo info) => $"{title}\r\nSürüm: {info.Version}\r\nDosya sürümü: {info.FileVersion}\r\nÜrün: {info.InformationalVersion}\r\nKaynak commit referansı: {info.Commit}\r\nKaynak durumu: {info.SourceState}\r\nBeklenen şema: {info.ExpectedSchema}\r\nAPI sözleşmesi: {info.ApiVersion}\r\n";
}
