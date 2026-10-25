namespace WinFormsApp2;

internal sealed partial class PasswordResetDialog
{
    private void ArrangePasswordReset(string username, Button submit)
    {
        Text = "Muhasebe — Parola Sıfırlama"; ClientSize = new Size(720, 330);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 102)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        outer.Controls.Add(ClassicLayouts.Banner("Parola Sıfırlama", "Hedef hesap: " + username), 0, 0);
        outer.Controls.Add(new Label { Text = "Hesabın açık oturumları sona erecek. Uygulama 30 dakika geçerli bir geçici parola üretecek; kullanıcı ilk girişte kendi parolasını belirleyecek.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(3) }, 0, 1);
        var group = new GroupBox { Text = "Yönetici doğrulaması", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        fields.Controls.Add(new Label { Text = "Devam etmek için kendi yönetici parolanızı girin:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        current.Dock = DockStyle.Fill; current.Margin = new Padding(3, 4, 3, 3); current.TabIndex = 0; fields.Controls.Add(current, 0, 1); group.Controls.Add(fields); outer.Controls.Add(group, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var cancel = new Button { Text = "Vazgeç", DialogResult = DialogResult.Cancel, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 6), TabIndex = 1 };
        submit.Dock = DockStyle.Fill; submit.Margin = new Padding(3, 6, 3, 6); submit.TabIndex = 2;
        actions.Controls.Add(cancel, 1, 0); actions.Controls.Add(submit, 2, 0); outer.Controls.Add(actions, 0, 3); Controls.Add(outer); CancelButton = cancel;
        Load += (_, _) => { FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; SetHint("Enter: Geçici parola oluştur · Esc: Vazgeç"); };
        AppTheme.Apply(this);
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && !IsOperationRunning)
        { DialogResult = DialogResult.Cancel; Close(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

internal sealed partial class TemporaryPasswordDialog
{
    private void ArrangeTemporaryPassword(PasswordResetResult result)
    {
        Text = "Muhasebe — Geçici Parola"; ClientSize = new Size(720, 330); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 102)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        outer.Controls.Add(ClassicLayouts.Banner("Geçici Parola", "Hesap: " + result.Username), 0, 0);
        outer.Controls.Add(new Label { Text = $"Son kullanım: {result.ExpiresUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}\nGeçici parolayı hesap sahibine güvenli biçimde iletin. Bu ekran kapatıldığında tekrar gösterilmez; gerekirse yeniden sıfırlayın.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(3) }, 0, 1);
        var group = new GroupBox { Text = "Geçici parola — yalnızca bu ekranda", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        secret.Dock = DockStyle.Fill; secret.Margin = new Padding(3, 4, 3, 3); secret.TabIndex = 0; fields.Controls.Add(secret, 0, 0);
        var show = new CheckBox { Text = "Geçici parolayı göster", Dock = DockStyle.Fill, TabIndex = 1 };
        show.CheckedChanged += (_, _) => secret.UseSystemPasswordChar = !show.Checked; fields.Controls.Add(show, 0, 1); group.Controls.Add(fields); outer.Controls.Add(group, 0, 2);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var close = new Button { Text = "Kapat", Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 6), TabIndex = 0 }; close.Click += (_, _) => Close();
        actions.Controls.Add(close, 1, 0); outer.Controls.Add(actions, 0, 3); Controls.Add(outer); AcceptButton = close; CancelButton = close; AppTheme.Apply(this);
    }
}
