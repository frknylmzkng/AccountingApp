namespace WinFormsApp2;

internal sealed partial class PasswordChangeForm : SessionForm
{
    private readonly TextBox current = Secret("currentPassword");
    private readonly TextBox next = Secret("newPassword");
    private readonly TextBox repeat = Secret("confirmPassword");
    private readonly Label notice = new() { AutoSize = true, MaximumSize = new Size(610, 0) };
    protected override bool CompactNavigation => true;
    internal static TextBox Secret(string name) => new() { Name = name, Width = 490, MaxLength = PasswordHasher.MaxPasswordLength, UseSystemPasswordChar = true };
    internal PasswordChangeForm()
    {
        var save = new Button { Text = "Parolayı değiştir" };
        ArrangePasswordChange(save); AcceptButton = save;
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
