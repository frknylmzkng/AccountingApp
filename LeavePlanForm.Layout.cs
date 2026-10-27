namespace WinFormsApp2;

internal sealed partial class LeavePlanForm
{
    private void ArrangeLeavePlan(Button save)
    {
        Text = "Muhasebe — İzin Planlama"; ClientSize = new Size(1120, 680);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("İzin Planlama", "Genel personeli seçin; ortak izin dönemini planlayın."), 0, 0);
        start.Name = "leavePlanStart"; end.Name = "leavePlanEnd"; save.Name = "leavePlanSave";
        var duration = new Label { Name = "leavePlanDuration" };
        void UpdateDuration()
        {
            try { duration.Text = "Süre: " + Calculations.LeaveDays(start.Value.Date, end.Value.Date) + " gün"; }
            catch (ArgumentException) { duration.Text = "Tarih aralığını kontrol edin."; }
        }
        start.ValueChanged += (_, _) => UpdateDuration(); end.ValueChanged += (_, _) => UpdateDuration(); UpdateDuration();
        var selection = new Label { Name = "SelectedLeavePeople" };
        void UpdateSelection()
        {
            int count = grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.DataBoundItem).OfType<System.Data.DataRowView>()
                .Where(v => v.Row.HasVersion(System.Data.DataRowVersion.Original)).Select(v => v.Row["TC", System.Data.DataRowVersion.Original]).Distinct().Count();
            selection.Text = "Seçili kişi: " + count;
        }
        grid.SelectionChanged += (_, _) => UpdateSelection(); UpdateSelection();
        var plan = new LeavePlanEditor(start, end, duration, selection, save);
        var list = new GroupBox { Name = "leavePlanRecords", Text = "Genel personel — mevcut izin kayıtları", Padding = new Padding(8, 20, 8, 8), TabIndex = 1 };
        grid.RowHeadersWidth = 28; grid.AutoGenerateColumns = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        // Layout keeps the bound DataRows and original person/leave versions used by bulk save.
        foreach (var (name, caption, width) in new[]
        {
            ("TC", "TC kimlik no", 110), ("AD", "Adı", 100), ("SOYAD", "Soyadı", 110),
            ("IZINBASLANGIC", "Mevcut izin başlangıcı", 175), ("IZINBITIS", "Mevcut izin bitişi", 175), ("IzinSuresi", "İzin süresi (gün)", 120)
        })
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = LogicalToDeviceUnits(width), FillWeight = width });
        grid.Dock = DockStyle.Fill; list.Controls.Add(grid);
        var workspace = new LeavePlanWorkspace(plan, list) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toPlan = new Button { Name = "leavePlanShowEditor", Text = "İzin dönemi", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toList = new Button { Name = "leavePlanShowList", Text = "Personel listesi", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 1 };
        toPlan.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toList.Click += (_, _) => workspace.ScrollControlIntoView(list);
        navigation.Controls.Add(toPlan); navigation.Controls.Add(toList);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2); Controls.Add(outer);
        AppTheme.Apply(this);
    }
}
