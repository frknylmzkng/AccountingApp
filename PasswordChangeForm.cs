namespace WinFormsApp2;

internal sealed class PasswordChangeForm : SessionForm
{
    private readonly TextBox current = Secret("currentPassword");
    private readonly TextBox next = Secret("newPassword");
    private readonly TextBox repeat = Secret("confirmPassword");
    private readonly Label notice = new() { AutoSize = true, MaximumSize = new Size(610, 0) };
    protected override bool CompactNavigation => true;
    internal static TextBox Secret(string name) => new() { Name = name, Width = 490, MaxLength = PasswordHasher.MaxPasswordLength, UseSystemPasswordChar = true };
    internal PasswordChangeForm()
    {
        Text = "Parolamı değiştir"; ClientSize = new Size(660, 380);
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(24), AutoScroll = true };
        panel.Controls.Add(notice); panel.Controls.Add(new Label { Text = PasswordPolicy.Description, AutoSize = true, MaximumSize = new Size(610, 0) });
        void Field(string text, TextBox field) { panel.Controls.Add(new Label { Text = text, AutoSize = true, Margin = new Padding(3, 12, 3, 3) }); panel.Controls.Add(field); }
        Field("Mevcut / geçici parola", current); Field("Yeni parola", next); Field("Yeni parola tekrar", repeat);
        var save = new Button { Text = "Parolayı değiştir", AutoSize = true, Margin = new Padding(3, 16, 3, 3) };
        panel.Controls.Add(save); Controls.Add(panel); AcceptButton = save;
        Load += (_, _) => notice.Text = Session?.MustChangePassword == true ? "Geçici parolayla giriş yaptınız. Diğer ekranlara geçmeden yeni parolanızı belirleyin." : "Parola değişince bütün açık oturumlarınız sona erecek. Yeni parolanızla yeniden giriş yapın.";
        save.Click += async (_, _) =>
        {
            try
            {
                bool done = await RunOperationAsync(token => new AccountService(AppDatabase.ConnectionString).ChangePasswordAsync(User, current.Text, next.Text, repeat.Text, token));
                if (done && !IsDisposed) RequestLogout("Parolanız değiştirildi. Yeni parolanızla yeniden giriş yapın.");
            }
            finally { ClearSecrets(); }
        };
    }
    private void ClearSecrets() { if (!current.IsDisposed) current.Clear(); if (!next.IsDisposed) next.Clear(); if (!repeat.IsDisposed) repeat.Clear(); }
    protected override void OnFormClosed(FormClosedEventArgs e) { ClearSecrets(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing) ClearSecrets(); base.Dispose(disposing); }
}
