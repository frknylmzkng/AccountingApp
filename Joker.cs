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
            ArrangePersonnel();
            RecordSelection.Configure(jokerView);
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
            var table = await People.ListAsync(PersonnelGroup.Joker, token);
            if (IsDisposed || token.IsCancellationRequested) return;
            table.Columns["TC"]!.ReadOnly = true;
            bindingRecords = true;
            try
            {
                jokerView.DataSource = table;
                jokerView.ClearSelection();
            }
            finally { bindingRecords = false; }
        }

        private async void ekle_btn_Click(object sender, EventArgs e)
        {
            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.Joker, person, true, token),
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

            PersonnelDetails? person = ReadInputs();
            if (person == null) return;
            await ApplyChangeAsync(token => People.SaveAsync(PersonnelGroup.Joker, person, false, token, RecordSelection.OriginalVersion(jokerView)),
                "Veri başarıyla güncellendi!");
        }

        private async void sil_btn_Click(object sender, EventArgs e)
        {
            if (!RecordSelection.TryGetTc(jokerView, out long tc))
            {
                MessageBox.Show("Lütfen silmek için listeden bir kayıt seçin!");
                return;
            }
            byte[] version;
            try { version = RecordSelection.OriginalVersion(jokerView); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message); return; }
            if (MessageBox.Show($"{tc} numaralı Joker kaydını silmek istediğinize emin misiniz?",
                "Silme Onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            await ApplyChangeAsync(token => People.DeleteAsync(PersonnelGroup.Joker, tc, version, token),
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

        private async Task ApplyChangeAsync(Func<CancellationToken, Task> change, string successMessage)
        {
            if (!await RunOperationAsync(change, successMessage) || IsDisposed) return;
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

