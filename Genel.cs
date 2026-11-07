using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    public partial class Genel : SessionForm
    {
        private readonly ListPager pager;
        public Genel()
        {
            InitializeComponent();
            ArrangePersonnel();
            RecordSelection.Configure(calisanView);
            pager = ListPager.Attach(calisanView, this, LoadDataAsync);
            TrackChanges("Personel kartı", TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt);
            TrackChanges("İzin tarihleri", dtpBaslangic, dtpBitis); GuardRecordSelection(calisanView);
            PersonnelDataReview.Attach(this, calisanView, IBAN_txt);
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
            var result = await People.PageAsync(PersonnelGroup.General, pager.Index, pager.SizeLimit, token);
            var table = result.Rows;
            if (IsDisposed || token.IsCancellationRequested) return;
            pager.Apply(result);
            table.Columns["TC"]!.ReadOnly = true;
            bindingRecords = true;
            try
            {
                calisanView.DataSource = table;
                calisanView.ClearSelection();
            }
            finally { bindingRecords = false; }
            Temizle();
            AcceptTrackedChanges();
            ResetLeaveInputs();
        }

        private async void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            using var startDialog = new EmploymentDateDialog(true);
            if (startDialog.ShowDialog(this) != DialogResult.OK) return;
            DateTime start = startDialog.SelectedDate!.Value;
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.General, person, true, token, employmentStart: start), "Çalışan eklendi. İzin kaydı oluşturulmadı.");
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

            if (!User.CanViewSalary || !User.CanViewIban)
            {
                if (calisanView.SelectedRows[0].DataBoundItem is not DataRowView selected) return;
                var personId = selected.Row.Field<Guid>("PersonId", DataRowVersion.Original);
                var request = new AccountingApp.Contracts.PersonnelCardDetails(AccountingApp.Contracts.PersonnelGroup.General, personId, RecordSelection.OriginalVersion(calisanView), ad_txt.Text, soyad_txt.Text, User.CanViewIban ? IBAN_txt.Text : null, "");
                await ApplyChangeAsync(ct => new PersonnelCardService(AppDatabase.ConnectionString, User).SaveDetailsAsync(request with { Reason = RemoteApi.Reason }, ct), "Personel bilgileri güncellendi; gizli alanlar korundu.");
                return;
            }

            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            byte[] version = RecordSelection.OriginalVersion(calisanView);
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.General, person, false, token, version), "Çalışan bilgileri güncellendi. İzin tarihleri korundu.");
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

            LeaveTarget target;
            try { target = LeaveTarget.FromSelection(calisanView).Single(); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
            await ApplyChangeAsync(token => People.SaveLeavesAsync([(target, leave)], token),
                "Seçilen personelin izin tarihleri kaydedildi. Personel bilgileri değiştirilmedi.", "İzin tarihleri");
        }

        private void temizle_btn_Click(object sender, EventArgs e) { if (ConfirmDiscardChanges("Alanları temizleme")) { Temizle(); AcceptTrackedChanges(); } }

        private async void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(calisanView, out long tc))
            {
                MessageBox.Show("Arşivlemek için listeden bir kayıt seçin.");
                return;
            }
            byte[] version;
            try { version = RecordSelection.OriginalVersion(calisanView); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
            if (MessageBox.Show($"{tc} numaralı çalışan arşivlenecek. Kart, izinler ve ödeme geçmişi korunacak; kalan bakiye silinmeyecek. Devam edilsin mi?",
                "Arşivleme onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using var endDialog = new EmploymentDateDialog(false);
            if (endDialog.ShowDialog(this) != DialogResult.OK) return;
            DateTime? end = endDialog.SelectedDate;
            await ApplyChangeAsync(token => People.ArchiveAsync(PersonnelGroup.General, tc, version, token, end),
                "Personel arşivlendi; izinler ve geçmiş korundu.", "");
        }

        private PersonnelDetails? ReadInputs()
        {
            if (!PersonnelInput.TryCreate(TC_txt.Text, ad_txt.Text, soyad_txt.Text,
                IBAN_txt.Text, User.CanViewSalary ? money_txt.Text : "0", out PersonnelDetails? person, out string error))
            {
                MessageBox.Show(error);
                return null;
            }
            IBAN_txt.Text = person.Iban;
            ad_txt.Text = person.FirstName;
            soyad_txt.Text = person.LastName;
            return person;
        }

        private async Task ApplyChangeAsync(Func<CancellationToken, Task> change, string successMessage, string savedGroup = "Personel kartı")
        {
            if (!ConfirmDiscardChanges("Diğer bölümü kaydedip listeyi yenileme", savedGroup)) return;
            if (!await RunChangeOperationAsync(change, successMessage) || IsDisposed) return;
            Temizle();
            AcceptTrackedChanges();
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
            AcceptTrackedChanges();
        }

        private void ResetLeaveInputs()
        {
            dtpBaslangic.Value = dtpBitis.Value = DateTime.Today;
            dtpBaslangic.Enabled = dtpBitis.Enabled = izinKaydet_btn.Enabled = false;
            izinDurumLabel.Text = "İzin kaydetmek için listeden personel seçin.";
        }

        protected override void RefreshEditState()
        {
            pager?.UpdateState();
            foreach (var field in new[] { TC_txt, ad_txt, soyad_txt, IBAN_txt }) field.ReadOnly = Session?.CanEdit != true;
            IBAN_txt.ReadOnly = Session?.CanEdit != true || Session?.CanViewIban != true;
            ekle_btn.Enabled = Session?.CanEdit == true && Session?.CanViewIban == true && !IsOperationRunning;
            money_txt.ReadOnly = Session?.CanPay != true || Session?.CanEdit != true || Session?.CanViewIban != true;
            if (Session?.CanViewSalary == true && money_txt.ReadOnly && string.IsNullOrWhiteSpace(money_txt.Text)) money_txt.Text = MoneyInput.Format(0);
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
            money_txt.Text = Session?.CanViewSalary != true || Session?.CanPay == true ? "" : MoneyInput.Format(0);
            ResetLeaveInputs();
        }
    }
}

