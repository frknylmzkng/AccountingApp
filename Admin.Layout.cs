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
        Text = "Muhasebe — Yönetici"; ClientSize = new Size(1120, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Hesap yönetimi", "Hesap ekleyin; işlemler için listeden bir satır seçin."), 0, 0);
        role.Name = "newAccountRole";
        var create = new AdminCreateCard(txtUsername, txtPassword, role, ekle_btn);
        activate.Name = "adminActivate"; deactivate.Name = "adminDeactivate"; reset.Name = "adminResetPassword";
        sil_btn.Text = "Hesabı &sil";
        var selected = new AdminActionsCard([activate, deactivate, reset, sil_btn]);
        var list = new GroupBox { Name = "adminRecords", Text = "Hesap listesi — işlem yapmak için bir satır seçin", Padding = new Padding(8, 20, 8, 8), TabIndex = 2 };
        adminView.Dock = DockStyle.Fill; adminView.RowHeadersWidth = 28;
        adminView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        adminView.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "username" => ("Kullanıcı adı", 220), "RoleName" => ("Yetki", 170), "IsActive" => ("Etkin", 80),
                "MustChangePassword" => ("Parola yenileme gerekli", 210), "PasswordResetExpiresUtc" => ("Geçici parola sonu (UTC)", 230),
                _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption; e.Column.MinimumWidth = adminView.LogicalToDeviceUnits(width); e.Column.FillWeight = width;
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
        list.Controls.Add(adminView);
        var workspace = new AdminWorkspace(create, selected, list) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toCreate = new Button { Name = "adminShowCreate", Text = "Yeni &hesap", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toSelected = new Button { Name = "adminShowActions", Text = "Hesap işle&mleri", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 1 };
        var toList = new Button { Name = "adminShowList", Text = "Hesap &listesi", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 2 };
        toCreate.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toSelected.Click += (_, _) => workspace.ScrollControlIntoView(selected);
        toList.Click += (_, _) => workspace.ScrollControlIntoView(list);
        navigation.Controls.Add(toCreate); navigation.Controls.Add(toSelected); navigation.Controls.Add(toList);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2); Controls.Add(outer);
        ResumeLayout(true); AppTheme.Apply(this);
    }
}
