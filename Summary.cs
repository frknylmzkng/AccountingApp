using System.Data;

namespace WinFormsApp2;

public partial class Summary : SessionForm
{
    private readonly ListPager pager;
    private readonly Label balances = new() { AutoSize = true, Location = new Point(76, 325) };
    private readonly DataGridView alerts = new() { Name = "LeaveAlerts", Location = new Point(50, 400), Size = new Size(700, 230) };
    public Summary()
    {
        InitializeComponent(); ArrangeSummary(); RecordSelection.Configure(alerts); RefreshAction = LoadSummaryAsync;
        pager = new ListPager(this, LoadSummaryAsync, "izin kaydı");
        ((SummaryLeavePanel)alerts.Parent!).SetPager(pager);
        alerts.Tag = "AccountingApp.Paged";
        alerts.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || alerts.Rows[e.RowIndex].DataBoundItem is not DataRowView row || row.Row["IZINBITIS"] is not DateTime end) return;
            e.CellStyle.BackColor = end.Date < DateTime.Today ? Color.FromArgb(127, 29, 29) : Color.FromArgb(254, 243, 199);
            e.CellStyle.ForeColor = end.Date < DateTime.Today ? Color.White : Color.Black;
        };
        // Apply colors before Show creates/paints the window, not only in Load.
        AppTheme.Apply(this);
    }
    private async Task LoadSummaryAsync() => await RunOperationAsync(async token =>
    {
        var data = await People.SummaryAsync(DateTime.Today, pager.Index, pager.SizeLimit, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        personelLbl.Text = data.GeneralCount.ToString(); jokerLabel.Text = data.JokerCount.ToString();
        toplamLbl.Text = (data.GeneralCount + data.JokerCount).ToString(); bugunLbl.Text = data.OnLeaveCount.ToString();
        balances.Text = data.Balance.HasValue ? $"{data.Balance:N2} TL" : "Yetki yok"; averageBalance.Text = data.Average.HasValue ? $"{data.Average:N2} TL" : "Yetki yok";
        alerts.DataSource = data.Alerts.Rows;
        alerts.Columns["IzinId"]!.Visible = false;
        alerts.ClearSelection(); alerts.CurrentCell = null; pager.Apply(data.Alerts);
    });
    protected override void RefreshEditState() => pager?.UpdateState();
    private protected override IReadOnlyList<ExportReportChoice> ExportReports()
    {
        var reports = base.ExportReports().ToList();
        DateTime day = DateTime.Today;
        reports.Add(new("Genel Durum — tüm etkin personel özeti", $"{day:dd.MM.yyyy}. SQL'den güncel kişi sayıları/bakiye/ortalama. Görünen izin sayfasıyla sınırlı değildir.",
            async token =>
            {
                var s = await People.SummaryAsync(day, 0, 1, token);
                var table = new DataTable("Genel Durum");
                table.Columns.Add("İş günü", typeof(DateTime)); table.Columns.Add("Genel", typeof(long)); table.Columns.Add("Joker", typeof(long));
                table.Columns.Add("Toplam", typeof(long)); table.Columns.Add("Bugün izinli", typeof(long)); table.Columns.Add("Bakiye TL", typeof(decimal)); table.Columns.Add("Ortalama TL", typeof(decimal));
                table.Rows.Add(day, s.GeneralCount, s.JokerCount, s.GeneralCount + s.JokerCount, s.OnLeaveCount, s.Balance, s.Average);
                return table;
            }));
        return reports;
    }

    // Kept for deterministic designer/preview fixtures only. Runtime uses SQL aggregates.
    internal void DisplaySummary(DataTable data)
    {
        var people = data.AsEnumerable().GroupBy(r => (r.Field<long>("TC"), r.Field<string>("GRUP"))).Select(g => g.First()).ToArray();
        jokerLabel.Text = people.Count(r => r.Field<string>("GRUP") == "Joker").ToString();
        personelLbl.Text = people.Count(r => r.Field<string>("GRUP") == "Genel").ToString(); toplamLbl.Text = people.Length.ToString();
        bugunLbl.Text = data.AsEnumerable().Where(r => r["IZINBASLANGIC"] is DateTime start && r["IZINBITIS"] is DateTime end && start.Date <= DateTime.Today && end.Date >= DateTime.Today).Select(r => r.Field<long>("TC")).Distinct().Count().ToString();
        decimal total = people.Sum(r => r.Field<decimal?>("PARA") ?? 0);
        decimal average = Calculations.WeightedAverage(people.Select(r => (r.Field<decimal?>("PARA") ?? 0, 1m)));
        balances.Text = $"{total:N2} TL"; averageBalance.Text = $"{average:N2} TL";
        var view = data.Clone(); view.Columns.Add("DURUM");
        foreach (var r in data.AsEnumerable().Where(r => r["IZINBASLANGIC"] is DateTime start && r["IZINBITIS"] is DateTime end && start.Date <= DateTime.Today.AddDays(30) && end.Date >= DateTime.Today.AddDays(-30)).OrderBy(r => r.Field<DateTime>("IZINBITIS") < DateTime.Today ? 0 : 1).ThenBy(r => r.Field<DateTime>("IZINBASLANGIC"))) view.ImportRow(r);
        alerts.DataSource = view;
        foreach (DataRow row in view.Rows) row["DURUM"] = ((DateTime)row["IZINBITIS"]).Date < DateTime.Today ? "Süresi bitti" : ((DateTime)row["IZINBASLANGIC"]).Date <= DateTime.Today ? "İzin sürüyor" : "Yaklaşıyor";
        alerts.ClearSelection(); alerts.CurrentCell = null;
    }
    private async void Summary_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadSummaryAsync(); }
    private void back_Click(object sender, EventArgs e) => RequestBack();
}
