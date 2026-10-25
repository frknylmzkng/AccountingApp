namespace WinFormsApp2;

internal sealed class SettingsForm : SessionForm
{
    private readonly TextBox config = new() { Width = 620 };
    private readonly TextBox backup = new() { Width = 620 };
    private readonly TextBox mirror = new() { Width = 620 };
    private readonly TextBox logo = new() { Width = 620 };
    private readonly CheckBox dark = new() { Text = "Koyu tema", AutoSize = true };
    private readonly Label result = new() { AutoSize = true, MaximumSize = new Size(800, 0) };
    public SettingsForm()
    {
        Text = "Bağlantı, görünüm ve yedekleme"; ClientSize = new Size(900, 600);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(25), AutoScroll = true };
        void Field(string label, TextBox field, bool file)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true });
            var line = new FlowLayoutPanel { AutoSize = true }; var browse = new Button { Text = "Seç…", AutoSize = true }; line.Controls.AddRange([field, browse]); panel.Controls.Add(line);
            browse.Click += (_, _) =>
            {
                if (file) { using var dialog = new OpenFileDialog { Filter = ReferenceEquals(field, logo) ? "PNG logo|*.png" : "Bağlantı profili|*.json", CheckFileExists = true }; if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.FileName; }
                else { using var dialog = new FolderBrowserDialog(); if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath; }
            };
        }
        Field("Bağlantı profili JSON (boş: uygulamanın appsettings.json dosyası)", config, true);
        Field("Ana yedek klasörü (boş: kullanıcı uygulama klasörü)", backup, false);
        Field("İsteğe bağlı USB / OneDrive aynalama klasörü", mirror, false); Field("Fiş logosu (PNG, isteğe bağlı)", logo, true);
        panel.Controls.Add(dark); panel.Controls.Add(new Label { Text = "Otomatik: 24 saatte bir, son 14 yedek. Yedekler personel verilerini içerir; klasör erişimini buna göre seçin.", AutoSize = true, MaximumSize = new Size(800, 0) });
        var buttons = new FlowLayoutPanel { AutoSize = true }; var save = new Button { Text = "Ayarları kaydet", AutoSize = true }; var create = new Button { Text = "Şimdi yedekle", AutoSize = true }; var restore = new Button { Text = "Yedekten geri yükle…", AutoSize = true };
        buttons.Controls.AddRange([save, create, restore]); panel.Controls.Add(buttons); panel.Controls.Add(result); Controls.Add(panel);
        var prefs = UserPreferences.Load(); config.Text = prefs.DatabaseConfigurationPath; backup.Text = prefs.BackupFolder; mirror.Text = prefs.MirrorFolder; logo.Text = prefs.LogoPath; dark.Checked = prefs.DarkTheme;
        save.Click += Save; create.Click += Backup; restore.Click += Restore;
    }
    private async void Save(object? sender, EventArgs e)
    {
        var prefs = UserPreferences.Load(); string selected = config.Text.Trim(); bool changed = !string.Equals(prefs.DatabaseConfigurationPath, selected, StringComparison.OrdinalIgnoreCase);
        bool done = await RunOperationAsync(async token =>
        {
            User.DemandAdmin();
            await AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Admin, token);
            if (changed)
            {
                var settings = DatabaseSettings.Load(string.IsNullOrWhiteSpace(selected) ? Path.Combine(AppContext.BaseDirectory, "appsettings.json") : selected);
                await using var connection = new Microsoft.Data.SqlClient.SqlConnection(settings.CreateConnectionString()); await connection.OpenAsync(token);
                using var check = new Microsoft.Data.SqlClient.SqlCommand("IF OBJECT_ID(N'dbo.admin') IS NULL OR OBJECT_ID(N'dbo.Calisan') IS NULL THROW 51021,N'Uygulama veritabanı değil.',1", connection); await check.ExecuteNonQueryAsync(token);
            }
            prefs.DatabaseConfigurationPath = selected; prefs.BackupFolder = backup.Text.Trim(); prefs.MirrorFolder = mirror.Text.Trim(); prefs.LogoPath = logo.Text.Trim(); prefs.DarkTheme = dark.Checked; prefs.Save();
        }, changed ? "Profil doğrulandı; yeniden giriş gerekiyor." : "Ayarlar kaydedildi.");
        if (done && !IsDisposed) { if (changed) RequestLogout(); else AppTheme.Apply(this); }
    }
    private async void Backup(object? sender, EventArgs e) => await RunOperationAsync(async token =>
    {
        string folder = string.IsNullOrWhiteSpace(backup.Text) ? BackupService.DefaultFolder : backup.Text.Trim();
        string path = await new BackupService(AppDatabase.ConnectionString, User).BackupAsync(folder, mirror.Text.Trim(), token);
        if (!IsDisposed) result.Text = "Doğrulanmış yedek: " + path;
    }, "Yedek alındı ve doğrulandı.");
    private async void Restore(object? sender, EventArgs e)
    {
        using var file = new OpenFileDialog { Filter = "Uygulama yedeği|AccountingApp-*.bak", CheckFileExists = true }; if (file.ShowDialog(this) != DialogResult.OK) return;
        if (MessageBox.Show(this, "Seçilen yedek mevcut verilerin yerini alacak. Önce güvenlik yedeği alınacak; açık bağlantılar kesilecek ve yeniden giriş istenecek. Devam edilsin mi?\n" + file.FileName, "Geri yükleme onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => new BackupService(AppDatabase.ConnectionString, User).RestoreAsync(file.FileName, string.IsNullOrWhiteSpace(backup.Text) ? BackupService.DefaultFolder : backup.Text.Trim(), token), "Geri yüklendi.") && !IsDisposed) RequestLogout();
    }
}
