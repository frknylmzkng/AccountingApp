using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using WinFormsApp2;

namespace AccountingApp.Setup;

internal sealed class SetupWizard : Form
{
    private readonly ComboBox mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox address = new() { Dock = DockStyle.Fill };
    private readonly TextBox profile = new() { Dock = DockStyle.Fill };
    private readonly TextBox username = new() { Dock = DockStyle.Fill };
    private readonly TextBox password = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox confirmation = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly TextBox summary = new() { Dock = DockStyle.Fill, ReadOnly = true, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly Button next = new() { Text = "Devam", Width = 120, Height = 32 };
    private readonly Button back = new() { Text = "Geri", Width = 100, Height = 32 };
    private readonly TabControl pages = new() { Dock = DockStyle.Fill };
    private readonly string target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AccountingApp", BuildDiagnostics.Local.Version + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
    private bool busy, finished, openingReview;
    internal SetupWizard()
    {
        Text = "Muhasebe — Kurulum ve güncelleme"; ClientSize = new Size(760, 590); MinimumSize = new Size(650, 520); StartPosition = FormStartPosition.CenterScreen;
        mode.Items.AddRange(["Mevcut bağlantıyı koru (güncelleme)", "Merkezi HTTPS API'ye bağlan", "Mevcut yerel veritabanı profili", "Yeni yerel veritabanı ve ilk yönetici"]); mode.SelectedIndex = 0;
        var preferences = UserPreferences.Load(); address.Text = preferences.CentralApiUrl; profile.Text = preferences.DatabaseConfigurationPath;
        var welcome = new TabPage("1 — Bağlantı"); var review = new TabPage("2 — Kurulum özeti");
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(18), ColumnCount = 2, RowCount = 9 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Field(string caption, Control control, int row) { fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); fields.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row); fields.Controls.Add(control, 1, row); }
        Field("Kurulum türü:", mode, 0); Field("HTTPS sunucu adresi:", address, 1); Field("Yerel profil (JSON):", profile, 2);
        var browse = new Button { Text = "Profil seç…", Dock = DockStyle.Left, Width = 120 };
        fields.Controls.Add(browse, 1, 3); fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        browse.Click += (_, _) => { using var file = new OpenFileDialog { Filter = "Bağlantı profili|*.json", CheckFileExists = true }; if (file.ShowDialog(this) == DialogResult.OK) profile.Text = file.FileName; };
        Field("İlk yönetici adı:", username, 4); Field("Yeni parola:", password, 5); Field("Parola tekrar:", confirmation, 6);
        var note = new Label { Text = "Yeni kurulum yalnızca yeni ve benzersiz bir LocalDB veritabanı oluşturur. Mevcut veritabanı yeniden kurulmaz veya otomatik yükseltilmez. API sunucusu bu sihirbazla kurulmaz.", Dock = DockStyle.Fill };
        fields.Controls.Add(note, 0, 7); fields.SetColumnSpan(note, 2); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); welcome.Controls.Add(fields); review.Controls.Add(summary); pages.TabPages.AddRange([welcome, review]);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var cancel = new Button { Text = "Kapat", Width = 100, Height = 32 }; cancel.Click += (_, _) => { if (!busy) Close(); };
        buttons.Controls.AddRange([cancel, next, back]); Controls.Add(pages); Controls.Add(buttons); AppTheme.Apply(this);
        mode.SelectedIndexChanged += (_, _) => UpdateFields(); UpdateFields();
        pages.Selecting += (_, e) =>
        {
            if (busy || (e.TabPageIndex == 1 && !openingReview)) { e.Cancel = true; return; }
            if (e.TabPageIndex == 0) { summary.Clear(); back.Enabled = false; next.Text = "Devam"; }
        };
        back.Enabled = false; back.Click += (_, _) => { pages.SelectedIndex = 0; back.Enabled = false; next.Text = "Devam"; summary.Clear(); };
        next.Click += async (_, _) =>
        {
            if (busy) return;
            if (finished) { Close(); return; }
            if (pages.SelectedIndex == 0)
            {
                summary.Text = $"Ürün: {BuildDiagnostics.Local.Version}\r\nDosya sürümü: {BuildDiagnostics.Local.FileVersion}\r\nKaynak durumu: {BuildDiagnostics.Local.SourceState}\r\nHedef: {target}\r\nSeçim: {mode.Text}\r\n\r\nPaketin dosya özetleri doğrulanacak. Açık masaüstü uygulaması veya bekleyen ödeme varsa kurulum durdurulacak.\r\nYeni sürüm ayrı klasöre kopyalanır; eski uygulama ve mevcut veriler silinmez. Bağlantı ve Muhasebe - Güncel kısayolu ancak hazırlık tamamlanırsa güncellenir.\r\n\r\nYeni veritabanı seçeneği yeni bir MDF oluşturur ve bu paketin şemasını uygular. Diğer seçeneklerde mevcut şema değiştirilmez.\r\n\r\nKur düğmesine basarak seçilen kurulum işlemini başlatabilirsiniz.";
                summary.AppendText("\r\n\r\nAPI adresi (seçildiyse): " + address.Text + "\r\nYerel profil (seçildiyse): " + profile.Text + "\r\nİlk yönetici adı (yeni kurulum): " + username.Text);
                openingReview = true; pages.SelectedIndex = 1; openingReview = false; back.Enabled = true; next.Text = "Kur"; return;
            }
            busy = true; next.Enabled = back.Enabled = false;
            try { await InstallAsync(); finished = true; pages.Enabled = false; next.Text = "Bitir"; summary.AppendText("\r\n\r\nKurulum tamamlandı. Masaüstündeki Muhasebe - Güncel kısayolunu açabilirsiniz."); }
            catch (Exception ex)
            {
                AppLog.Failure(ex, "Setup.Install");
                MessageBox.Show(this, ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException or DatabaseConfigurationException or DatabaseSetupException or LocalDbStartupException ? ex.Message : "Kurulum tamamlanamadı. Önceki kısayol ve tercihler korunmaya çalışıldı; yeni klasörü ve kurulum günlüğünü kontrol edin.", "Kurulum"); back.Enabled = true;
            }
            finally { password.Clear(); confirmation.Clear(); busy = false; next.Enabled = true; }
        };
        FormClosing += (_, e) => { if (busy) e.Cancel = true; };
    }
    private void UpdateFields()
    { address.Enabled = mode.SelectedIndex == 1; profile.Enabled = mode.SelectedIndex == 2; username.Enabled = password.Enabled = confirmation.Enabled = mode.SelectedIndex == 3; }
    private async Task InstallAsync()
    {
        if (Process.GetProcessesByName("WinFormsApp2").Any()) throw new InvalidOperationException("Önce açık Muhasebe uygulamalarını kapatın.");
        string payments = Path.Combine(UserPreferences.DirectoryPath, "payments");
        if (Directory.Exists(payments) && Directory.EnumerateFiles(payments, "*.pending").Any()) throw new InvalidOperationException("Önce eski bağlantıda bekleyen ödeme sonucunu kontrol edip onaylayın; ödeme günlüklerini silmeyin.");
        string root = AppContext.BaseDirectory; await VerifyPackageAsync(root);
        var prefs = UserPreferences.Load(); string? selectedProfile = null;
        if (mode.SelectedIndex == 1) { prefs.UseCentralApi = true; prefs.CentralApiUrl = DesktopConnection.Normalize(address.Text).AbsoluteUri; }
        if (mode.SelectedIndex == 2) { selectedProfile = Path.GetFullPath(profile.Text.Trim()); prefs.UseCentralApi = false; }
        if (mode.SelectedIndex == 0 && !prefs.UseCentralApi)
        {
            selectedProfile = prefs.DatabaseConfigurationPath;
            if (string.IsNullOrWhiteSpace(selectedProfile)) selectedProfile = ExistingProfile() ?? Path.Combine(root, "payload", "appsettings.json");
        }
        if (prefs.UseCentralApi && mode.SelectedIndex != 3)
        {
            using var http = RemoteApi.CreateClient(DesktopConnection.Normalize(prefs.CentralApiUrl));
            using var response = await http.GetAsync("health/live"); response.EnsureSuccessStatusCode();
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (body.RootElement.GetProperty("apiVersion").GetString() != AccountingApp.Contracts.ApiContract.Version) throw new InvalidOperationException("API sözleşmesi uyumsuz.");
        }
        if (selectedProfile != null) await CheckExistingAsync(selectedProfile);
        string? newPassword = null;
        if (mode.SelectedIndex == 3) { _ = LocalDbRuntime.FindUtility(); PasswordPolicy.Validate(password.Text, confirmation.Text); AdminStore.ValidateNewAccount(username.Text, password.Text); newPassword = password.Text; prefs.UseCentralApi = false; }
        if (Directory.Exists(target)) throw new InvalidOperationException("Hedef klasör zaten var; kurulum yeniden açılmalı.");
        Directory.CreateDirectory(target);
        await Task.Run(() => CopyPayload(Path.Combine(root, "payload"), target));
        File.Copy(Path.Combine(root, "package-manifest.json"), Path.Combine(target, "distribution-manifest.json"));
        using (var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(target, "distribution-manifest.json"))))
            foreach (var entry in manifest.RootElement.GetProperty("files").EnumerateArray())
            {
                string relative = entry.GetProperty("path").GetString()!;
                if (!relative.StartsWith("payload/", StringComparison.Ordinal)) continue;
                string copied = Path.GetFullPath(relative[8..], target);
                if (!copied.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Kopyalanan paket yolu geçersiz.");
                using var bytes = File.OpenRead(copied);
                if (bytes.Length != entry.GetProperty("length").GetInt64() || Convert.ToHexString(await SHA256.HashDataAsync(bytes)) != entry.GetProperty("sha256").GetString()) throw new InvalidOperationException("Kopyalanan paket doğrulanamadı; tercihler değiştirilmedi.");
            }
        string installedProfile = Path.Combine(target, "appsettings.json");
        if (selectedProfile != null) { File.Copy(selectedProfile, installedProfile, true); prefs.DatabaseConfigurationPath = installedProfile; }
        if (mode.SelectedIndex == 3)
        {
            string id = Guid.NewGuid().ToString("N"), file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AccountingApp", "Data", "Muhasebe-" + id + ".mdf");
            File.WriteAllText(installedProfile, JsonSerializer.Serialize(new { Database = new { Server = "(LocalDB)\\MSSQLLocalDB", Name = "AccountingApp_" + id, FilePath = file, ConnectTimeoutSeconds = 30 } }));
            var settings = DatabaseSettings.Load(installedProfile); await LocalDbRuntime.EnsureStartedAsync(settings.CreateConnectionString(false));
            string admin = username.Text; string secret = newPassword!;
            await Task.Run(() => new DatabaseInstaller(settings).Install(() => new(admin, secret)));
            await SchemaUpgrade.PrepareAsync(settings.CreateConnectionString()); prefs.DatabaseConfigurationPath = installedProfile;
        }
        await PublishPreferencesAndShortcutAsync(prefs);
    }
    private static async Task CheckExistingAsync(string profile)
    {
        var settings = DatabaseSettings.Load(profile); string cs = settings.CreateConnectionString(); await LocalDbRuntime.EnsureStartedAsync(cs);
        using var connection = new SqlConnection(cs); await connection.OpenAsync();
        var scope = BackupScope.Read(connection);
        if (settings.FilePath.Length > 0 && !string.Equals(Path.GetFullPath(settings.FilePath), Path.GetFullPath(scope.DataFile), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Bağlantı profilindeki MDF ile bağlı veritabanının dosyası uyuşmuyor.");
        using var command = new SqlCommand("SELECT ScriptName FROM dbo.__MigrationJournal", connection);
        using var reader = await command.ExecuteReaderAsync(); var actual = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync()) actual.Add(reader.GetString(0));
        var assembly = typeof(SchemaUpgrade).Assembly;
        var expected = assembly.GetManifestResourceNames().Where(n => n.Contains(".database.upgrades.", StringComparison.Ordinal)).Select(n =>
        { using var stream = new StreamReader(assembly.GetManifestResourceStream(n)!); return n + ":" + Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(stream.ReadToEnd().Replace("\r\n", "\n")))); }).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(actual)) throw new InvalidOperationException("Mevcut veritabanı bu paketle uyumlu değil. Önce yedekli şema yükseltmesi ayrı bakım işlemiyle tamamlanmalı; kurulum şemayı değiştirmedi.");
    }
    private static async Task VerifyPackageAsync(string root)
    {
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "package-manifest.json")));
        var info=BuildDiagnostics.Local;
        string runtime=typeof(BuildDiagnostics).Assembly.GetCustomAttributes<System.Reflection.AssemblyMetadataAttribute>().Single(a=>a.Key=="AccountingRuntimeVersion").Value!;
        if (json.RootElement.GetProperty("format").GetInt32() != 1 || json.RootElement.GetProperty("version").GetString() != info.Version || json.RootElement.GetProperty("kind").GetString() != "desktop"
            ||json.RootElement.GetProperty("fileVersion").GetString()!=info.FileVersion||json.RootElement.GetProperty("apiVersion").GetString()!=info.ApiVersion
            ||json.RootElement.GetProperty("expectedSchema").GetInt32().ToString()!=info.ExpectedSchema||json.RootElement.GetProperty("commit").GetString()!=info.Commit
            ||json.RootElement.GetProperty("sourceState").GetString()!=info.SourceState||json.RootElement.GetProperty("runtime").GetString()!=runtime) throw new InvalidOperationException("Kurulum paketi, kaynak kimliği veya şema/API/runtime sürümü uyuşmuyor.");
        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in json.RootElement.GetProperty("files").EnumerateArray())
        {
            string relative = entry.GetProperty("path").GetString()!;
            if(relative.Contains('\\')||relative.Split('/').Any(p=>p.Length==0||p is "." or ".."||p.Contains(':')||p.EndsWith(' ')||p.EndsWith('.')))throw new InvalidOperationException("Paket dosya yolu geçersiz.");
            string file = Path.GetFullPath(relative, root);
            if (!file.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !expected.Add(file)) throw new InvalidOperationException("Paket dosya yolu geçersiz.");
            for (string? parent = Path.GetDirectoryName(file); parent != null && parent.Length >= Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar).Length; parent = Path.GetDirectoryName(parent))
                if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Paket klasöründe bağlantı kullanılamaz.");
            using var stream = File.OpenRead(file);
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || stream.Length != entry.GetProperty("length").GetInt64() || Convert.ToHexString(await SHA256.HashDataAsync(stream)) != entry.GetProperty("sha256").GetString()) throw new InvalidOperationException("Paket dosyası doğrulanamadı.");
        }
        string payload = Path.Combine(root, "payload");
        if (!expected.Contains(Path.Combine(payload, "WinFormsApp2.exe"))) throw new InvalidOperationException("Uygulama yükü eksik.");
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
            foreach (string path in Directory.EnumerateFileSystemEntries(pending.Pop()))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Paket yükünde bağlantı var.");
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(path);
                else if (!expected.Contains(Path.GetFullPath(path))&&!string.Equals(Path.GetFullPath(path),Path.Combine(Path.GetFullPath(root),"package-manifest.json"),StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Paket yükünde beklenmeyen dosya var. Paketi yeni ve boş bir klasöre çıkarın.");
            }
    }
    private static void CopyPayload(string source, string destination)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        { string targetFile = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!); File.Copy(file, targetFile, false); }
    }
    private static string? ExistingProfile()
    {
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Muhasebe - Güncel.lnk");
        if (!File.Exists(shortcut)) return null;
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(shortcut);
        try { string exe = link.TargetPath; return Path.Combine(Path.GetDirectoryName(exe)!, "appsettings.json"); }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }
    private async Task PublishPreferencesAndShortcutAsync(UserPreferences prefs)
    {
        string rollback = Path.Combine(target, "installation-before"); Directory.CreateDirectory(rollback);
        string prefFile = Path.Combine(UserPreferences.DirectoryPath, "preferences.json"); bool hadFile = File.Exists(prefFile);
        if (hadFile) File.Copy(prefFile, Path.Combine(rollback, "preferences.json"));
        using var registry = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\AccountingApp"); object? oldRegistry = registry.GetValue("Preferences");
        string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Muhasebe - Güncel.lnk"); bool hadShortcut = File.Exists(shortcut);
        if (hadShortcut) File.Copy(shortcut, Path.Combine(rollback, "shortcut.lnk"));
        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        dynamic link = shell.CreateShortcut(shortcut);
        try
        {
            prefs.Save(); link.TargetPath = Path.Combine(target, "WinFormsApp2.exe"); link.WorkingDirectory = target; link.Description = "Muhasebe " + BuildDiagnostics.Local.Version; link.IconLocation = Path.Combine(target, "WinFormsApp2.exe") + ",0"; link.Save();
            await File.WriteAllTextAsync(Path.Combine(target, "installation.json"), JsonSerializer.Serialize(new { Version=BuildDiagnostics.Local.Version, InstalledUtc=DateTime.UtcNow, Mode=mode.SelectedIndex, PreviousShortcutPreserved=hadShortcut }));
        }
        catch
        {
            if (hadFile) File.Copy(Path.Combine(rollback, "preferences.json"), prefFile, true); else if (File.Exists(prefFile)) File.Delete(prefFile);
            if (oldRegistry != null) registry.SetValue("Preferences", oldRegistry); else registry.DeleteValue("Preferences", false);
            if (hadShortcut) File.Copy(Path.Combine(rollback, "shortcut.lnk"), shortcut, true); else if (File.Exists(shortcut)) File.Delete(shortcut);
            throw;
        }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
    }
}
