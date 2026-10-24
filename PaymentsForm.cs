using System.Data;
using System.Globalization;

namespace WinFormsApp2;

internal sealed class PaymentsForm : SessionForm
{
    private readonly DataGridView people = new() { Name = "People", Dock = DockStyle.Fill };
    private readonly DataGridView ledger = new() { Name = "Ledger", Dock = DockStyle.Fill };
    private readonly TextBox amount = new() { Name = "paymentAmount", Width = 130, PlaceholderText = "Ödeme tutarı TL" };
    private readonly CheckBox thermal = new() { Text = "80 mm fiş", AutoSize = true };
    private readonly DateTimePicker day = new() { Width = 130, Format = DateTimePickerFormat.Short };
    private readonly TextBox declared = new() { Name = "declaredTotal", Width = 130, PlaceholderText = "Beyan edilen TL" };
    private Guid requestId = Guid.NewGuid();
    public PaymentsForm()
    {
        Text = "Personel ödemeleri ve defteri"; ClientSize = new Size(1080, 680);
        RecordSelection.Configure(people); RecordSelection.Configure(ledger);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = ClientSize, Orientation = Orientation.Horizontal, SplitterDistance = 280 }; split.Panel1.Controls.Add(people); split.Panel2.Controls.Add(ledger);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.TopDown, Padding = new Padding(10) };
        var pay = new Button { Text = "Ödeme kaydet", AutoSize = true }; var reverse = new Button { Text = "Ödemeyi iptal et", AutoSize = true };
        var receipt = new Button { Text = "Seçili ödeme fişi", AutoSize = true }; var closing = new Button { Text = "Günlük kapanış", AutoSize = true };
        var paymentRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        paymentRow.Controls.AddRange([new Label { Text = "Tutar (TL)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, amount, pay, reverse, receipt, thermal]);
        var closingRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        closingRow.Controls.AddRange([new Label { Text = "Kapanış günü", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, day, new Label { Text = "Beyan (TL)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, declared, closing]);
        bar.Controls.AddRange([new Label { Text = "Ödeme: seçili personelin kalan tutarını azaltır.", AutoSize = true }, paymentRow, closingRow]);
        Controls.Add(split); Controls.Add(bar); EditControls.Add(pay);
        pay.Click += Pay; reverse.Click += Reverse; receipt.Click += Receipt; closing.Click += PrintClosing;
        reverse.Enabled = false; Load += async (_, _) => { reverse.Enabled = Session?.Role == UserRole.Admin; if (!PreviewOnly) await ReloadAsync(); };
        RefreshAction = ReloadAsync;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var list = await People.ListAsync(null, token); var history = await People.LedgerAsync(null, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        // One selectable row per person even when a person has multiple leave periods.
        var distinct = list.Clone(); foreach (var group in list.AsEnumerable().GroupBy(r => (r.Field<long>("TC"), r.Field<string>("GRUP")))) distinct.ImportRow(group.First());
        people.DataSource = distinct; people.ClearSelection(); ledger.DataSource = history; ledger.ClearSelection();
    });
    private async void Pay(object? sender, EventArgs e)
    {
        if (!RecordSelection.TryGetTc(people, out long tc) || !MoneyInput.TryParse(amount.Text, out decimal value) || value <= 0) { MessageBox.Show(this, "Personel seçin ve pozitif ödeme tutarı girin."); return; }
        if (!RecordSelection.TryGetKey(people, "GRUP", out string selectedGroup)) return;
        var group = selectedGroup == "Joker" ? PersonnelGroup.Joker : PersonnelGroup.General;
        if (MessageBox.Show(this, $"TC …{tc.ToString(CultureInfo.InvariantCulture)[^4..]} için {value:N2} TL ödeme kaydedilsin mi?", "Ödeme onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => People.PayAsync(group, tc, value, requestId, token), "Ödeme kaydedildi.") && !IsDisposed) { requestId = Guid.NewGuid(); amount.Clear(); await ReloadAsync(); }
    }
    private DataRow? SelectedPayment() => ledger.SelectedRows.Count == 1 && ledger.SelectedRows[0].DataBoundItem is DataRowView row && row.Row["RequestId"] is Guid ? row.Row : null;
    private async void Reverse(object? sender, EventArgs e)
    {
        var row = SelectedPayment(); if (row == null) return;
        if (MessageBox.Show(this, "Ödeme iptal edilerek kalan tutara geri eklensin mi?", "Ödeme iptali", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunOperationAsync(token => People.ReversePaymentAsync((Guid)row["RequestId"], token), "Ödeme iptal edildi.") && !IsDisposed) await ReloadAsync();
    }
    private async void Receipt(object? sender, EventArgs e)
    {
        var row = SelectedPayment(); if (row == null) { MessageBox.Show(this, "Defterden bir ödeme satırı seçin."); return; }
        var lines = new List<string> { $"Personel: TC {PaymentPrinting.Mask(row["TC"].ToString()!)}", $"Tutar: {-Convert.ToDecimal(row["Delta"]):N2} TL", $"Kayıt UTC: {row["CreatedUtc"]}", $"Durum: {(row["ReversedUtc"] == DBNull.Value ? "Kaydedildi" : "İptal edildi")}" };
        string tc = row["TC"].ToString()!; var group = (PersonnelGroup)Convert.ToInt32(row["PersonnelGroup"]); string reference = ((Guid)row["RequestId"]).ToString("N");
        if (!await RunOperationAsync(async token =>
        {
            var persons = await People.ListAsync(group, token, tc);
            if (persons.Rows.Count == 0) lines.Add("Personel artık etkin listede yok.");
            else
            {
                var person = persons.Rows[0]; lines.Add("IBAN: " + PaymentPrinting.Mask(person["IBAN"].ToString()!)); lines.Add($"Güncel kalan tutar: {person["PARA"]:N2} TL");
                if (person["IZINBASLANGIC"] is DateTime start && person["IZINBITIS"] is DateTime end) lines.Add($"İzin: {start:dd.MM.yyyy} – {end:dd.MM.yyyy} ({Calculations.LeaveDays(start,end)} gün)");
            }
        }) || IsDisposed) return;
        var report = new PrintReport("PERSONEL ÖDEME FİŞİ", lines, reference);
        try { PaymentPrinting.Preview(this, report, thermal.Checked); } catch (Exception ex) { AppLog.Failure(ex, "Print.Preview"); MessageBox.Show(this, "Önizleme açılamadı. Yazıcı sürücüsünü kontrol edin."); }
    }
    private void PrintClosing(object? sender, EventArgs e)
    {
        if (ledger.DataSource is not DataTable data || !MoneyInput.TryParse(declared.Text, out decimal counted) || counted < 0) { MessageBox.Show(this, "Beyan edilen günlük toplamı girin."); return; }
        var rows = data.AsEnumerable().Where(r => r.Field<string>("Kind") == "Payment" && r["ReversedUtc"] == DBNull.Value && DateTime.SpecifyKind(r.Field<DateTime>("CreatedUtc"), DateTimeKind.Utc).ToLocalTime().Date == day.Value.Date).ToArray();
        decimal total = -rows.Sum(r => r.Field<decimal>("Delta"));
        var lines = new List<string> { $"Gün: {day.Value:dd.MM.yyyy}", $"Geçerli ödeme sayısı: {rows.Length}", $"Kayıt toplamı: {total:N2} TL", $"Beyan edilen: {counted:N2} TL" };
        lines.AddRange(rows.Select(r => $"TC …{r.Field<long>("TC").ToString()[^4..]}   {-r.Field<decimal>("Delta"):N2} TL"));
        try { PaymentPrinting.Preview(this, new("GÜNLÜK ÖDEME KAPANIŞI", lines, "closing-" + day.Value.ToString("yyyyMMdd"), counted - total), thermal.Checked); }
        catch (Exception ex) { AppLog.Failure(ex, "Print.Closing"); MessageBox.Show(this, "Kapanış önizlemesi açılamadı."); }
    }
}
