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
    private readonly Button receipt = new() { Text = "Seçili ödeme fişi" };
    private readonly Button closing = new() { Text = "Güncel önizleme" };
    private readonly Button acknowledge = new() { Text = "Sonucu onayla / yeni ödeme", AutoSize = true };
    private readonly Label paymentState = new() { AutoSize = true, MaximumSize = new Size(1000, 0) };
    private PaymentState? pending;
    private bool paymentStateLoaded;
    private readonly ListPager peoplePager, ledgerPager;
    private PaymentWorkflow Workflow => new(AppDatabase.ConnectionString, User);
    internal Action<IWin32Window, PrintReport, bool> PreviewReport = PaymentPrinting.Preview;
    public PaymentsForm()
    {
        Text = "Personel ödemeleri ve defteri"; ClientSize = new Size(1080, 680);
        day.Value = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DailyClosingService.BusinessZone).Date;
        RecordSelection.Configure(people); RecordSelection.Configure(ledger);
        var reverse = new Button { Text = "Ödemeyi iptal et" };
        var inspect = new Button { Text = "Ödeme sonucunu kontrol et" };
        ArrangePayments(reverse, receipt, closing, inspect);
        peoplePager = ListPager.Attach(people, this, ReloadAsync);
        ledgerPager = ListPager.Attach(ledger, this, ReloadAsync, "hareket");
        TrackChanges("Yeni ödeme", amount);
        pay.Click += Pay; reverse.Click += Reverse; receipt.Click += Receipt; closing.Click += PrintClosing;
        inspect.Click += async (_, _) => await RefreshScreenAsync();
        acknowledge.Click += async (_, _) =>
        {
            if (await RunOperationAsync(token => Workflow.AcknowledgeAsync(token)))
            {
                if (IsDisposed) return;
                amount.Clear(); await ReloadAsync();
            }
        };
        pay.Enabled = false; acknowledge.Enabled = false;
        reverse.Enabled = false; Load += async (_, _) => { reverse.Enabled = Session?.Role == UserRole.Admin; RefreshEditState(); if (!PreviewOnly) await ReloadAsync(); };
        RefreshAction = ReloadAsync;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var state = User.CanViewSalary ? await Workflow.InspectAsync(token) : null;
        var page = await People.PageAsync(null, peoplePager.Index, peoplePager.SizeLimit, token, includeLeaves: false);
        var history = await People.LedgerPageAsync(ledgerPager.Index, ledgerPager.SizeLimit, token);
        var list = page.Rows;
        if (IsDisposed || token.IsCancellationRequested) return;
        // One selectable row per person even when a person has multiple leave periods.
        var distinct = list.Clone(); foreach (var group in list.AsEnumerable().GroupBy(r => (r.Field<long>("TC"), r.Field<string>("GRUP")))) distinct.ImportRow(group.First());
        people.DataSource = distinct; people.ClearSelection(); ledger.DataSource = history.Rows; ledger.ClearSelection();
        peoplePager.Apply(page); ledgerPager.Apply(history);
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
            string person = (state.Request.Purpose == PaymentPurpose.Advance ? "Avans · " : "Bakiye ödemesi · ") +
                $"TC …{state.Request.Tc.ToString(CultureInfo.InvariantCulture)[^4..]} · {state.Request.Amount:N2} TL: ";
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
        peoplePager?.UpdateState(); ledgerPager?.UpdateState();
        bool edit = Session?.CanPay == true && paymentStateLoaded && !IsOperationRunning;
        pay.Enabled = edit && (pending == null || pending.Outcome.Status == PaymentStatus.Unconfirmed);
        acknowledge.Enabled = edit && pending != null && pending.Outcome.Status != PaymentStatus.Unconfirmed;
        amount.Enabled = edit && pending == null;
        people.Enabled = !IsOperationRunning && pending == null;
        receipt.Enabled = closing.Enabled = Session?.CanViewSalary == true && Session?.CanExportPersonalData == true && !IsOperationRunning;
    }
    private async void Pay(object? sender, EventArgs e)
    {
        long tc; decimal value; PersonnelGroup group;
        byte[]? selectedVersion = null;
        bool retry = pending != null;
        if (pending != null) { tc = pending.Request.Tc; value = pending.Request.Amount; group = pending.Request.Group; }
        else
        {
            if (!MoneyInput.TryParse(amount.Text, out value) || value <= 0) { MessageBox.Show(this, "Personel seçin ve pozitif ödeme tutarı girin."); return; }
            // Freeze the original identity/version before the confirmation dialog.
            PaymentTarget selected;
            try { selected = PaymentTarget.FromSelection(people); }
            catch (InvalidOperationException ex) { MessageBox.Show(this, ex.Message, "Ödeme seçimi", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            tc = selected.Tc; group = selected.Group; selectedVersion = selected.PersonVersion;
        }
        string question = $"TC …{tc.ToString(CultureInfo.InvariantCulture)[^4..]} için {value:N2} TL " + (retry ? "önceki ödeme kontrol edilerek yeniden denensin mi?" : "ödeme kaydedilsin mi?");
        if (MessageBox.Show(this, question, "Ödeme onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        await (retry ? RunOperationAsync(Persist) : RunChangeOperationAsync(Persist));
        async Task Persist(CancellationToken token)
        {
            var state = retry ? await Workflow.RetryAsync(token) : await Workflow.BeginAsync(group, tc, value, selectedVersion!, token);
            if (!IsDisposed && !token.IsCancellationRequested) { ApplyPaymentState(state); AcceptTrackedChanges("Yeni ödeme"); }
        }
        if (!IsDisposed) await ReloadAsync();
    }
    private DataRow? SelectedPayment() => ledger.SelectedRows.Count == 1 && ledger.SelectedRows[0].DataBoundItem is DataRowView row && row.Row["RequestId"] is Guid ? row.Row : null;
    private protected override IReadOnlyList<ExportReportChoice> ExportReports()
    {
        var reports = base.ExportReports().ToList();
        if (SelectedPayment() is { } row)
        {
            Guid id = (Guid)row["RequestId", DataRowVersion.Original];
            reports.Add(new("Seçili ödeme fişi — güncel durum", $"Ödeme {id:D}. Güncel SQL kaydından oluşturulur; personel/defter sayfasıyla sınırlı değildir.",
                async token => ExportReportTables.FromPrint(await new PaymentReportService(AppDatabase.ConnectionString, User).ReceiptAsync(id, token))));
        }
        DateTime date = day.Value.Date; string declaration = declared.Text;
        reports.Add(new("Güncel günlük ödeme önizlemesi", $"{date:dd.MM.yyyy}; geçerli ödemelerin güncel durumu. Kalıcı nakit kapanışı değildir. Beyan alanı kullanılır.",
            async token =>
            {
                if (!MoneyInput.TryParse(declaration, out decimal amount)) throw new ArgumentException("Önce geçerli beyan tutarı girin.");
                return ExportReportTables.FromPrint(await new PaymentReportService(AppDatabase.ConnectionString, User).ClosingAsync(date, amount, token));
            }));
        return reports;
    }
    private async void Reverse(object? sender, EventArgs e)
    {
        var row = SelectedPayment(); if (row == null) return;
        Guid requestId = (Guid)row["RequestId", DataRowVersion.Original];
        if (MessageBox.Show(this, "Ödeme iptal edilerek kalan tutara geri eklensin mi?", "Ödeme iptali", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunChangeOperationAsync(token => People.ReversePaymentAsync(requestId, token), "Ödeme iptal edildi.") && !IsDisposed) await ReloadAsync();
    }
    private async void Receipt(object? sender, EventArgs e)
    {
        var row = SelectedPayment(); if (row == null) { MessageBox.Show(this, "Defterden bir ödeme satırı seçin."); return; }
        Guid requestId = (Guid)row["RequestId", DataRowVersion.Original];
        bool paper = thermal.Checked; PrintReport? report = null;
        if (!await RunOperationAsync(async token => report = await new PaymentReportService(AppDatabase.ConnectionString, User).ReceiptAsync(requestId, token)) || IsDisposed) return;
        try { PreviewReport(this, report!, paper); } catch (Exception ex) { AppLog.Failure(ex, "Print.Preview"); MessageBox.Show(this, "Önizleme açılamadı. Yazıcı sürücüsünü kontrol edin."); }
    }
    private async void PrintClosing(object? sender, EventArgs e)
    {
        if (!MoneyInput.TryParse(declared.Text, out decimal counted) || counted < 0) { MessageBox.Show(this, "Beyan edilen günlük toplamı girin."); return; }
        DateTime selectedDay = day.Value.Date; bool paper = thermal.Checked; PrintReport? report = null;
        if (!await RunOperationAsync(async token => report = await new PaymentReportService(AppDatabase.ConnectionString, User).ClosingAsync(selectedDay, counted, token)) || IsDisposed) return;
        try { PreviewReport(this, report!, paper); }
        catch (Exception ex) { AppLog.Failure(ex, "Print.Closing"); MessageBox.Show(this, "Kapanış önizlemesi açılamadı."); }
    }
}
