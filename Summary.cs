using System.Data;

namespace WinFormsApp2;

public partial class Summary : SessionForm
{
    private readonly Label balances = new() { AutoSize = true, Location = new Point(76, 325) };
    private readonly DataGridView alerts = new() { Name = "LeaveAlerts", Location = new Point(50, 400), Size = new Size(700, 230) };
    public Summary()
    {
        InitializeComponent(); ClientSize = new Size(800, 650); Controls.Add(balances); Controls.Add(alerts); RecordSelection.Configure(alerts); RefreshAction = LoadSummaryAsync;
        alerts.CellFormatting += (_, e) =>
        {
            if (e.RowIndex < 0 || alerts.Rows[e.RowIndex].DataBoundItem is not DataRowView row || row.Row["IZINBITIS"] is not DateTime end) return;
            e.CellStyle.BackColor = end.Date < DateTime.Today ? Color.FromArgb(127, 29, 29) : Color.FromArgb(254, 243, 199);
            e.CellStyle.ForeColor = end.Date < DateTime.Today ? Color.White : Color.Black;
        };
    }
    private async Task LoadSummaryAsync() => await RunOperationAsync(async token =>
    {
        var data = await People.ListAsync(null, token);
        if (IsDisposed || token.IsCancellationRequested) return;
        var people = data.AsEnumerable().GroupBy(r => (r.Field<long>("TC"), r.Field<string>("GRUP"))).Select(g => g.First()).ToArray();
        jokerLabel.Text = people.Count(r => r.Field<string>("GRUP") == "Joker").ToString();
        personelLbl.Text = people.Count(r => r.Field<string>("GRUP") == "Genel").ToString(); toplamLbl.Text = people.Length.ToString();
        bugunLbl.Text = data.AsEnumerable().Where(r => r["IZINBASLANGIC"] is DateTime start && r["IZINBITIS"] is DateTime end && start.Date <= DateTime.Today && end.Date >= DateTime.Today).Select(r => r.Field<long>("TC")).Distinct().Count().ToString();
        decimal total = people.Sum(r => r.Field<decimal?>("PARA") ?? 0);
        decimal average = Calculations.WeightedAverage(people.Select(r => (r.Field<decimal?>("PARA") ?? 0, 1m)));
        balances.Text = $"Kalan ödeme toplamı: {total:N2} TL\nPersonel başına ortalama: {average:N2} TL\nİzinler: son 30 günde biten ve gelecek 30 günde başlayacak kayıtlar";
        var view = data.Clone(); view.Columns.Add("DURUM");
        foreach (var r in data.AsEnumerable().Where(r => r["IZINBASLANGIC"] is DateTime start && r["IZINBITIS"] is DateTime end && start.Date <= DateTime.Today.AddDays(30) && end.Date >= DateTime.Today.AddDays(-30)).OrderBy(r => r.Field<DateTime>("IZINBITIS") < DateTime.Today ? 0 : 1).ThenBy(r => r.Field<DateTime>("IZINBASLANGIC"))) view.ImportRow(r);
        alerts.DataSource = view;
        foreach (DataRow row in view.Rows) row["DURUM"] = ((DateTime)row["IZINBITIS"]).Date < DateTime.Today ? "Süresi bitti" : ((DateTime)row["IZINBASLANGIC"]).Date <= DateTime.Today ? "İzin sürüyor" : "Yaklaşıyor";
    });
    private async void Summary_Load(object sender, EventArgs e) { if (!PreviewOnly) await LoadSummaryAsync(); }
    private void back_Click(object sender, EventArgs e) => RequestBack();
}
