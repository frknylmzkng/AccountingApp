namespace WinFormsApp2;

internal sealed partial class SettingsForm
{
    private void ArrangeSettings(Button save, Button create, Button restore, Button retry)
    {
        Text = "Muhasebe — Ayarlar"; ClientSize = new Size(980, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
        foreach (int height in new[] { 55, 110, 172, 110, 46 }) outer.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Ayarlar", "Bağlantı profilini, yedek klasörlerini ve görünümü yönetin."), 0, 0);
        TableLayoutPanel Section(string title, int row, int rows)
        {
            var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 8, 3, 3) };
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = rows };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
            group.Controls.Add(table); outer.Controls.Add(group, 0, row); return table;
        }
        void Field(TableLayoutPanel table, string caption, TextBox field, string name, int row, bool file)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            table.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            field.Name = name; field.Dock = DockStyle.Fill; field.Margin = new Padding(3, 8, 8, 3); field.TabIndex = row * 3 + 1; table.Controls.Add(field, 1, row);
            var browse = new Button { Text = "Seç…", Dock = DockStyle.Fill, Margin = new Padding(3, 4, 3, 4), TabIndex = row * 3 + 2 };
            table.Controls.Add(browse, 2, row);
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
        }
        void Note(TableLayoutPanel table, string text, int row)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var label = new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            table.Controls.Add(label, 0, row); table.SetColumnSpan(label, 3);
        }
        var connection = Section("Veritabanı bağlantısı", 1, 2);
        Field(connection, "Bağlantı profili (JSON):", config, "configurationPath", 0, true);
        Note(connection, "Boş bırakırsanız uygulamanın appsettings.json dosyası kullanılır. Profil değişikliği kaydedildiğinde yeniden giriş gerekir.", 1);
        var backups = Section("Yedekleme ve geri yükleme", 2, 3);
        Field(backups, "Ana yedek klasörü:", backup, "backupFolder", 0, false);
        Field(backups, "Aynalama klasörü:", mirror, "mirrorFolder", 1, false);
        Note(backups, "Yönetici oturumu açıkken saatlik kontrol: ana yedek 24 saatte bir, eksik aynalama sonraki kontrolde yeniden denenir. Son 14 yedek tutulur. Ana klasör boşsa kullanıcı uygulama klasörü kullanılır; yedekler personel verilerini içerir.", 2);
        var appearance = Section("Görünüm ve fiş", 3, 2);
        Field(appearance, "Fiş logosu (PNG):", logo, "receiptLogo", 0, true);
        logo.PlaceholderText = "İsteğe bağlı PNG dosyası";
        appearance.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var themes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        themes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290)); themes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); themes.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        classic.Dock = dark.Dock = DockStyle.Fill; classic.TabIndex = 0; dark.TabIndex = 1;
        themes.Controls.Add(classic, 0, 0); themes.Controls.Add(dark, 1, 0); appearance.Controls.Add(themes, 0, 1); appearance.SetColumnSpan(themes, 3);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1, Margin = new Padding(3, 4, 3, 4) };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 192)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Button[] buttons = [save, create, retry, restore];
        for (int i = 0; i < buttons.Length; i++) { buttons[i].Dock = DockStyle.Fill; buttons[i].Margin = new Padding(3); buttons[i].TabIndex = i; actions.Controls.Add(buttons[i], i + 1, 0); }
        outer.Controls.Add(actions, 0, 4);
        result.AutoSize = false; result.MaximumSize = Size.Empty; result.Dock = DockStyle.Fill; result.TextAlign = ContentAlignment.TopLeft; result.Padding = new Padding(3, 8, 3, 3);
        outer.Controls.Add(result, 0, 5); Controls.Add(outer);
        Load += (_, _) => MinimumSize = LogicalToDeviceUnits(new Size(900, 670));
        AppTheme.Apply(this);
    }
}
