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
            EditControls.AddRange([ekle_btn, edit_btn, sil_btn, izinKaydet_btn]);
            RefreshAction = () => LoadDataAsync(); ClearAction = Temizle; DeleteAction = () => sil_btn_Click(this, EventArgs.Empty);
        }

        private string connectionString => AppDatabase.ConnectionString;

        private bool bindingRecords;

        private void back_Click(object sender, EventArgs e)
        {
            RequestBack();
        }

        public async Task VerilerAsync(CancellationToken token = default)
        {
            var table = await People.ListAsync(PersonnelGroup.General, token);
            if (IsDisposed || token.IsCancellationRequested) return;
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

        private async void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.General, person, true, token), "Çalışan eklendi. İzin kaydı oluşturulmadı.");
        }

        private async void edit_btn_Click(object sender, EventArgs e)
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
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.General, person, false, token, RecordSelection.OriginalVersion(calisanView)), "Çalışan bilgileri güncellendi. İzin tarihleri korundu.");
        }

        private async void izinKaydet_btn_Click(object sender, EventArgs e)
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

            await ApplyChangeAsync(token => People.SaveLeavesAsync([(tc, leave)], token),
                "Seçilen personelin izin tarihleri kaydedildi. Personel bilgileri değiştirilmedi.");
        }

        private void temizle_btn_Click(object sender, EventArgs e) => Temizle();

        private async void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(calisanView, out long tc))
            {
                MessageBox.Show("Lütfen silmek için listeden bir kayıt seçin!");
                return;
            }
            if (MessageBox.Show($"{tc} numaralı çalışanı ve ilişkili izinleri silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            await ApplyChangeAsync(token => People.DeleteAsync(PersonnelGroup.General, tc, token),
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

        private async Task ApplyChangeAsync(Func<CancellationToken, Task> change, string successMessage)
        {
            if (!await RunOperationAsync(change, successMessage) || IsDisposed) return;
            Temizle();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync() => await RunOperationAsync(VerilerAsync);

        private async void Genel_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadDataAsync(); }

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
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = Session == null || User.CanEdit;
            izinDurumLabel.Text = $"İzin hedefi: {tc}\nTarihler yalnızca İZNİ KAYDET ile kaydedilir.";
        }

        private void ResetLeaveInputs()
        {
            dtpBaslangic.Value = dtpBitis.Value = DateTime.Today;
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = false;
            izinDurumLabel.Text = "İzin kaydetmek için listeden personel seçin.";
        }

        protected override void RefreshEditState()
        {
            bool selected = Session?.CanEdit == true && RecordSelection.TryGetTc(calisanView, out _);
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = selected;
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

