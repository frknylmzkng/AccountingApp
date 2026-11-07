namespace WinFormsApp2;

internal sealed partial class SettingsForm : SessionForm
{
    private readonly TextBox config = new() { Width = 620 };
    private readonly TextBox backup = new() { Width = 620 };
    private readonly TextBox mirror = new() { Width = 620 };
    private readonly TextBox logo = new() { Width = 620 };
    private readonly CheckBox dark = new() { Text = "Koyu tema", AutoSize = true };
    private readonly CheckBox classic = new() { Text = "Windows 2000 · Klasik görünüm", AutoSize = true };
    private readonly Label result = new() { AutoSize = true, MaximumSize = new Size(800, 0) };
    public SettingsForm()
    {
        var save = new Button { Text = "Ayarları kaydet" };
        var create = new Button { Text = "Şimdi yedekle" };
        var restore = new Button { Text = "Yedekten geri yükle…" };
        var retry = new Button { Text = "Aynalamayı yeniden dene" };
        ArrangeSettings(save, create, restore, retry);
        var prefs = UserPreferences.Load(); config.Text = prefs.DatabaseConfigurationPath; backup.Text = prefs.BackupFolder; mirror.Text = prefs.MirrorFolder; logo.Text = prefs.LogoPath; dark.Checked = prefs.DarkTheme;
        classic.Checked = prefs.ClassicTheme; dark.Enabled = !classic.Checked;
        TrackChanges("Ayarlar", config, backup, mirror, logo, classic, dark);
        classic.CheckedChanged += (_, _) => dark.Enabled = !classic.Checked;
        save.Click += Save; create.Click += Backup; restore.Click += Restore;
        retry.Click += RetryMirror;
        if (DesktopConnection.ApiAddress != null)
        {
            config.Enabled = backup.Enabled = mirror.Enabled = false;
            foreach (var control in AppTheme.Descendants(this).Where(c => c.Name is "configurationPathBrowse" or "backupFolderBrowse" or "mirrorFolderBrowse")) control.Enabled = false;
            create.Enabled = restore.Enabled = retry.Enabled = false;
            config.Text = DesktopConnection.ApiAddress.AbsoluteUri;
            AcceptTrackedChanges("Ayarlar");
        }
        RefreshAction = RefreshBackupStatusAsync;
        Load += async (_, _) => { if (!PreviewOnly) await RefreshBackupStatusAsync(); };
    }
    private async void Save(object? sender, EventArgs e)
    {
        var prefs = UserPreferences.Load(); string selected = config.Text.Trim(); bool changed = User.Remote == null && !string.Equals(prefs.DatabaseConfigurationPath, selected, StringComparison.OrdinalIgnoreCase);
        bool done = await RunOperationAsync(async token =>
        {
            User.DemandAdmin();
            await AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Admin, token);
            if (changed && User.Remote == null)
            {
                var settings = DatabaseSettings.Load(string.IsNullOrWhiteSpace(selected) ? Path.Combine(AppContext.BaseDirectory, "appsettings.json") : selected);
                await using var connection = new Microsoft.Data.SqlClient.SqlConnection(settings.CreateConnectionString()); await connection.OpenAsync(token);
                using var check = new Microsoft.Data.SqlClient.SqlCommand("IF OBJECT_ID(N'dbo.admin') IS NULL OR OBJECT_ID(N'dbo.Calisan') IS NULL THROW 51021,N'Uygulama veritabanı değil.',1", connection); await check.ExecuteNonQueryAsync(token);
            }
            if (User.Remote == null) { prefs.DatabaseConfigurationPath = selected; prefs.BackupFolder = backup.Text.Trim(); prefs.MirrorFolder = mirror.Text.Trim(); }
            prefs.LogoPath = logo.Text.Trim(); prefs.DarkTheme = dark.Checked; prefs.ClassicTheme = classic.Checked; prefs.Save();
        }, changed ? "Profil doğrulandı; yeniden giriş gerekiyor." : "Ayarlar kaydedildi.");
        if (done && !IsDisposed) { AcceptTrackedChanges("Ayarlar"); if (changed && User.Remote == null) RequestLogout("Bağlantı profili kaydedildi. Yeni profille yeniden giriş yapın."); else { AppTheme.Apply(this); await RefreshBackupStatusAsync(); } }
    }
    private string BackupFolder => string.IsNullOrWhiteSpace(backup.Text) ? BackupService.DefaultFolder : backup.Text.Trim();
    private async Task RefreshBackupStatusAsync() => await RunOperationAsync(async token =>
    {
        if (User.Remote != null) { await User.Remote.ValidateAsync(AccountAccess.Admin, token); result.Text = "Merkezi API bağlantısı etkin. Güvenli yedek / destek ekranından sunucuda şifreli yedek oluşturabilir, doğrulayabilir ve aynalamayı yeniden deneyebilirsiniz. Anahtar yönetimi ve geri yükleme sunucu bilgisayarının ayrı bakım sürecindedir. Bağlantı seçimi giriş ekranındadır."; return; }
        string text = await new BackupService(AppDatabase.ConnectionString, User).StatusAsync(BackupFolder, mirror.Text.Trim(), token);
        if (!IsDisposed) result.Text = text;
    });
    private async void Backup(object? sender, EventArgs e)
    {
        string folder = BackupFolder, destination = mirror.Text.Trim();
        await RunOperationAsync(token => new BackupService(AppDatabase.ConnectionString, User).BackupAsync(folder, destination, token), "Yerel yedek ve seçili aynalama tamamlandı.");
        if (!IsDisposed && !Lifetime.IsCancellationRequested) await RefreshBackupStatusAsync();
    }
    private async void RetryMirror(object? sender, EventArgs e)
    {
        string folder = BackupFolder, destination = mirror.Text.Trim();
        await RunOperationAsync(token => new BackupService(AppDatabase.ConnectionString, User).RetryMirrorAsync(folder, destination, token), "Aynalama tamamlandı; yeni yerel yedek oluşturulmadı.");
        if (!IsDisposed && !Lifetime.IsCancellationRequested) await RefreshBackupStatusAsync();
    }
    private async void Restore(object? sender, EventArgs e)
    {
        if (!ConfirmDiscardChanges("Veritabanını geri yükleyip oturumu sonlandırma")) return;
        BackupScope? target = null;
        if (!await RunOperationAsync(async token =>
        {
            await AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Admin, token);
            target = await BackupScope.ReadAsync(AppDatabase.ConnectionString, token);
        }) || IsDisposed || target == null) return;
        using var file = new OpenFileDialog { Title = "Bu kapsama ait şifreli yedeği veya eski v2 yedeği seçin", Filter = "Şifreli uygulama yedeği|AccountingApp-v2-*.bak.abk|Eski v2 yedeği (manifesti yanında olmalı)|AccountingApp-v2-*.bak", CheckFileExists = true }; if (file.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, $"Hedef veritabanı: {target.Catalog}\nKapsam: {target.Key}\nVeritabanı kimliği: {target.FamilyId:D}\n\nSeçilen yedek mevcut verilerin yerini alacak. Önce güvenlik yedeği alınacak; açık bağlantılar kesilecek ve yeniden giriş istenecek. Kimlik ve kapsam eşleşmezse işlem yapılmaz. Devam edilsin mi?\n" + file.FileName, "Geri yükleme onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => new BackupService(AppDatabase.ConnectionString, User).RestoreAsync(file.FileName, string.IsNullOrWhiteSpace(backup.Text) ? BackupService.DefaultFolder : backup.Text.Trim(), token), "Geri yüklendi.") && !IsDisposed) RequestLogout("Veritabanı geri yüklendi. Yeniden giriş yapın.");
    }
}
