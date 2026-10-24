using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    public partial class Genel : SessionForm
    {
        public Genel()
        {
            InitializeComponent();
            RecordSelection.Configure(calisanView);
            ResetLeaveInputs();
        }

        private string connectionString => AppDatabase.ConnectionString;

        private bool bindingRecords;

        private void back_Click(object sender, EventArgs e)
        {
            RequestBack();
        }

        public void Veriler()
        {
            using var connection = new SqlConnection(connectionString);
            using var adapter = new SqlDataAdapter(@"
                SELECT C.TC, C.AD, C.SOYAD, C.IBAN, C.PARA,
                       I.IZINBASLANGIC, I.IZINBITIS, I.IzinSuresi
                FROM Calisan C LEFT JOIN izinler I ON C.TC = I.TC", connection);
            var table = new DataTable();
            adapter.Fill(table);
            table.Columns["TC"]!.ReadOnly = true;
            bindingRecords = true;
            try
            {
                calisanView.DataSource = table;
                calisanView.ClearSelection();
            }
            finally { bindingRecords = false; }
            ResetLeaveInputs();
        }

        private void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            ApplyChange(() => new PersonnelStore(connectionString).Add(PersonnelGroup.General,
                person), "Çalışan eklendi. İzin kaydı oluşturulmadı.");
        }

        private void edit_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(calisanView, out long selectedTc))
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
            ApplyChange(() => new PersonnelStore(connectionString).Update(PersonnelGroup.General,
                person), "Çalışan bilgileri güncellendi. İzin tarihleri korundu.");
        }

        private void izinKaydet_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(calisanView, out long tc))
            {
                MessageBox.Show("İzin kaydetmek için listeden bir personel seçin.");
                return;
            }
            if (TC_txt.Text.Trim() != tc.ToString(CultureInfo.InvariantCulture))
            {
                MessageBox.Show("TC değiştirilemez. İzin kaydedilecek personeli listeden yeniden seçin.");
                return;
            }
            if (!LeaveInput.TryCreate(dtpBaslangic.Value, dtpBitis.Value,
                out LeavePeriod? leave, out string error))
            {
                MessageBox.Show(error);
                return;
            }

            ApplyChange(() => new PersonnelStore(connectionString).SaveLeave(tc, leave),
                "Seçilen personelin izin tarihleri kaydedildi. Personel bilgileri değiştirilmedi.");
        }

        private void temizle_btn_Click(object sender, EventArgs e) => Temizle();

        private void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(calisanView, out long tc))
            {
                MessageBox.Show("Lütfen silmek için listeden bir kayıt seçin!");
                return;
            }
            if (MessageBox.Show($"{tc} numaralı çalışanı ve ilişkili izinleri silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            ApplyChange(() => new PersonnelStore(connectionString).Delete(PersonnelGroup.General, tc),
                "Çalışan ve izin bilgileri silindi!");
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

        private void Genel_Load(object sender, EventArgs e) => LoadData();

        private void calisanView_SelectionChanged(object? sender, EventArgs e)
        {
            if (bindingRecords) return;
            if (!RecordSelection.TryGetTc(calisanView, out long tc))
            {
                ResetLeaveInputs();
                return;
            }
            if (calisanView.SelectedRows[0].DataBoundItem is not DataRowView record) return;
            TC_txt.Text = tc.ToString(CultureInfo.InvariantCulture);
            ad_txt.Text = record["AD"].ToString();
            soyad_txt.Text = record["SOYAD"].ToString();
            IBAN_txt.Text = record["IBAN"].ToString();
            money_txt.Text = record["PARA"] == DBNull.Value ? "" :
                MoneyInput.Format(Convert.ToDecimal(record["PARA"], CultureInfo.InvariantCulture));
            dtpBaslangic.Value = record["IZINBASLANGIC"] is DateTime start ? start : DateTime.Today;
            dtpBitis.Value = record["IZINBITIS"] is DateTime end ? end : DateTime.Today;
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = true;
            izinDurumLabel.Text = $"İzin hedefi: {tc}\nTarihler yalnızca İZNİ KAYDET ile kaydedilir.";
        }

        private void ResetLeaveInputs()
        {
            dtpBaslangic.Value = dtpBitis.Value = DateTime.Today;
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = false;
            izinDurumLabel.Text = "İzin kaydetmek için listeden personel seçin.";
        }

        private void Temizle()
        {
            calisanView.ClearSelection();
            TC_txt.Clear();
            ad_txt.Clear();
            soyad_txt.Clear();
            IBAN_txt.Clear();
            money_txt.Clear();
            ResetLeaveInputs();
        }
    }
}

