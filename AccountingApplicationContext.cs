namespace WinFormsApp2
{
    internal sealed class AccountingApplicationContext : ApplicationContext
    {
        private readonly Func<AppPage, Form> createForm;
        private bool switching;
        private bool exiting;
        private bool backgroundBackups;
        private System.Windows.Forms.Timer? backupTimer;
        private CancellationTokenSource? sessionLifetime;
        private bool backupRunning;

        internal string? Username { get; private set; }
        internal UserSession? Session { get; private set; }
        internal bool IsAuthenticated => Username != null;
        internal AppPage? CurrentPage { get; private set; }

        public AccountingApplicationContext() : this(CreateForm) { backgroundBackups = true; }

        internal AccountingApplicationContext(Func<AppPage, Form> createForm)
        {
            this.createForm = createForm;
            SwitchTo(AppPage.Login);
        }

        internal bool StartSession(string username, UserRole role = UserRole.Admin)
        {
            if (exiting || switching || CurrentPage != AppPage.Login || IsAuthenticated ||
                string.IsNullOrWhiteSpace(username) || !Enum.IsDefined(role))
                return false;

            Username = username;
            Session = new UserSession(username, role);
            try { SwitchTo(AppPage.Dashboard); }
            catch { Username = null; Session = null; throw; }
            if (backgroundBackups && role == UserRole.Admin)
            {
                sessionLifetime = new();
                backupTimer = new System.Windows.Forms.Timer { Interval = 3600000 };
                backupTimer.Tick += async (_, _) => await DailyBackupAsync(); backupTimer.Start();
                _ = DailyBackupAsync();
            }
            return true;
        }

        internal bool Navigate(AppPage page)
        {
            if (exiting || switching || !IsAuthenticated || page == AppPage.Login ||
                !Enum.IsDefined(typeof(AppPage), page))
                return false;
            if (page is AppPage.Admin or AppPage.Settings && Session?.Role != UserRole.Admin) return false;
            if (CurrentPage != page) SwitchTo(page);
            return true;
        }

        internal void Logout()
        {
            if (exiting || switching || !IsAuthenticated) return;
            StopBackgroundWork();
            Username = null;
            Session = null;
            AppLog.Event("Session.Logout");
            AppDatabase.Reset();
            try { SwitchTo(AppPage.Login); }
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
            }
        }

        private void Unwire(Form form)
        {
            if (form is Giris login) login.LoginSucceeded -= OnLoginSucceeded;
            if (form is SessionForm screen)
            {
                screen.PageRequested -= OnPageRequested;
                screen.LogoutRequested -= OnLogoutRequested;
            }
        }

        private void OnLoginSucceeded(object? sender, LoginSucceededEventArgs e)
        {
            if (ReferenceEquals(sender, MainForm)) StartSession(e.Username, e.Role);
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
            try { Logout(); }
            catch (Exception)
            {
                MessageBox.Show("Giriş ekranı açılamadı. Uygulamayı yeniden açın.", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task DailyBackupAsync()
        {
            if (backupRunning || Session?.Role != UserRole.Admin || sessionLifetime == null) return;
            backupRunning = true;
            var user = Session; var token = sessionLifetime.Token;
            try { await new BackupService(AppDatabase.ConnectionString, user).EnsureDailyAsync(token); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
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
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
    }
}
