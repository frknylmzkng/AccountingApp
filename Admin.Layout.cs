namespace WinFormsApp2;

public partial class Admin
{
    private void ArrangeAdmin(Button deactivate, Button activate, Button reset)
    {
        SuspendLayout();
        // Reuse the guarded account actions and original bound records.
        Control[] retained = [txtUsername, txtPassword, role, ekle_btn, sil_btn, deactivate, activate, reset, adminView];
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — Yönetici"; ClientSize = new Size(1120, 610);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 210)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Hesap yönetimi", "Yeni hesap ekleyin; mevcut hesaplar için aşağıdaki listeden bir satır seçin."), 0, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var create = new GroupBox { Text = "Yeni hesap", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 3; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Field(string caption, Control field, int row)
        {
            fields.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            field.Dock = DockStyle.Fill; field.Margin = new Padding(3, 5, 3, 3); field.TabIndex = row;
            fields.Controls.Add(field, 1, row);
        }
        Field("Kullanıcı adı:", txtUsername, 0); Field("Parola:", txtPassword, 1); Field("Yetki:", role, 2);
        ekle_btn.Text = "Hesap &ekle"; ekle_btn.AutoSize = false; ekle_btn.Size = new Size(138, 30); ekle_btn.Anchor = AnchorStyles.Right; ekle_btn.TabIndex = 3;
        fields.Controls.Add(ekle_btn, 1, 3); create.Controls.Add(fields); cards.Controls.Add(create, 0, 0);
        var selected = new GroupBox { Text = "Listeden seçilen hesap", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Button[] buttons = [activate, deactivate, reset, sil_btn];
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].AutoSize = false; buttons[i].Dock = DockStyle.Fill; buttons[i].Margin = new Padding(3, 5, 3, 5); buttons[i].TabIndex = i;
            actions.Controls.Add(buttons[i], i % 2, i / 2);
        }
        sil_btn.Text = "Hesabı &sil";
        var note = new Label { Text = "Pasife alma, hesabın açık oturumlarını sonlandırır. Kendi parolanız için Dosya menüsünü kullanın.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        actions.Controls.Add(note, 0, 2); actions.SetColumnSpan(note, 2); selected.Controls.Add(actions); cards.Controls.Add(selected, 1, 0); outer.Controls.Add(cards, 0, 1);
        var list = new GroupBox { Text = "Hesap listesi — işlem yapmak için bir satır seçin", Dock = DockStyle.Fill, Padding = new Padding(8, 20, 8, 8) };
        adminView.Dock = DockStyle.Fill; adminView.RowHeadersWidth = 28;
        adminView.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "username" => ("Kullanıcı adı", 220), "RoleName" => ("Yetki", 170), "IsActive" => ("Etkin", 80),
                "MustChangePassword" => ("Parola yenileme gerekli", 210), "PasswordResetExpiresUtc" => ("Geçici parola sonu (UTC)", 230),
                _ => (e.Column.HeaderText, e.Column.Width)
            };
            e.Column.HeaderText = caption; e.Column.MinimumWidth = width; e.Column.Width = width;
        };
        adminView.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            string property = adminView.Columns[e.ColumnIndex].DataPropertyName;
            if (property == "PasswordResetExpiresUtc" && e.Value is DateTime expiry)
            { e.Value = expiry.ToString("dd.MM.yyyy HH:mm"); e.FormattingApplied = true; }
            if (property == "RoleName" && e.Value is string value)
            {
                e.Value = value switch { "Admin" => "Yönetici", "PersonnelManager" => "Personel sorumlusu", "Viewer" => "Görüntüleyici", _ => value };
                e.FormattingApplied = true;
            }
        };
        list.Controls.Add(adminView); outer.Controls.Add(list, 0, 2); Controls.Add(outer);
        ResumeLayout(true); AppTheme.Apply(this);
    }
}
