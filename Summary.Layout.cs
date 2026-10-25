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
        Text = "Muhasebe — Genel Durum"; ClientSize = new Size(1120, 610);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Genel Durum", "Personel sayıları, kalan ödeme tutarları ve yakın dönem izinleri"), 0, 0);
        GroupBox Metric(string title, Label value)
        {
            var group = new GroupBox { Text = title, Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 7, 3, 3) };
            value.AutoSize = false; value.Dock = DockStyle.Fill; value.TextAlign = ContentAlignment.MiddleCenter; value.Font = AppTheme.TitleFont; value.Text = "—"; value.AutoEllipsis = true;
            group.Controls.Add(value); return group;
        }
        var counts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
        counts.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var (title, value) in new[] { ("Genel personel", personelLbl), ("Joker personel", jokerLabel), ("Bugün izinli", bugunLbl), ("Toplam personel", toplamLbl) })
        {
            counts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); counts.Controls.Add(Metric(title, value));
        }
        outer.Controls.Add(counts, 0, 1);
        var amounts = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        amounts.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); amounts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); amounts.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        balances.Name = "TotalBalance";
        amounts.Controls.Add(Metric("Kalan ödeme toplamı", balances), 0, 0); amounts.Controls.Add(Metric("Personel başına ortalama", averageBalance), 1, 0); outer.Controls.Add(amounts, 0, 2);
        var leaves = new GroupBox { Text = "İzin takibi", Dock = DockStyle.Fill, Padding = new Padding(8, 18, 8, 8), Margin = new Padding(3, 8, 3, 3) };
        var list = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        list.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); list.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        list.Controls.Add(new Label { Text = "Son 30 gün / bugün / gelecek 30 gün. Süresi biten izinler koyu kırmızı, diğer kayıtlar sarıdır.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        alerts.Dock = DockStyle.Fill; alerts.RowHeadersWidth = 28;
        alerts.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 96), "AD" => ("Adı", 105), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 215),
                "PARA" => ("Kalan (TL)", 100), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 70), "DURUM" => ("Durum", 100), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption; e.Column.Width = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        list.Controls.Add(alerts, 0, 1); leaves.Controls.Add(list); outer.Controls.Add(leaves, 0, 3);
        Controls.Add(outer); ResumeLayout(true);
    }
}
