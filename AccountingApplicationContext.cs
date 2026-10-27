namespace WinFormsApp2
{
    internal sealed class AccountingApplicationContext : ApplicationContext
    {
        private readonly Func<AppPage, Form> createForm;
        private bool switching;
        private bool exiting;
        private bool backgroundServices;
        private System.Windows.Forms.Timer? backupTimer;
        private CancellationTokenSource? sessionLifetime;
        private bool backupRunning;
        private System.Windows.Forms.Timer? sessionTimer;
        private bool sessionChecking;
        private bool sessionRevoked;
        internal Func<UserSession, CancellationToken, Task<bool>>? SessionValidator { get; set; }

        internal string? Username { get; private set; }
        internal UserSession? Session { get; private set; }
        internal bool IsAuthenticated => Username != null;
        internal AppPage? CurrentPage { get; private set; }

        public AccountingApplicationContext() : this(CreateForm) { backgroundServices = true; }

        internal AccountingApplicationContext(Func<AppPage, Form> createForm)
        {
            this.createForm = createForm;
            SwitchTo(AppPage.Login);
        }

        internal bool StartSession(string username, UserRole role = UserRole.Admin) => StartSession(new UserSession(username, role));

        internal bool StartSession(UserSession user)
        {
            if (exiting || switching || CurrentPage != AppPage.Login || IsAuthenticated ||
                string.IsNullOrWhiteSpace(user.Username) || !Enum.IsDefined(user.Role))
                return false;

            Username = user.Username;
            Session = user;
            sessionRevoked = false;
            try { SwitchTo(user.MustChangePassword ? AppPage.PasswordChange : AppPage.Dashboard); }
            catch { Username = null; Session = null; throw; }
            if (backgroundServices)
            {
                sessionLifetime = new();
                SessionValidator ??= (current, token) => AccountAuthorization.IsCurrentAsync(AppDatabase.ConnectionString, current, token);
                sessionTimer = new System.Windows.Forms.Timer { Interval = 30000 };
                sessionTimer.Tick += async (_, _) => await CheckSessionAsync(); sessionTimer.Start();
                if (user.Role == UserRole.Admin && !user.MustChangePassword)
                {
                    backupTimer = new System.Windows.Forms.Timer { Interval = 3600000 };
                    backupTimer.Tick += async (_, _) => await DailyBackupAsync(); backupTimer.Start();
                    _ = DailyBackupAsync();
                }
            }
            return true;
        }

        internal bool Navigate(AppPage page)
        {
            if (sessionRevoked) { Logout(SessionRevokedException.Notice); return false; }
            if (exiting || switching || !IsAuthenticated || MainForm is SessionForm { IsOperationRunning: true } || page == AppPage.Login ||
                !Enum.IsDefined(typeof(AppPage), page))
                return false;
            if (page is AppPage.Admin or AppPage.Settings && Session?.Role != UserRole.Admin) return false;
            if (Session?.MustChangePassword == true && page != AppPage.PasswordChange) return false;
            if (CurrentPage != page) SwitchTo(page);
            return true;
        }

        internal void Logout(string? notice = null)
        {
            if (exiting || switching || !IsAuthenticated || MainForm is SessionForm { IsOperationRunning: true }) return;
            StopBackgroundWork();
            Username = null;
            Session = null;
            AppLog.Event("Session.Logout");
            AppDatabase.Reset();
            try { SwitchTo(AppPage.Login); if (notice != null && MainForm is Giris login) login.SetSessionNotice(notice); }
            catch
            {
                // A failed return to login must never leave a protected screen available.
                ExitThread();
                throw;
            }
        }

        private void SwitchTo(AppPage page)
        {
            switching = true;
            Form? next = null;
            Form? previous = MainForm;
            AppPage? previousPage = CurrentPage;
            try
            {
                next = createForm(page);
                if (next.IsDisposed || ReferenceEquals(next, previous))
                    throw new InvalidOperationException("Yeni ekran oluşturulamadı.");
                if (next is Dashboard dashboard) dashboard.SetUsername(Username ?? "");
                if (next is SessionForm sessionForm) sessionForm.Session = Session;

                if (previous != null) Unwire(previous);
                // Change MainForm before closing the old one so navigation does not end the message loop.
                MainForm = next;
                CurrentPage = page;
                Wire(next);
                if (previous != null)
                {
                    previous.Close();
                    previous.Dispose();
                }
                next.Show();
            }
            catch
            {
                if (next != null && !ReferenceEquals(next, previous))
                {
                    Unwire(next);
                    next.Dispose();
                }
                if (previous == null || previous.IsDisposed)
                {
                    MainForm = null;
                    CurrentPage = null;
                    Username = null;
                    ExitThread();
                }
                else if (ReferenceEquals(MainForm, next))
                {
                    MainForm = previous;
                    CurrentPage = previousPage;
                    Wire(previous);
                }
                throw;
            }
            finally { switching = false; }
        }

        private void Wire(Form form)
        {
            if (form is Giris login) login.LoginSucceeded += OnLoginSucceeded;
            if (form is SessionForm screen)
            {
                screen.PageRequested += OnPageRequested;
                screen.LogoutRequested += OnLogoutRequested;
                screen.SessionRevoked += OnSessionRevoked;
            }
        }

        private void Unwire(Form form)
        {
            if (form is Giris login) login.LoginSucceeded -= OnLoginSucceeded;
            if (form is SessionForm screen)
            {
                screen.PageRequested -= OnPageRequested;
                screen.LogoutRequested -= OnLogoutRequested;
                screen.SessionRevoked -= OnSessionRevoked;
            }
        }

        private void OnLoginSucceeded(object? sender, LoginSucceededEventArgs e)
        {
            if (ReferenceEquals(sender, MainForm)) StartSession(e.Session);
        }

        private void OnPageRequested(object? sender, PageRequestedEventArgs e)
        {
            if (!ReferenceEquals(sender, MainForm)) return;
            try { Navigate(e.Page); }
            catch (Exception)
            {
                MessageBox.Show("Ekran açılamadı. Lütfen yeniden deneyin.", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnLogoutRequested(object? sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, MainForm)) return;
            try { Logout((sender as SessionForm)?.LogoutNotice); }
            catch (Exception)
            {
                MessageBox.Show("Giriş ekranı açılamadı. Uygulamayı yeniden açın.", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnSessionRevoked(object? sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, MainForm)) return;
            sessionRevoked = true;
            Logout(SessionRevokedException.Notice);
        }

        internal async Task CheckSessionAsync()
        {
            if (sessionChecking || Session == null || SessionValidator == null) return;
            var checkedSession = Session;
            var token = sessionLifetime?.Token ?? CancellationToken.None;
            sessionChecking = true;
            try
            {
                if (!sessionRevoked && !await SessionValidator(checkedSession, token))
                {
                    if (!ReferenceEquals(Session, checkedSession)) return;
                    sessionRevoked = true;
                }
                if (ReferenceEquals(Session, checkedSession) && sessionRevoked) Logout(SessionRevokedException.Notice);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception ex) { AppLog.Failure(ex, "Session.Check"); }
            finally { sessionChecking = false; }
        }

        private async Task DailyBackupAsync()
        {
            if (backupRunning || Session?.Role != UserRole.Admin || sessionLifetime == null) return;
            backupRunning = true;
            var user = Session; var token = sessionLifetime.Token;
            try
            {
                await new BackupService(AppDatabase.ConnectionString, user).EnsureDailyAsync(token);
                if (!token.IsCancellationRequested && ReferenceEquals(Session, user) && MainForm is SessionForm current)
                    current.SetHint("Yerel yedek ve seçili aynalama kontrolü tamamlandı.");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (SessionRevokedException)
            {
                if (ReferenceEquals(Session, user)) { sessionRevoked = true; Logout(SessionRevokedException.Notice); }
            }
            catch (BackupMirrorException)
            {
                if (!token.IsCancellationRequested && ReferenceEquals(Session, user) && MainForm is SessionForm form)
                    form.SetHint("Ana yedek mevcut; harici kopya bekliyor. Erişim sağlandığında yeniden denenecek; Ayarlar'dan da deneyebilirsiniz.");
            }
            catch (Exception ex)
            {
                AppLog.Failure(ex, "Backup.Daily");
                if (!token.IsCancellationRequested && MainForm is SessionForm form) form.SetHint("Otomatik yedek alınamadı. Ayarlar / yedek ekranından kontrol edin.");
            }
            finally { backupRunning = false; }
        }
        private void StopBackgroundWork()
        {
            backupTimer?.Dispose(); backupTimer = null;
            sessionTimer?.Dispose(); sessionTimer = null;
            sessionLifetime?.Cancel(); sessionLifetime?.Dispose(); sessionLifetime = null;
        }

        protected override void OnMainFormClosed(object? sender, EventArgs e)
        {
            exiting = true;
            StopBackgroundWork(); Session = null;
            Username = null;
            CurrentPage = null;
            if (sender is Form form) Unwire(form);
            base.OnMainFormClosed(sender, e);
        }

        protected override void ExitThreadCore()
        {
            if (exiting && MainForm == null) return;
            exiting = true;
            StopBackgroundWork(); Session = null;
            Username = null;
            CurrentPage = null;
            Form? current = MainForm;
            MainForm = null;
            if (current != null)
            {
                Unwire(current);
                current.Dispose();
            }
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                exiting = true;
                StopBackgroundWork(); Session = null;
                Username = null;
                CurrentPage = null;
                Form? current = MainForm;
                MainForm = null;
                if (current != null)
                {
                    Unwire(current);
                    current.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private static Form CreateForm(AppPage page) => page switch
        {
            AppPage.Login => new Giris(),
            AppPage.Dashboard => new Dashboard(),
            AppPage.General => new Genel(),
            AppPage.Joker => new Joker(),
            AppPage.Search => new Search(),
            AppPage.Summary => new Summary(),
            AppPage.Contact => new Contact(),
            AppPage.Admin => new Admin(),
            AppPage.Payments => new PaymentsForm(),
            AppPage.Settings => new SettingsForm(),
            AppPage.LeavePlan => new LeavePlanForm(),
            AppPage.PasswordChange => new PasswordChangeForm(),
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
    }
}
