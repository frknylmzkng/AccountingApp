namespace WinFormsApp2;

internal sealed partial class PasswordResetDialog : SessionForm
{
    private readonly TextBox current = PasswordChangeForm.Secret("adminCurrentPassword");
    internal PasswordResetResult? Result { get; private set; }
    internal bool SessionInvalidated { get; private set; }
    protected override bool ShowNavigationTools => false;
    internal PasswordResetDialog(UserSession user, AccountTarget target)
    {
        Session = user;
        var submit = new Button { Text = "Geçici parola oluştur" };
        ArrangePasswordReset(target.Username, submit); AcceptButton = submit;
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

internal sealed partial class TemporaryPasswordDialog : Form
{
    private readonly TextBox secret;
    internal TemporaryPasswordDialog(PasswordResetResult result)
    {
        secret = PasswordChangeForm.Secret("temporaryPassword"); secret.ReadOnly = true; secret.Text = result.TemporaryPassword;
        ArrangeTemporaryPassword(result);
    }
    protected override void OnFormClosed(FormClosedEventArgs e) { secret.Clear(); base.OnFormClosed(e); }
    protected override void Dispose(bool disposing) { if (disposing && !secret.IsDisposed) secret.Clear(); base.Dispose(disposing); }
}
