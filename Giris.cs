namespace WinFormsApp2
{
    public partial class Giris : Form
    {
        private readonly Func<Task> prepareCredentials;
        private readonly Func<string, string, Task<bool>> authenticate;
        private bool busy;
        private bool closed;
        private bool credentialsPrepared;

        internal LoginFailure? LastFailure { get; private set; }

        internal event EventHandler<LoginSucceededEventArgs>? LoginSucceeded;

        public Giris() : this(
            () => Task.Run(() => new AdminStore(AppDatabase.ConnectionString).MigrateLegacyPasswords()),
            (username, password) => Task.Run(() => new AdminStore(AppDatabase.ConnectionString).Authenticate(username, password))) { }

        internal Giris(Func<Task> prepareCredentials, Func<string, string, Task<bool>> authenticate)
        {
            this.prepareCredentials = prepareCredentials;
            this.authenticate = authenticate;
            InitializeComponent();
            AcceptButton = giris_btn;
            Load += Giris_Load;
        }

        private bool CanContinue => !closed && !IsDisposed && !Disposing;

        private async void Giris_Load(object? sender, EventArgs e) => await PrepareCredentialsAsync();

        internal async Task<bool> PrepareCredentialsAsync()
        {
            if (!CanContinue || busy) return false;
            if (credentialsPrepared) return true;
            busy = true;
            LastFailure = null;
            giris_btn.Enabled = false;
            giris_btn.Text = "BEKLEYİN";
            statusLabel.Text = "Giriş hazırlanıyor; veritabanı bağlantısı kontrol ediliyor…";
            LoginDiagnostics.RecordPreparationState("started");
            try
            {
                await prepareCredentials();
                if (!CanContinue) return false;
                credentialsPrepared = true;
                LoginDiagnostics.RecordPreparationState("ready");
                statusLabel.Text = "Giriş hazır. Kullanıcı adınızı ve şifrenizi girin.";
                return true;
            }
            catch (Exception ex)
            {
                if (CanContinue)
                {
                    ReportFailure(ex, "prepare");
                    LoginDiagnostics.RecordPreparationState("failed:" + LastFailure!.Code);
                }
                return false;
            }
            finally
            {
                busy = false;
                if (CanContinue)
                {
                    giris_btn.Enabled = true;
                    giris_btn.Text = credentialsPrepared ? "GİRİŞ" : "YENİDEN DENE";
                }
            }
        }

        internal async Task<bool> TrySignInAsync(string username, string password)
        {
            if (!CanContinue || busy || string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                return false;
            if (!credentialsPrepared && !await PrepareCredentialsAsync()) return false;
            if (!CanContinue) return false;

            busy = true;
            LastFailure = null;
            giris_btn.Enabled = false;
            giris_btn.Text = "BEKLEYİN";
            statusLabel.Text = "Giriş doğrulanıyor…";
            try
            {
                bool authenticated = await authenticate(username.Trim(), password);
                if (!CanContinue) return false;
                password_txt.Clear();
                if (authenticated)
                    LoginSucceeded?.Invoke(this, new LoginSucceededEventArgs(username.Trim()));
                else
                    statusLabel.Text = "Kullanıcı adı veya şifre hatalı. Bilgilerinizi kontrol edin.";
                return authenticated;
            }
            catch (Exception ex)
            {
                if (CanContinue)
                {
                    password_txt.Clear();
                    ReportFailure(ex, "authenticate");
                }
                return false;
            }
            finally
            {
                busy = false;
                if (CanContinue)
                {
                    giris_btn.Enabled = true;
                    giris_btn.Text = "GİRİŞ";
                }
            }
        }

        private async void giris_btn_Click(object sender, EventArgs e)
        {
            if (busy) return;
            if (!credentialsPrepared && !await PrepareCredentialsAsync()) return;
            if (!CanContinue) return;
            string username = username_txt.Text.Trim();
            string password = password_txt.Text;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show(this, "Kullanıcı adı ve şifre boş bırakılamaz!", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool authenticated = await TrySignInAsync(username, password);
            if (CanContinue && !authenticated && LastFailure == null)
                MessageBox.Show(this, "Kullanıcı adı veya şifre hatalı!", "Hata",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closed = true;
            password_txt.Clear();
            username_txt.Clear();
            base.OnFormClosed(e);
        }

        private void ReportFailure(Exception exception, string operation)
        {
            LastFailure = LoginDiagnostics.Record(exception, operation);
            statusLabel.Text = LastFailure.Message + Environment.NewLine + "Hata kodu: " + LastFailure.Code;
        }
    }
}

