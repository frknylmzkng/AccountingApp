namespace WinFormsApp2;

internal sealed partial class SettingsForm
{
    private void ArrangeSettings(Button save, Button create, Button restore, Button retry)
    {
        Text = "Muhasebe — Ayarlar"; ClientSize = new Size(1080, 720);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Ayarlar", "Bağlantı profilini, yedek klasörlerini ve görünümü yönetin."), 0, 0);
        SettingsPathField Field(string caption, TextBox field, string name, bool file)
        {
            field.Name = name;
            var browse = new Button { Name = name + "Browse", Text = "Seç…", AccessibleName = caption + " seç" };
            browse.Click += (_, _) =>
            {
                if (file)
                {
                    using var dialog = new OpenFileDialog { Filter = ReferenceEquals(field, logo) ? "PNG logo|*.png" : "Bağlantı profili|*.json", CheckFileExists = true };
                    if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.FileName;
                }
                else
                {
                    using var dialog = new FolderBrowserDialog();
                    if (dialog.ShowDialog(this) == DialogResult.OK) field.Text = dialog.SelectedPath;
                }
            };
            return new(caption, field, browse);
        }
        var connection = new SettingsCard("settingsConnection", "Veritabanı bağlantısı",
            [Field("Bağlantı profili (JSON):", config, "configurationPath", true)],
            new Label { Text = "Boş bırakırsanız uygulamanın appsettings.json dosyası kullanılır. Profil değişikliği kaydedildiğinde yeniden giriş gerekir." });
        var backups = new SettingsCard("settingsBackups", "Yedekleme ve geri yükleme",
            [Field("Ana yedek klasörü:", backup, "backupFolder", false), Field("Aynalama klasörü:", mirror, "mirrorFolder", false)],
            new Label { Text = "Yönetici oturumu açıkken saatlik kontrol: ana yedek 24 saatte bir, eksik aynalama sonraki kontrolde yeniden denenir. Son 14 yedek tutulur. Ana klasör boşsa kullanıcı uygulama klasörü kullanılır; yedekler personel verilerini içerir." });
        classic.Name = "classicTheme"; dark.Name = "darkTheme";
        logo.PlaceholderText = "İsteğe bağlı PNG dosyası";
        var appearance = new SettingsCard("settingsAppearance", "Görünüm ve fiş",
            [Field("Fiş logosu (PNG):", logo, "receiptLogo", true)], null, [classic, dark]);
        result.Name = "backupStatus"; result.Text = "Yedek ve aynalama durumu kontrol ediliyor…";
        var status = new SettingsCard("settingsStatus", "Yedek ve aynalama durumu", [], result);
        save.Name = "settingsSave"; create.Name = "settingsBackup"; retry.Name = "settingsRetryMirror"; restore.Name = "settingsRestore";
        var actions = new SettingsActions([save, create, retry, restore]);
        var workspace = new SettingsWorkspace([connection, backups, appearance, status], actions) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var targets = new[] { ("settingsShowConnection", "Bağlantı", connection), ("settingsShowBackups", "Yedekleme", backups),
            ("settingsShowAppearance", "Görünüm ve fiş", appearance), ("settingsShowStatus", "Yedek durumu", status) };
        foreach (var (name, caption, target) in targets)
        {
            var button = new Button { Name = name, Text = caption, Width = 118, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = navigation.Controls.Count };
            button.Click += (_, _) => { if (ReferenceEquals(target, connection)) workspace.AutoScrollPosition = Point.Empty; else workspace.ScrollControlIntoView(target); };
            navigation.Controls.Add(button);
        }
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2); Controls.Add(outer);
        AppTheme.Apply(this);
    }
}
