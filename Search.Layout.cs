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
        Text = "Muhasebe — Kişi Arama"; ClientSize = new Size(1120, 580);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 144)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Kişi Arama", "TC, ad veya soyadla Genel ve Joker personel kayıtlarını arayın. Boş filtreler tüm kayıtları getirir."), 0, 0);
        var filters = new GroupBox { Text = "Arama filtreleri", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 8, 3, 8) };
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2 };
        foreach (float labelWidth in new[] { 96f, 44f, 64f })
        {
            entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
        }
        entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); entries.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Field(Label label, string caption, TextBox field, int column)
        {
            label.Text = caption; label.AutoSize = false; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.TabIndex = column;
            field.Dock = DockStyle.Fill; field.Margin = new Padding(3, 7, 12, 3); field.TabIndex = column + 1;
            entries.Controls.Add(label, column, 0); entries.Controls.Add(field, column + 1, 0);
        }
        Field(label5, "&TC kimlik no:", txtTC, 0); Field(label4, "&Adı:", txtAd, 2); Field(label2, "&Soyadı:", txtSoyad, 4);
        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty, TabIndex = 6 };
        Ara.Text = "&Ara"; Ara.Size = new Size(112, 30); Ara.Margin = new Padding(6, 3, 0, 3); Ara.TabIndex = 0;
        var clear = new Button { Text = "Filtreleri &temizle", Size = new Size(142, 30), Margin = new Padding(6, 3, 0, 3), TabIndex = 1 };
        clear.Click += (_, _) => ClearAction?.Invoke();
        commands.Controls.Add(Ara); commands.Controls.Add(clear); entries.Controls.Add(commands, 0, 1); entries.SetColumnSpan(commands, 6);
        filters.Controls.Add(entries); outer.Controls.Add(filters, 0, 1);
        searchResults = new GroupBox { Text = "Arama sonuçları — filtreleri girip Ara'ya basın", Dock = DockStyle.Fill, Padding = new Padding(8, 18, 8, 8) };
        dataGridViewAramaSonuclari.Dock = DockStyle.Fill; dataGridViewAramaSonuclari.RowHeadersWidth = 28;
        dataGridViewAramaSonuclari.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 96), "AD" => ("Adı", 105), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 215),
                "PARA" => ("Kalan (TL)", 100), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 70), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption; e.Column.Width = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        searchResults.Controls.Add(dataGridViewAramaSonuclari); outer.Controls.Add(searchResults, 0, 2);
        Controls.Add(outer); AcceptButton = Ara; ResumeLayout(true);
    }
}
