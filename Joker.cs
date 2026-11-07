using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    public partial class Joker : SessionForm
    {
        private readonly ListPager pager;
        public Joker()
        {
            InitializeComponent();
            ArrangePersonnel();
            RecordSelection.Configure(jokerView);
            pager = ListPager.Attach(jokerView, this, LoadDataAsync);
            TrackChanges("Personel kartı", TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt); GuardRecordSelection(jokerView);
            PersonnelDataReview.Attach(this, jokerView, IBAN_txt);
            EditControls.AddRange([ekle_btn, edit_btn, sil_btn]);
            RefreshAction = () => LoadDataAsync(); ClearAction = Temizle; DeleteAction = () => sil_btn_Click(this, EventArgs.Empty);
        }

        private string connectionString => AppDatabase.ConnectionString;

        private bool bindingRecords;

        private void back_Click_1(object sender, EventArgs e)
        {
            RequestBack();
        }

        public async Task VerilerAsync(CancellationToken token = default)
        {
            var result = await People.PageAsync(PersonnelGroup.Joker, pager.Index, pager.SizeLimit, token);
            var table = result.Rows;
            if (IsDisposed || token.IsCancellationRequested) return;
            pager.Apply(result);
            table.Columns["TC"]!.ReadOnly = true;
            bindingRecords = true;
            try
            {
                jokerView.DataSource = table;
                jokerView.ClearSelection();
            }
            finally { bindingRecords = false; }
            Temizle();
            AcceptTrackedChanges();
        }

        private async void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            using var startDialog = new EmploymentDateDialog(true);
            if (startDialog.ShowDialog(this) != DialogResult.OK) return;
            DateTime start = startDialog.SelectedDate!.Value;
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.Joker, person, true, token, employmentStart: start),
                "Veriler eklendi!");
        }

        private async void edit_btn_Click(object sender, EventArgs e)
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

            if (!User.CanViewSalary || !User.CanViewIban)
            {
                if (jokerView.SelectedRows[0].DataBoundItem is not DataRowView selected) return;
                var personId = selected.Row.Field<Guid>("PersonId", DataRowVersion.Original);
                var request = new AccountingApp.Contracts.PersonnelCardDetails(AccountingApp.Contracts.PersonnelGroup.Joker, personId, RecordSelection.OriginalVersion(jokerView), ad_txt.Text, soyad_txt.Text, User.CanViewIban ? IBAN_txt.Text : null, "");
                await ApplyChangeAsync(ct => new PersonnelCardService(AppDatabase.ConnectionString, User).SaveDetailsAsync(request with { Reason = RemoteApi.Reason }, ct), "Personel bilgileri güncellendi; gizli alanlar korundu.");
                return;
            }

            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            byte[] version = RecordSelection.OriginalVersion(jokerView);
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.Joker, person, false, token, version),
                "Veri başarıyla güncellendi!");
        }

        private async void sil_btn_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardChanges("Personeli arşivleme")) return;
            if (!RecordSelection.TryGetTc(jokerView, out long tc))
            {
                MessageBox.Show("Arşivlemek için listeden bir kayıt seçin.");
                return;
            }
            byte[] version;
            try { version = RecordSelection.OriginalVersion(jokerView); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
            if (MessageBox.Show($"{tc} numaralı Joker kaydı arşivlenecek. Kart, ödeme geçmişi ve kalan bakiye korunacak. Devam edilsin mi?",
                "Arşivleme onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using var endDialog = new EmploymentDateDialog(false);
            if (endDialog.ShowDialog(this) != DialogResult.OK) return;
            DateTime? end = endDialog.SelectedDate;
            await ApplyChangeAsync(token => People.ArchiveAsync(PersonnelGroup.Joker, tc, version, token, end),
                "Personel arşivlendi; geçmiş ve bakiye korundu.");
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

        private async Task ApplyChangeAsync(Func<CancellationToken, Task> change, string successMessage)
        {
            if (!await RunChangeOperationAsync(change, successMessage) || IsDisposed) return;
            Temizle();
            await LoadDataAsync();
        }

        private async Task LoadDataAsync() => await RunOperationAsync(VerilerAsync);

        private async void Joker_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadDataAsync(); }

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
            AcceptTrackedChanges();
        }

        private void Temizle()
        {
            jokerView.ClearSelection();
            TC_txt.Clear();
            ad_txt.Clear();
            soyad_txt.Clear();
            IBAN_txt.Clear();
            money_txt.Text = Session?.CanViewSalary != true || Session?.CanPay == true ? "" : MoneyInput.Format(0);
        }
        protected override void RefreshEditState()
        {
            pager?.UpdateState();
            foreach (var field in new[] { TC_txt, ad_txt, soyad_txt, IBAN_txt }) field.ReadOnly = Session?.CanEdit != true;
            IBAN_txt.ReadOnly = Session?.CanEdit != true || Session?.CanViewIban != true;
            ekle_btn.Enabled = Session?.CanEdit == true && Session?.CanViewIban == true && !IsOperationRunning;
            money_txt.ReadOnly = Session?.CanPay != true || Session?.CanEdit != true || Session?.CanViewIban != true;
            if (Session?.CanViewSalary == true && money_txt.ReadOnly && string.IsNullOrWhiteSpace(money_txt.Text)) money_txt.Text = MoneyInput.Format(0);
        }
    }
}

