using System.Data;

namespace WinFormsApp2;

internal sealed class DailyClosingsForm : SessionForm
{
    private readonly DataGridView records = new() { Name = "SavedDailyClosings", AccessibleName = "Kesinleşmiş kapanış listesi — açık sayfa", Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly DateTimePicker day = new() { Format = DateTimePickerFormat.Short, Width = 130 };
    private readonly TextBox declared = new() { Width = 150, PlaceholderText = "Beyan net TL (+/-)" };
    private readonly Button finalize = new() { Text = "Günü kesinleştir", AutoSize = true }, view = new() { Text = "Seçili kapanışı göster", AutoSize = true },
        print = new() { Text = "Seçili kapanışı yazdır", AutoSize = true };
    private readonly TextBox details = new() { ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, WordWrap = false,
        ShortcutsEnabled = false, ContextMenuStrip = new ContextMenuStrip() };
    private readonly ListPager pager;
    private DailyClosingService Service => new(AppDatabase.ConnectionString, User);
    internal DailyClosingsForm()
    {
        Text = "Muhasebe — Kesinleşmiş günlük kapanışlar"; ClientSize = new(1120, 760);
        RecordSelection.Configure(records);
        day.Value = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DailyClosingService.BusinessZone).Date.AddDays(-1);
        var reload = new Button { Text = "Yenile", AutoSize = true };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(8) };
        bar.Controls.AddRange([new Label { Text = "İş günü (Europe/Istanbul)", AutoSize = true, Margin = new(4, 7, 4, 0) }, day, declared, finalize, view, print, reload]);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new(SizeType.Percent, 55)); layout.RowStyles.Add(new(SizeType.Percent, 45));
        layout.Controls.Add(records, 0, 0); layout.Controls.Add(details, 0, 1); Controls.Add(layout); Controls.Add(bar);
        Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 52, Padding = new(8),
            Text = "Kapsam: tüm veritabanı, ödeme ve iptal/iade hareketleri. Yalnızca tamamlanmış günler kesinleşir. Kayıt değiştirilemez/silinemez; sonraki iptal kendi gününe iade olarak girer. Banka gerçekleşmesi değildir." });
        pager = ListPager.Attach(records, this, ReloadAsync, "kapanış");
        TrackChanges("Yeni kesin kapanış", day, declared);
        reload.Click += async (_, _) => await ReloadAsync(); RefreshAction = ReloadAsync;
        records.SelectionChanged += (_, _) => { details.Clear(); RefreshEditState(); };
        finalize.Click += FinalizeDay; view.Click += View; print.Click += Print;
        Load += async (_, _) => { MinimumSize = LogicalToDeviceUnits(new Size(760, 550)); if (!PreviewOnly) await ReloadAsync(); RefreshEditState(); };
    }
    private Guid? SelectedId => records.SelectedRows.Count == 1 && records.SelectedRows[0].DataBoundItem is DataRowView row
        ? row.Row.Field<Guid>("ClosingId", DataRowVersion.Original) : null;
    protected override void RefreshEditState()
    {
        pager?.UpdateState();
        finalize.Enabled = Session?.Role == UserRole.Admin && !IsOperationRunning;
        day.Enabled = finalize.Enabled; declared.ReadOnly = !finalize.Enabled;
        view.Enabled = SelectedId.HasValue && !IsOperationRunning;
        print.Enabled = Session?.CanExportPersonalData == true && SelectedId.HasValue && !IsOperationRunning;
    }
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var result = await Service.PageAsync(pager.Index, pager.SizeLimit, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        records.DataSource = result.Rows; records.ClearSelection(); details.Clear(); pager.Apply(result);
        var captions = new Dictionary<string, string> { ["ClosingId"] = "Kapanış kimliği", ["BusinessDate"] = "İş günü",
            ["TimeZoneId"] = "Saat dilimi", ["ClosedUtc"] = "Kesinleşme UTC", ["Actor"] = "Kesinleştiren",
            ["PaymentCount"] = "Ödeme adedi", ["ReversalCount"] = "İade adedi", ["CashOut"] = "Çıkış TL", ["CashReturn"] = "İade TL",
            ["NetCash"] = "Net TL", ["DeclaredCash"] = "Beyan TL", ["Difference"] = "Fark TL", ["Reason"] = "Gerekçe" };
        foreach (var item in captions) records.Columns[item.Key]!.HeaderText = item.Value;
        foreach (string name in new[] { "CashOut", "CashReturn", "NetCash", "DeclaredCash", "Difference" }) records.Columns[name]!.DefaultCellStyle.Format = "N2";
    });
    private async void FinalizeDay(object? sender, EventArgs e)
    {
        DateTime date = day.Value.Date;
        if (!MoneyInput.TryParse(declared.Text, out decimal value)) { MessageBox.Show(this, MoneyInput.ValidationMessage); return; }
        if (MessageBox.Show(this, $"{date:dd.MM.yyyy} · Europe/Istanbul · tüm veritabanı\nBeyan net: {value:N2} TL\nKayıt kalıcıdır. Bu güne sonradan nakit hareketi eklenemez. Kesinleştirilsin mi?",
            "Kesin kapanış onayı", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        if (await RunChangeOperationAsync(token => Service.FinalizeAsync(date, value, token), "Kapanış kaydı alındı; mevcut kayıt değiştirilmedi.", "Yeni kesin kapanış"))
        { pager.Reset(); await ReloadAsync(); }
    }
    private async void View(object? sender, EventArgs e)
    {
        if (SelectedId is not Guid id) return;
        await RunOperationAsync(async token =>
        {
            var s = await Service.ReadAsync(id, token);
            if (!IsDisposed) details.Text = $"KESİNLEŞMİŞ KAYIT: {s.ClosingId:D}\r\n{s.BusinessDate:dd.MM.yyyy} · {s.TimeZoneId}\r\nKesinleşme UTC: {s.ClosedUtc:O} · {s.Actor}\r\nGerekçe: {s.Reason}\r\nÇıkış: {s.CashOut:N2} · İade: {s.CashReturn:N2} · Net: {s.NetCash:N2} TL\r\nBeyan: {s.Declared:N2} · Fark: {s.Difference:N2} TL\r\nÖdeme: {s.PaymentCount} · İade: {s.ReversalCount}\r\n\r\n" +
                string.Join("\r\n", s.Rows.Select(r => $"{r.LedgerId} · TC {PaymentPrinting.Mask(r.Tc.ToString(System.Globalization.CultureInfo.InvariantCulture))} · {r.Kind} · {r.Delta:N2} TL"));
        });
    }
    private async void Print(object? sender, EventArgs e)
    {
        if (SelectedId is not Guid id) return;
        PrintReport? report = null;
        if (await RunOperationAsync(async token => report = await Service.PrintAsync(id, token)) && !IsDisposed)
        {
            try { PaymentPrinting.Preview(this, report!, false); }
            catch (Exception ex) { AppLog.Failure(ex, "Closing.Print"); MessageBox.Show(this, "Yazdırma önizlemesi açılamadı."); }
        }
    }
    private protected override IReadOnlyList<ExportReportChoice> ExportReports()
    {
        var reports = base.ExportReports().ToList();
        if (SelectedId is Guid id)
        {
            reports.Add(new("Seçili kesin kapanış — saklanan hareketler", $"Kalıcı kapanış {id:D}. Sayfadan bağımsız tüm saklanan hareketler; TC maskelidir. Güncel defterden yeniden hesaplanmaz.",
                token => Service.ExportLinesAsync(id, token)));
            reports.Add(new("Seçili kesin kapanış — saklanan özet", $"Kalıcı kapanış {id:D}. Saklanan başlık ve toplamlar.",
                token => Service.ExportSummaryAsync(id, token)));
        }
        return reports;
    }
}

internal static class ExportReportTables
{
    internal static DataTable FromPrint(PrintReport report)
    {
        var table = new DataTable("Rapor"); table.Columns.Add("Bilgi");
        table.Rows.Add(report.Title); table.Rows.Add("Rapor kimliği: " + report.Reference);
        foreach (string line in report.Lines) table.Rows.Add(line);
        return table;
    }
}
