using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    public partial class Joker : SessionForm
    {
        public Joker()
        {
            InitializeComponent();
            RecordSelection.Configure(jokerView);
        }

        private string connectionString => AppDatabase.ConnectionString;

        private bool bindingRecords;

        private void back_Click_1(object sender, EventArgs e)
        {
            RequestBack();
        }

        public void Veriler()
        {
            using var connection = new SqlConnection(connectionString);
            using var adapter = new SqlDataAdapter("SELECT TC, AD, SOYAD, IBAN, PARA FROM Joker", connection);
            var table = new DataTable();
            adapter.Fill(table);
            table.Columns["TC"]!.ReadOnly = true;
            bindingRecords = true;
            try
            {
                jokerView.DataSource = table;
                jokerView.ClearSelection();
            }
            finally { bindingRecords = false; }
        }

        private void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            ApplyChange(() => new PersonnelStore(connectionString).Add(PersonnelGroup.Joker,
                person),
                "Veriler eklendi!");
        }

        private void edit_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(jokerView, out long selectedTc))
            {
                MessageBox.Show("Lütfen düzenlemek için listeden bir kayıt seçin!");
                return;
            }
            if (TC_txt.Text.Trim() != selectedTc.ToString(CultureInfo.InvariantCulture))
            {
                MessageBox.Show("Düzenleme sırasında TC değiştirilemez. Kaydı listeden yeniden seçin.");
                return;
            }

            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            ApplyChange(() => new PersonnelStore(connectionString).Update(PersonnelGroup.Joker,
                person),
                "Veri başarıyla güncellendi!");
        }

        private void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(jokerView, out long tc))
            {
                MessageBox.Show("Lütfen silmek için listeden bir kayıt seçin!");
                return;
            }
            if (MessageBox.Show($"{tc} numaralı Joker kaydını silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ApplyChange(() => new PersonnelStore(connectionString).Delete(PersonnelGroup.Joker, tc),
                "Kayıt başarıyla silindi!");
        }

        private PersonnelDetails? ReadInputs()
        {
            if (!PersonnelInput.TryCreate(TC_txt.Text, ad_txt.Text, soyad_txt.Text,
                IBAN_txt.Text, money_txt.Text, out PersonnelDetails? person, out string error))
            {
                MessageBox.Show(error);
                return null;
            }
            IBAN_txt.Text = person.Iban;
            return person;
        }

        private void ApplyChange(Action change, string successMessage)
        {
            try { change(); }
            catch (Exception ex)
            {
                MessageBox.Show("İşlem tamamlanamadı: " + ex.Message);
                return;
            }

            // Only a committed operation clears the input or reports success.
            Temizle();
            MessageBox.Show(successMessage);
            LoadData(changeSaved: true);
        }

        private void LoadData(bool changeSaved = false)
        {
            try { Veriler(); }
            catch (Exception ex)
            {
                MessageBox.Show((changeSaved ? "İşlem kaydedildi ancak liste yenilenemedi: " :
                    "Liste yüklenemedi: ") + ex.Message);
            }
        }

        private void Joker_Load(object sender, EventArgs e) => LoadData();

        private void jokerView_SelectionChanged(object? sender, EventArgs e)
        {
            if (bindingRecords || !RecordSelection.TryGetTc(jokerView, out long tc)) return;
            if (jokerView.SelectedRows[0].DataBoundItem is not DataRowView record) return;
            TC_txt.Text = tc.ToString(CultureInfo.InvariantCulture);
            ad_txt.Text = record["AD"].ToString();
            soyad_txt.Text = record["SOYAD"].ToString();
            IBAN_txt.Text = record["IBAN"].ToString();
            money_txt.Text = record["PARA"] == DBNull.Value ? "" :
                MoneyInput.Format(Convert.ToDecimal(record["PARA"], CultureInfo.InvariantCulture));
        }

        private void Temizle()
        {
            jokerView.ClearSelection();
            TC_txt.Clear();
            ad_txt.Clear();
            soyad_txt.Clear();
            IBAN_txt.Clear();
            money_txt.Clear();
        }
    }
}

