using System.Net.Http;
using System.Text.Json;

namespace WinFormsApp2;

internal static class DesktopConnection
{
    private static string? frozen;
    internal static Uri? ApiAddress
    {
        get
        {
            if (frozen == null)
            {
                var prefs = UserPreferences.Load();
                frozen = prefs.UseCentralApi ? Normalize(prefs.CentralApiUrl).AbsoluteUri : "";
            }
            return frozen.Length == 0 ? null : new Uri(frozen);
        }
    }
    internal static bool IsRemote(string connection) => connection.StartsWith("api:", StringComparison.Ordinal);
    internal static Uri Normalize(string value) => Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
        uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0
        ? uri : throw new ArgumentException("Merkezi sunucu adresini https://sunucu:7443/ biçiminde girin.");
    internal static void Reset() => frozen = null;
    internal static async Task PrepareAsync()
    {
        if (ApiAddress is not { } uri) { await SchemaUpgrade.PrepareAsync(AppDatabase.ConnectionString); return; }
        using var client = RemoteApi.CreateClient(uri);
        using var response = await client.GetAsync("health/live");
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (body.RootElement.GetProperty("apiVersion").GetString() != AccountingApp.Contracts.ApiContract.Version)
            throw new InvalidOperationException("Merkezi sunucu API sürümü bu uygulamayla uyumlu değil.");
    }
    internal static bool Configure(IWin32Window owner)
    {
        var prefs = UserPreferences.Load();
        using var form = new Form { Text = "Bağlantı seçimi", ClientSize = new Size(560, 220), FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent };
        var remote = new CheckBox { Text = "Merkezi API kullan", Checked = prefs.UseCentralApi, AutoSize = true };
        var url = new TextBox { Text = prefs.CentralApiUrl, Width = 510 };
        var save = new Button { Text = "Kaydet", Width = 100 };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        layout.Controls.AddRange([remote, new Label { Text = "HTTPS sunucu adresi:", AutoSize = true }, url,
            new Label { Text = "Yerel mod mevcut veritabanı profilini kullanır. API hatasında yerel moda geçilmez.", Width = 510, Height = 38 }, save]);
        form.Controls.Add(layout); form.AcceptButton = save; AppTheme.Apply(form);
        save.Click += (_, _) =>
        {
            try
            {
                using var process = System.Diagnostics.Process.GetCurrentProcess();
                var instances = System.Diagnostics.Process.GetProcessesByName(process.ProcessName);
                try { if (instances.Any(p => p.Id != process.Id)) throw new InvalidOperationException("Bağlantıyı değiştirmeden önce diğer Muhasebe uygulaması pencerelerini kapatın."); }
                finally { foreach (var instance in instances) instance.Dispose(); }
                string pending = Path.Combine(UserPreferences.DirectoryPath, "payments");
                if (Directory.Exists(pending) && Directory.EnumerateFiles(pending, "*.pending").Any())
                    throw new InvalidOperationException("Bekleyen ödeme varken bağlantı değiştirilemez. Önce mevcut bağlantıyla giriş yapıp ödemenin sonucunu kontrol edin ve onaylayın.");
                if (remote.Checked) prefs.CentralApiUrl = Normalize(url.Text).AbsoluteUri;
                prefs.UseCentralApi = remote.Checked; prefs.Save(); Reset(); AppDatabase.Reset(); form.DialogResult = DialogResult.OK;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException)
            { MessageBox.Show(form, ex.Message, "Bağlantı kaydedilemedi"); }
        };
        return form.ShowDialog(owner) == DialogResult.OK;
    }
}
