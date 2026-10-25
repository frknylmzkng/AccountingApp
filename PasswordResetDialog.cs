namespace WinFormsApp2;

internal sealed class PasswordResetDialog : SessionForm
{
    private readonly TextBox current = PasswordChangeForm.Secret("adminCurrentPassword");
    internal PasswordResetResult? Result { get; private set; }
    internal bool SessionInvalidated { get; private set; }
    protected override bool ShowNavigationTools => false;
    internal PasswordResetDialog(UserSession user, AccountTarget target)
    {
        Session = user; Text = "Yönetici parola sıfırlama"; ClientSize = new Size(650, 285);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24) };
        panel.Controls.Add(new Label { Text = "Hedef hesap: " + target.Username, AutoSize = true });
        panel.Controls.Add(new Label { Text = "Hesabın açık oturumları sona erecek. Uygulama 30 dakika geçerli bir geçici parola üretecek; kullanıcı ilk girişte kendi parolasını belirleyecek.", AutoSize = true, MaximumSize = new Size(590, 0) });
        panel.Controls.Add(new Label { Text = "Devam etmek için kendi yönetici parolanız", AutoSize = true, Margin = new Padding(3, 18, 3, 3) }); panel.Controls.Add(current);
        var submit = new Button { Text = "Geçici parola oluştur", AutoSize = true, Margin = new Padding(3, 18, 3, 3) };
        panel.Controls.Add(submit); Controls.Add(panel); AcceptButton = submit;
        SessionRevoked += (_, _) => SessionInvalidated = true;
        submit.Click += async (_, _) =>
        {
            try
            {
                bool done = await RunOperationAsync(async token => Result = await new AccountService(AppDatabase.ConnectionString).ResetPasswordAsync(User, target, current.Text, token));
                if (!IsDisposed && (done || SessionInvalidated)) { DialogResult = done ? DialogResult.OK : DialogResult.Cancel; Close(); }
            }
            finally { if (!current.IsDisposed) current.Clear(); }
        };
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { if (!current.IsDisposed) current.Clear(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing && !current.IsDisposed) current.Clear(); base.Dispose(disposing); }
}

internal sealed class TemporaryPasswordDialog : Form
{
    private readonly TextBox secret;
    internal TemporaryPasswordDialog(PasswordResetResult result)
    {
        Text = "Geçici parola — yalnızca bu ekranda"; ClientSize = new Size(660, 260); StartPosition = FormStartPosition.CenterParent;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24) };
        panel.Controls.Add(new Label { Text = "Hesap: " + result.Username, AutoSize = true });
        panel.Controls.Add(new Label { Text = $"Son kullanım: {result.ExpiresUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}\nGeçici parolayı hesap sahibine güvenli biçimde iletin. Bu ekran kapatıldığında tekrar gösterilmez; gerekirse yeniden sıfırlayın.", AutoSize = true, MaximumSize = new Size(600, 0) });
        secret = PasswordChangeForm.Secret("temporaryPassword"); secret.ReadOnly = true; secret.Text = result.TemporaryPassword; panel.Controls.Add(secret);
        var show = new CheckBox { Text = "Geçici parolayı göster", AutoSize = true }; show.CheckedChanged += (_, _) => secret.UseSystemPasswordChar = !show.Checked;
        var close = new Button { Text = "Kapat", AutoSize = true }; close.Click += (_, _) => Close();
        panel.Controls.Add(show); panel.Controls.Add(close); Controls.Add(panel); AppTheme.Apply(this);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { secret.Clear(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing && !secret.IsDisposed) secret.Clear(); base.Dispose(disposing); }
}
