namespace WinFormsApp2;

internal sealed partial class PasswordResetDialog
{
    private void ArrangePasswordReset(string username, Button submit)
    {
        Text = "Muhasebe — Parola Sıfırlama"; ClientSize = new Size(800, 440);
        var info = new SecretDialogInfoCard("passwordResetInfo", "Hesap ve işlem bilgisi", "Hedef hesap: " + username,
            "Hesabın açık oturumları sona erecek. Uygulama 30 dakika geçerli bir geçici parola üretecek; kullanıcı ilk girişte kendi parolasını belirleyecek.");
        var verification = new SecretDialogEntryCard("passwordResetVerification", "Yönetici doğrulaması",
            "Devam etmek için kendi yönetici parolanızı girin:", current);
        var cancel = new Button { Name = "passwordResetCancel", Text = "Vazgeç", DialogResult = DialogResult.Cancel };
        submit.Name = "passwordResetSubmit"; CancelButton = cancel;
        var actions = new SecretDialogActions([(cancel, 110), (submit, 210)]);
        var workspace = new SecretDialogWorkspace(info, verification);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Parola Sıfırlama", "Yönetici doğrulamasıyla geçici parola oluşturun."), 0, 0);
        outer.Controls.Add(new SecretDialogBody(workspace, actions) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        Controls.Add(outer);
        Load += (_, _) => SetHint("Enter: Geçici parola oluştur · Esc: Vazgeç");
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
        Text = "Muhasebe — Geçici Parola"; ClientSize = new Size(800, 440); StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = false;
        var info = new SecretDialogInfoCard("temporaryPasswordInfo", "Hesap ve süre bilgisi", "Hesap: " + result.Username,
            $"Son kullanım: {result.ExpiresUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}\nGeçici parolayı hesap sahibine güvenli biçimde iletin. Bu ekran kapatıldığında tekrar gösterilmez; gerekirse yeniden sıfırlayın.");
        var show = new CheckBox { Name = "temporaryPasswordShow", Text = "Geçici parolayı göster" };
        show.CheckedChanged += (_, _) => secret.UseSystemPasswordChar = !show.Checked;
        var entry = new SecretDialogEntryCard("temporaryPasswordEntry", "Geçici parola — yalnızca bu ekranda", "Geçici parola:", secret, show);
        var close = new Button { Name = "temporaryPasswordClose", Text = "Kapat" }; close.Click += (_, _) => Close();
        var workspace = new SecretDialogWorkspace(info, entry);
        var actions = new SecretDialogActions([(close, 130)]);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 2 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Geçici Parola", "Oluşturulan geçici parolanın hesap ve süre bilgileri."), 0, 0);
        outer.Controls.Add(new SecretDialogBody(workspace, actions) { Dock = DockStyle.Fill, Margin = Padding.Empty }, 0, 1);
        Controls.Add(outer); AcceptButton = close; CancelButton = close; AppTheme.Apply(this);
    }
}
