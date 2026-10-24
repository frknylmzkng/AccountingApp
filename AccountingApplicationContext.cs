namespace WinFormsApp2
{
    internal sealed class AccountingApplicationContext : ApplicationContext
    {
        private readonly Func<AppPage, Form> createForm;
        private bool switching;
        private bool exiting;

        internal string? Username { get; private set; }
        internal bool IsAuthenticated => Username != null;
        internal AppPage? CurrentPage { get; private set; }

        public AccountingApplicationContext() : this(CreateForm) { }

        internal AccountingApplicationContext(Func<AppPage, Form> createForm)
        {
            this.createForm = createForm;
            SwitchTo(AppPage.Login);
        }

        internal bool StartSession(string username)
        {
            if (exiting || switching || CurrentPage != AppPage.Login || IsAuthenticated ||
                string.IsNullOrWhiteSpace(username))
                return false;

            Username = username;
            try { SwitchTo(AppPage.Dashboard); }
            catch { Username = null; throw; }
            return true;
        }

        internal bool Navigate(AppPage page)
        {
            if (exiting || switching || !IsAuthenticated || page == AppPage.Login ||
                !Enum.IsDefined(typeof(AppPage), page))
                return false;
            if (CurrentPage != page) SwitchTo(page);
            return true;
        }

        internal void Logout()
        {
            if (exiting || switching || !IsAuthenticated) return;
            Username = null;
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
            if (ReferenceEquals(sender, MainForm)) StartSession(e.Username);
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

        protected override void OnMainFormClosed(object? sender, EventArgs e)
        {
            exiting = true;
            Username = null;
            CurrentPage = null;
            if (sender is Form form) Unwire(form);
            base.OnMainFormClosed(sender, e);
        }

        protected override void ExitThreadCore()
        {
            if (exiting && MainForm == null) return;
            exiting = true;
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
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
    }
}
