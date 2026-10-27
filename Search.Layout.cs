namespace WinFormsApp2;

public partial class Search
{
    private GroupBox? searchResults;

    private void ArrangeSearch()
    {
        SuspendLayout();
        var retained = new Control[] { txtTC, txtAd, txtSoyad, label5, label4, label2, Ara, dataGridViewAramaSonuclari };
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — Kişi Arama"; ClientSize = new Size(1120, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Kişi Arama", "Genel ve Joker personel kayıtlarını birlikte arayın."), 0, 0);
        var clear = new Button { Name = "searchClear", Text = "Filtreleri &temizle" };
        clear.Click += (_, _) => ClearAction?.Invoke();
        var filters = new SearchFilterPanel([label5, label4, label2], [txtTC, txtAd, txtSoyad], Ara, clear);
        searchResults = new GroupBox { Name = "searchResults", Text = "Arama sonuçları — filtreleri girip Ara'ya basın", Padding = new Padding(8, 20, 8, 8), TabIndex = 1 };
        dataGridViewAramaSonuclari.Dock = DockStyle.Fill; dataGridViewAramaSonuclari.RowHeadersWidth = 28;
        dataGridViewAramaSonuclari.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dataGridViewAramaSonuclari.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 110), "AD" => ("Adı", 100), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 220),
                "PARA" => ("Kalan (TL)", 105), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 55), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption;
            e.Column.MinimumWidth = dataGridViewAramaSonuclari.LogicalToDeviceUnits(width);
            e.Column.FillWeight = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        // Keep the original bound rows and the existing column preferences;
        // resizing changes geometry, never the query, binding or selection.
        searchResults.Controls.Add(dataGridViewAramaSonuclari);
        var workspace = new SearchWorkspace(filters, searchResults) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toFilters = new Button { Name = "searchShowFilters", Text = "Arama &filtreleri", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toResults = new Button { Name = "searchShowResults", Text = "Arama &sonuçları", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 1 };
        toFilters.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toResults.Click += (_, _) => workspace.ScrollControlIntoView(searchResults);
        navigation.Controls.Add(toFilters); navigation.Controls.Add(toResults);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2);
        Controls.Add(outer); AcceptButton = Ara; ResumeLayout(true);
    }
}
