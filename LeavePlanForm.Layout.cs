namespace WinFormsApp2;

internal sealed partial class LeavePlanForm
{
    private void ArrangeLeavePlan(Button save)
    {
        Text = "Muhasebe — İzin Planlama"; ClientSize = new Size(1120, 610);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 160)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("İzin Planlama", "Genel personel listesinden kişileri seçin; aynı izin dönemini tek işlemde kaydedin."), 0, 0);
        var plan = new GroupBox { Text = "Seçili kişilerin izin dönemi", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 8, 3, 8) };
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 3 };
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); entries.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Date(string caption, DateTimePicker field, int column)
        {
            entries.Controls.Add(new Label { Text = caption, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, column, 0);
            field.Dock = DockStyle.Fill; field.Margin = new Padding(3, 6, 12, 3); field.TabIndex = column;
            entries.Controls.Add(field, column + 1, 0);
        }
        Date("Başlangıç:", start, 0); Date("Bitiş:", end, 2);
        var duration = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        entries.Controls.Add(duration, 4, 0);
        void UpdateDuration()
        {
            try { duration.Text = "Süre: " + Calculations.LeaveDays(start.Value.Date, end.Value.Date) + " gün"; }
            catch (ArgumentException) { duration.Text = "Tarih aralığını kontrol edin."; }
        }
        start.ValueChanged += (_, _) => UpdateDuration(); end.ValueChanged += (_, _) => UpdateDuration(); UpdateDuration();
        var selection = new Label { Name = "SelectedLeavePeople", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        void UpdateSelection()
        {
            int count = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem).OfType<System.Data.DataRowView>()
                .Where(v => v.Row.HasVersion(System.Data.DataRowVersion.Original)).Select(v => v.Row["TC", System.Data.DataRowVersion.Original]).Distinct().Count();
            selection.Text = "Seçili kişi: " + count;
        }
        grid.SelectionChanged += (_, _) => UpdateSelection(); UpdateSelection();
        entries.Controls.Add(selection, 0, 1); entries.SetColumnSpan(selection, 4);
        save.Dock = DockStyle.Fill; save.Margin = new Padding(3, 5, 3, 5); save.TabIndex = 4; entries.Controls.Add(save, 4, 1);
        var note = new Label { Text = "Ctrl / Shift ile birden fazla satır seçebilirsiniz. Aynı kişi bir kez işlenir; herhangi bir hata olursa planın tamamı geri alınır.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        entries.Controls.Add(note, 0, 2); entries.SetColumnSpan(note, 5); plan.Controls.Add(entries); outer.Controls.Add(plan, 0, 1);
        var list = new GroupBox { Text = "Genel personel — mevcut izin kayıtları", Dock = DockStyle.Fill, Padding = new Padding(8, 20, 8, 8) };
        grid.RowHeadersWidth = 28; grid.AutoGenerateColumns = false;
        // Preserve the bound DataRow and original TC used by the existing bulk-save action.
        foreach (var (name, caption, width) in new[]
        {
            ("TC", "TC kimlik no", 120), ("AD", "Adı", 160), ("SOYAD", "Soyadı", 160),
            ("IZINBASLANGIC", "Mevcut izin başlangıcı", 180), ("IZINBITIS", "Mevcut izin bitişi", 180), ("IzinSuresi", "İzin süresi (gün)", 130)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = width, Width = width });
        list.Controls.Add(grid); outer.Controls.Add(list, 0, 2); Controls.Add(outer);
        Load += (_, _) => MinimumSize = LogicalToDeviceUnits(new Size(900, 600));
        AppTheme.Apply(this);
    }
}
