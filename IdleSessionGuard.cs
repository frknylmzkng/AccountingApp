using System.Runtime.InteropServices;

namespace WinFormsApp2;

internal sealed class IdleSessionGuard : IDisposable
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromMinutes(10);
    internal static bool IsExpired(long now, long lastActivity) => now - lastActivity >= (long)Timeout.TotalMilliseconds;
    private readonly UserSession user;
    private readonly string connectionString;
    private readonly Action logout;
    private readonly Func<bool> revoked;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private readonly uint threadId = GetCurrentThreadId();
    private readonly Dictionary<nint, int> hidden = [];
    private long activity = Environment.TickCount64;
    private uint observedInput;
    private nint previousForeground;
    private SessionLockForm? screen;
    private bool disposed;
    private bool invalidated;
    internal bool IsLocked => screen != null;
    internal IdleSessionGuard(UserSession user, string connectionString, Action logout, Func<bool> revoked)
    {
        this.user = user; this.connectionString = connectionString; this.logout = logout; this.revoked = revoked;
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (GetLastInputInfo(ref input)) observedInput = input.Time;
        SessionUiState.InteractionCheck = CheckInteraction;
        timer.Tick += (_, _) => Tick(); timer.Start();
    }
    private bool CheckInteraction() { Tick(); return !IsLocked && !disposed; }
    private void Tick()
    {
        if (disposed) return;
        if (screen != null)
        {
            HidePrivateWindows();
            screen.SetCanEnd(SessionUiState.ActiveOperations == 0);
            if ((revoked() || invalidated) && SessionUiState.ActiveOperations == 0) logout();
            return;
        }
        var input = new LastInput { Size = (uint)Marshal.SizeOf<LastInput>() };
        if (GetLastInputInfo(ref input) && input.Time != observedInput)
        {
            observedInput = input.Time;
            GetWindowThreadProcessId(GetForegroundWindow(), out uint process);
            if (process == (uint)Environment.ProcessId)
            {
                long last = Environment.TickCount64 - unchecked((uint)Environment.TickCount64 - input.Time);
                // Input arriving after sleep/UI suspension must not silently
                // revive a session that had already exceeded its timeout.
                if (IsExpired(last, activity)) { Lock(); return; }
                activity = last;
            }
        }
        if (IsExpired(Environment.TickCount64, activity)) Lock();
    }
    internal void Lock()
    {
        if (disposed || screen != null) return;
        previousForeground = GetForegroundWindow(); SessionUiState.Lock();
        user.Remote?.Lock();
        screen = new SessionLockForm(user, connectionString, Resume, EndSession, () => revoked() || invalidated, () => { invalidated = true; EndSession(); });
        HidePrivateWindows();
        screen.Show(); screen.SetCanEnd(SessionUiState.ActiveOperations == 0);
        AppLog.Event("Session.Lock");
    }
    private void HidePrivateWindows()
    {
        nint allowed = screen?.IsHandleCreated == true ? screen.Handle : 0;
        EnumThreadWindows(threadId, (handle, _) =>
        {
            if (!IsWindowVisible(handle) || handle == allowed) return true;
            nint owner = GetWindow(handle, 4); int depth = 0;
            while (owner != 0 && depth++ < 32) { if (owner == allowed && allowed != 0) return true; owner = GetWindow(owner, 4); }
            if (!hidden.ContainsKey(handle)) hidden.Add(handle, IsIconic(handle) ? 7 : IsZoomed(handle) ? 3 : 4);
            ShowWindow(handle, 0); return true;
        }, 0);
    }
    private void Resume()
    {
        if (disposed || revoked() || invalidated) { EndSession(); return; }
        var locked = screen; screen = null; locked?.AllowClose(); locked?.Close(); locked?.Dispose();
        foreach (var pair in hidden) if (IsWindow(pair.Key)) ShowWindow(pair.Key, pair.Value);
        if (hidden.ContainsKey(previousForeground) && IsWindow(previousForeground)) SetForegroundWindow(previousForeground);
        hidden.Clear(); activity = Environment.TickCount64;
        SessionUiState.Release();
    }
    private void EndSession()
    {
        if (SessionUiState.ActiveOperations != 0) { screen?.SetCanEnd(false); return; }
        logout();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; timer.Dispose();
        SessionUiState.InteractionCheck = null;
        screen?.AllowClose(); screen?.Dispose(); screen = null;
        // A logout never restores private windows. Close outstanding dialogs first.
        foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
            if (form is SessionForm session) session.PermitApplicationClose();
        foreach (var pair in hidden)
            if (IsWindow(pair.Key)) PostMessage(pair.Key, 0x0010, 0, 0);
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => f.Owner != null).ToArray()) form.Dispose();
        hidden.Clear(); SessionUiState.Release();
    }
    [StructLayout(LayoutKind.Sequential)] private struct LastInput { public uint Size; public uint Time; }
    private delegate bool WindowCallback(nint handle, nint data);
    [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInput input);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint process);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, WindowCallback callback, nint data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] private static extern bool IsZoomed(nint handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint handle, int command);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool PostMessage(nint handle, uint message, nint wParam, nint lParam);
}

internal sealed class SessionLockForm : Form
{
    private readonly TextBox password = PasswordChangeForm.Secret("unlockPassword");
    private readonly Label status = new() { AutoSize = true, Dock = DockStyle.Fill };
    private readonly Button unlock = new() { Text = "Kilidi aç", AutoSize = true }, end = new() { Text = "Oturumu sonlandır", AutoSize = true };
    private readonly CancellationTokenSource lifetime = new();
    private bool mayClose, authenticating, canEnd, resourcesDisposed;
    internal SessionLockForm(UserSession user, string connectionString, Action resume, Action logout, Func<bool> revoked, Action invalidate)
    {
        Text = "Muhasebe — Oturum kilitli"; ClientSize = new(560, 330); MinimumSize = new(480, 340);
        StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = true; MaximizeBox = false;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(22), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "Oturum kilitli · " + user.Username + "\nDevam etmek için aynı hesabın parolasını girin.", AutoSize = true, Dock = DockStyle.Fill, Padding = new(0, 0, 0, 16) }, 0, 0);
        password.Dock = DockStyle.Fill; layout.Controls.Add(password, 0, 1); layout.Controls.Add(status, 0, 2);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true }; buttons.Controls.AddRange([unlock, end]); layout.Controls.Add(buttons, 0, 3);
        Controls.Add(layout); AcceptButton = unlock;
        status.Text = "Girilen bilgiler korunur. Devam eden kayıt işlemi iptal edilmez.";
        unlock.Click += async (_, _) =>
        {
            if (authenticating) return;
            authenticating = true; unlock.Enabled = end.Enabled = password.Enabled = false;
            string supplied = password.Text;
            try
            {
                bool valid = await new AccountService(connectionString).UnlockSessionAsync(user, supplied, lifetime.Token);
                if (IsDisposed) return;
                if (revoked()) { logout(); return; }
                if (valid) { password.Clear(); resume(); return; }
                status.Text = LoginAttemptPolicy.ReauthenticationFailure;
            }
            catch (SessionRevokedException) { if (!IsDisposed) { status.Text = SessionRevokedException.Notice; invalidate(); } }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex) { AppLog.Failure(ex, "Session.Unlock"); if (!IsDisposed) status.Text = "Kilidi açmak için doğrulama yapılamadı. Bağlantıyı kontrol edip yeniden deneyin."; }
            finally
            {
                supplied = ""; authenticating = false;
                if (!IsDisposed) { password.Clear(); password.Enabled = unlock.Enabled = true; end.Enabled = canEnd; password.Focus(); }
            }
        };
        end.Click += (_, _) => RequestEnd(logout);
        FormClosing += (_, e) => { if (!mayClose) { e.Cancel = true; if (!authenticating) RequestEnd(logout); } };
        Shown += (_, _) => password.Focus(); AppTheme.Apply(this);
    }
    private void RequestEnd(Action logout)
    {
        if (!canEnd || authenticating) { status.Text = "Kayıt işlemi sürüyor. Tamamlandıktan sonra oturumu sonlandırabilirsiniz."; return; }
        if (MessageBox.Show(this, "Kaydedilmemiş form bilgileri kaybolacak. Kaydedilmiş ve bekleyen ödeme günlüğü korunur. Oturum sonlandırılsın mı?",
            "Kilitli oturumu sonlandır", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes) logout();
    }
    internal void SetCanEnd(bool value) { canEnd = value; if (!authenticating) end.Enabled = value; }
    internal void AllowClose() => mayClose = true;
    protected override void Dispose(bool disposing) { if (disposing && !resourcesDisposed) { resourcesDisposed = true; lifetime.Cancel(); password.Clear(); lifetime.Dispose(); } base.Dispose(disposing); }
}
