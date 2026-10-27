namespace WinFormsApp2;

public partial class Summary
{
    private readonly Label averageBalance = new() { Name = "AverageBalance" };

    private void ArrangeSummary()
    {
        SuspendLayout();
        var retained = new Control[] { jokerLabel, personelLbl, bugunLbl, toplamLbl, balances };
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — Genel Durum"; ClientSize = new Size(1120, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Genel Durum", "Personel, kalan ödeme tutarları ve yakın dönem izinleri"), 0, 0);
        var counts = new[]
        {
            new SummaryMetricCard("Genel personel", personelLbl), new SummaryMetricCard("Joker personel", jokerLabel),
            new SummaryMetricCard("Bugün izinli", bugunLbl), new SummaryMetricCard("Toplam personel", toplamLbl)
        };
        balances.Name = "TotalBalance";
        var amounts = new[] { new SummaryMetricCard("Kalan ödeme toplamı", balances), new SummaryMetricCard("Personel başına ortalama", averageBalance) };
        var leaves = new SummaryLeavePanel(alerts) { TabIndex = 0 };
        alerts.Dock = DockStyle.None; alerts.RowHeadersWidth = 28;
        alerts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        alerts.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 110), "AD" => ("Adı", 100), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 220),
                "PARA" => ("Kalan (TL)", 105), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 55), "DURUM" => ("Durum", 100), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption;
            e.Column.MinimumWidth = alerts.LogicalToDeviceUnits(width);
            e.Column.FillWeight = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        var workspace = new SummaryWorkspace(counts, amounts, leaves) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toPeople = new Button { Name = "summaryShowPeople", Text = "&Personel özeti", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toBalances = new Button { Name = "summaryShowBalances", Text = "&Bakiye özeti", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 1 };
        var toLeaves = new Button { Name = "summaryShowLeaves", Text = "İzin &takibi", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 2 };
        toPeople.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toBalances.Click += (_, _) => workspace.ScrollControlIntoView(amounts[^1]);
        toLeaves.Click += (_, _) => workspace.ScrollControlIntoView(leaves);
        navigation.Controls.Add(toPeople); navigation.Controls.Add(toBalances); navigation.Controls.Add(toLeaves);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2);
        Controls.Add(outer); ResumeLayout(true);
    }
}
