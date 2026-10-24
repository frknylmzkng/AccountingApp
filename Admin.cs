using System.Data;

namespace WinFormsApp2
{
    public partial class Admin : SessionForm
    {
        public Admin()
        {
            InitializeComponent();
            RecordSelection.Configure(adminView);
        }

        private string connectionString => AppDatabase.ConnectionString;

        private void Admin_Load(object sender, EventArgs e) => LoadData();

        private void back_Click(object sender, EventArgs e)
        {
            RequestBack();
        }

        public void Veriler()
        {
            DataTable table = new AdminStore(connectionString).GetUsers();
            table.Columns["username"]!.ReadOnly = true;
            adminView.DataSource = table;
            adminView.ClearSelection();
        }

        private void ekle_btn_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
            {
                MessageBox.Show("Kullanıcı adı ve şifre boş olamaz!");
                return;
            }
            if (txtPassword.Text.Length < 6)
            {
                MessageBox.Show("Şifre en az 6 karakter olmalıdır!");
                return;
            }

            ApplyChange(() => new AdminStore(connectionString).Add(
                txtUsername.Text.Trim(), txtPassword.Text), "Yönetici başarıyla eklendi!");
        }

        private void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetKey(adminView, "username", out string username))
            {
                MessageBox.Show("Lütfen silmek için listeden bir admin seçin!");
                return;
            }
            if (MessageBox.Show($"{username} kullanıcısını silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ApplyChange(() => new AdminStore(connectionString).Delete(username), "Admin başarıyla silindi!");
        }

        private void ApplyChange(Action change, string successMessage)
        {
            try { change(); }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem tamamlanamadı: " + ex.Message);
                return;
            }

            txtUsername.Clear();
            txtPassword.Clear();
            MessageBox.Show(successMessage);
            LoadData(changeSaved: true);
        }

        private void LoadData(bool changeSaved = false)
        {
            try { Veriler(); }
            catch (Exception ex)
            {
                MessageBox.Show((changeSaved ? "İşlem kaydedildi ancak liste yenilenemedi: " :
                    "Veri yükleme hatası: ") + ex.Message);
            }
        }
    }
}

