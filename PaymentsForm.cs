using System.Data;
using System.Globalization;

namespace WinFormsApp2;

internal sealed partial class PaymentsForm : SessionForm
{
    private readonly DataGridView people = new() { Name = "People", Dock = DockStyle.Fill };
    private readonly DataGridView ledger = new() { Name = "Ledger", Dock = DockStyle.Fill };
    private readonly TextBox amount = new() { Name = "paymentAmount", Width = 130, PlaceholderText = "Ödeme tutarı TL" };
    private readonly CheckBox thermal = new() { Text = "80 mm fiş", AutoSize = true };
    private readonly DateTimePicker day = new() { Width = 130, Format = DateTimePickerFormat.Short };
    private readonly TextBox declared = new() { Name = "declaredTotal", Width = 130, PlaceholderText = "Beyan edilen TL" };
    private readonly Button pay = new() { Text = "Ödeme kaydet", AutoSize = true };
    private readonly Button acknowledge = new() { Text = "Sonucu onayla / yeni ödeme", AutoSize = true };
    private readonly Label paymentState = new() { AutoSize = true, MaximumSize = new Size(1000, 0) };
    private PaymentState? pending;
    private bool paymentStateLoaded;
    private PaymentWorkflow Workflow => new(AppDatabase.ConnectionString, User);
    public PaymentsForm()
    {
        Text = "Personel ödemeleri ve defteri"; ClientSize = new Size(1080, 680);
        RecordSelection.Configure(people); RecordSelection.Configure(ledger);
        var reverse = new Button { Text = "Ödemeyi iptal et" };
        var receipt = new Button { Text = "Seçili ödeme fişi" };
        var closing = new Button { Text = "Kapanış önizlemesi" };
        var inspect = new Button { Text = "Ödeme sonucunu kontrol et" };
        ArrangePayments(reverse, receipt, closing, inspect);
        EditControls.Add(pay); EditControls.Add(acknowledge);
        pay.Click += Pay; reverse.Click += Reverse; receipt.Click += Receipt; closing.Click += PrintClosing;
        inspect.Click += async (_, _) => await ReloadAsync();
        acknowledge.Click += async (_, _) =>
        {
            if (await RunOperationAsync(token => Workflow.AcknowledgeAsync(token)))
            {
                if (IsDisposed) return;
                amount.Clear(); await ReloadAsync();
            }
        };
        pay.Enabled = false; acknowledge.Enabled = false;
        reverse.Enabled = false; Load += async (_, _) => { reverse.Enabled = Session?.Role == UserRole.Admin; if (!PreviewOnly) await ReloadAsync(); };
        RefreshAction = ReloadAsync;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var state = await Workflow.InspectAsync(token);
        var list = await People.ListAsync(null, token); var history = await People.LedgerAsync(null, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        // One selectable row per person even when a person has multiple leave periods.
        var distinct = list.Clone(); foreach (var group in list.AsEnumerable().GroupBy(r => (r.Field<long>("TC"), r.Field<string>("GRUP")))) distinct.ImportRow(group.First());
        people.DataSource = distinct; people.ClearSelection(); ledger.DataSource = history; ledger.ClearSelection();
        ApplyPaymentState(state);
    });
    private void ApplyPaymentState(PaymentState? state)
    {
        if (IsDisposed) return;
        pending = state; paymentStateLoaded = true;
        if (state == null) paymentState.Text = "Yeni ödeme için personel seçin. Kaydedilen sonucu onayladıktan sonra sonraki ödemeye geçebilirsiniz.";
        else
        {
            amount.Text = MoneyInput.Format(state.Request.Amount);
            string person = $"TC …{state.Request.Tc.ToString(CultureInfo.InvariantCulture)[^4..]} · {state.Request.Amount:N2} TL: ";
            paymentState.Text = person + (state.Outcome.Status switch
            {
                PaymentStatus.Recorded => "Ödeme kaydedildi. Yeni ödeme için sonucu onaylayın.",
                PaymentStatus.Reversed => "Bu ödeme daha önce kaydedilmiş ve iptal edilmiş. Yeni ödeme için sonucu onaylayın.",
                PaymentStatus.Rejected => "Ödeme yapılmadı. " + state.Outcome.Reason + " Yeni ödeme için sonucu onaylayın.",
                _ => "Sonuç henüz doğrulanamadı. Önceki ödemeyi aynı bilgilerle yeniden deneyebilirsiniz."
            });
        }
        pay.Text = state == null ? "Ödeme kaydet" : "Bekleyen ödemeyi yeniden dene";
    }
    protected override void RefreshEditState()
    {
        bool edit = Session?.CanEdit == true && paymentStateLoaded && !IsOperationRunning;
        pay.Enabled = edit && (pending == null || pending.Outcome.Status == PaymentStatus.Unconfirmed);
        acknowledge.Enabled = edit && pending != null && pending.Outcome.Status != PaymentStatus.Unconfirmed;
        amount.Enabled = edit && pending == null;
        people.Enabled = !IsOperationRunning && pending == null;
    }
    private async void Pay(object? sender, EventArgs e)
    {
        long tc; decimal value; PersonnelGroup group;
        bool retry = pending != null;
        if (pending != null) { tc = pending.Request.Tc; value = pending.Request.Amount; group = pending.Request.Group; }
        else
        {
            if (!RecordSelection.TryGetTc(people, out tc) || !MoneyInput.TryParse(amount.Text, out value) || value <= 0) { MessageBox.Show(this, "Personel seçin ve pozitif ödeme tutarı girin."); return; }
            if (!RecordSelection.TryGetKey(people, "GRUP", out string selectedGroup)) return;
            group = selectedGroup == "Joker" ? PersonnelGroup.Joker : PersonnelGroup.General;
        }
        string question = $"TC …{tc.ToString(CultureInfo.InvariantCulture)[^4..]} için {value:N2} TL " + (retry ? "önceki ödeme kontrol edilerek yeniden denensin mi?" : "ödeme kaydedilsin mi?");
        if (MessageBox.Show(this, question, "Ödeme onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await RunOperationAsync(async token =>
        {
            var state = retry ? await Workflow.RetryAsync(token) : await Workflow.BeginAsync(group, tc, value, token);
            if (!IsDisposed && !token.IsCancellationRequested) ApplyPaymentState(state);
        });
        if (!IsDisposed) await ReloadAsync();
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
    private async void PrintClosing(object? sender, EventArgs e)
    {
        if (!await RunOperationAsync(token => AccountAuthorization.ValidateAsync(AppDatabase.ConnectionString, User, AccountAccess.Read, token)) || IsDisposed) return;
        if (ledger.DataSource is not DataTable data || !MoneyInput.TryParse(declared.Text, out decimal counted) || counted < 0) { MessageBox.Show(this, "Beyan edilen günlük toplamı girin."); return; }
        var rows = data.AsEnumerable().Where(r => r.Field<string>("Kind") == "Payment" && r["ReversedUtc"] == DBNull.Value && DateTime.SpecifyKind(r.Field<DateTime>("CreatedUtc"), DateTimeKind.Utc).ToLocalTime().Date == day.Value.Date).ToArray();
        decimal total = -rows.Sum(r => r.Field<decimal>("Delta"));
        var lines = new List<string> { $"Gün: {day.Value:dd.MM.yyyy}", $"Geçerli ödeme sayısı: {rows.Length}", $"Kayıt toplamı: {total:N2} TL", $"Beyan edilen: {counted:N2} TL" };
        lines.AddRange(rows.Select(r => $"TC …{r.Field<long>("TC").ToString()[^4..]}   {-r.Field<decimal>("Delta"):N2} TL"));
        try { PaymentPrinting.Preview(this, new("GÜNLÜK ÖDEME KAPANIŞI", lines, "closing-" + day.Value.ToString("yyyyMMdd"), counted - total), thermal.Checked); }
        catch (Exception ex) { AppLog.Failure(ex, "Print.Closing"); MessageBox.Show(this, "Kapanış önizlemesi açılamadı."); }
    }
}
