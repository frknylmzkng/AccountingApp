namespace WinFormsApp2
{
    public partial class Dashboard : SessionForm
    {
        public Dashboard()
        {
            InitializeComponent();
            ArrangeDashboard();
            AcceptButton = null;
            ActiveControl = null;
        }

        internal void SetUsername(string username) => userLabel.Text = "Kullanıcı: " + username;
        protected override void OnLoad(EventArgs e)
        {
            button4.Text = Session?.Role == UserRole.Admin ? "&Yönetici hesapları" : "Parolamı &değiştir";
            PopulateShortcuts();
            base.OnLoad(e);
        }

        private void button4_Click(object sender, EventArgs e) => RequestPage(Session?.Role == UserRole.Admin ? AppPage.Admin : AppPage.PasswordChange);
        private void button1_Click(object sender, EventArgs e) => RequestPage(AppPage.General);
        private void button2_Click(object sender, EventArgs e) => RequestPage(AppPage.Joker);
        private void button3_Click(object sender, EventArgs e) => RequestPage(AppPage.Search);
        private void button5_Click(object sender, EventArgs e) => RequestPage(AppPage.Summary);
        private void button6_Click(object sender, EventArgs e) => RequestPage(AppPage.Contact);
        private void logout_btn_Click(object sender, EventArgs e) => RequestLogout();
    }
}

