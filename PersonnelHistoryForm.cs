using System.Data;

namespace WinFormsApp2;

internal sealed class PersonnelHistoryForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly PersonnelGroup group;
    private readonly DataGridView cards = Grid("HistoryCards"), payments = Grid("HistoryPayments"), leaves = Grid("HistoryLeaves"), events = Grid("HistoryEvents"), leaveChanges = Grid("HistoryLeaveChanges");
    private readonly CheckBox active = new() { Text = "Etkin kayıtları da göster", AutoSize = true };
    private readonly Button restore = new() { Text = "Arşivlemeyi geri al (aynı dönem)", AutoSize = true, Enabled = false };
    private readonly Label details = new() { Dock = DockStyle.Top, Height = 76, Padding = new(8), AutoEllipsis = true };
    private bool binding;
    private readonly ListPager pager;
    private static DataGridView Grid(string name)
    {
        var grid = new DataGridView { Name = name, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        RecordSelection.Configure(grid); return grid;
    }
    internal PersonnelHistoryForm(PersonnelGroup group, UserSession session)
    {
        this.group = group; Session = session; PersistGridLayout = false;
        Text = "Muhasebe — Personel arşivi ve geçmişi"; ClientSize = new(1000, 720);
        var reload = new Button { Text = "Yenile", AutoSize = true };
        var close = new Button { Text = "Kapat", AutoSize = true }; close.Click += (_, _) => { if (!IsOperationRunning) Close(); };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(8), WrapContents = true };
        var employment = new Button { Text = "Çalışma dönemleri…", AutoSize = true };
        employment.Click += async (_, _) => await OpenEmploymentAsync();
        var card = new Button { Text = "Seçili personel kartı…", AutoSize = true };
        card.Click += async (_,_) => await PersonnelCardUi.OpenAsync(this,cards,group);
        toolbar.Controls.AddRange([active, reload, restore, employment, card, close]);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (var (caption, grid) in new[] { ("Ödeme geçmişi", payments), ("Tüm dönemlerin izinleri", leaves), ("İzin değişiklikleri", leaveChanges), ("Arşiv / etkinleştirme", events) })
        { var tab = new TabPage(caption); tab.Controls.Add(grid); tabs.TabPages.Add(tab); }
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new(SizeType.Percent, 45)); layout.RowStyles.Add(new(SizeType.Percent, 55));
        layout.Controls.Add(cards, 0, 0); layout.Controls.Add(tabs, 0, 1);
        Controls.Add(layout); Controls.Add(details); Controls.Add(toolbar);
        pager = ListPager.Attach(cards, this, ReloadAsync);
        EditControls.Add(restore); RefreshAction = ReloadAsync;
        active.CheckedChanged += async (_, _) => { pager.Reset(); await ReloadAsync(); }; reload.Click += async (_, _) => await ReloadAsync();
        cards.SelectionChanged += async (_, _) => { if (!binding) await ShowHistoryAsync(); };
        restore.Click += Restore; Load += async (_, _) => await ReloadAsync();
        PageRequested += (_, _) => Close(); SessionRevoked += (_, _) => Close();
        void FitDetails() => details.Height = TextRenderer.MeasureText(details.Text, details.Font,
            new Size(Math.Max(1, ClientSize.Width - details.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak).Height + details.Padding.Vertical;
        ClientSizeChanged += (_, _) => FitDetails(); details.TextChanged += (_, _) => FitDetails();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(620, 480)); }
    private DataRow? Selected => cards.SelectedRows.Count == 1 && cards.SelectedRows[0].DataBoundItem is DataRowView row ? row.Row : null;
    private async Task ReloadAsync() => await RunOperationAsync(async token =>
    {
        var page = await People.PageAsync(group, pager.Index, pager.SizeLimit, token, includeArchived: true, archivedOnly: !active.Checked, includeLeaves: false);
        var data = page.Rows;
        if (IsDisposed || token.IsCancellationRequested) return;
        var view = data.Clone(); view.Columns.Add("DURUM"); view.Columns.Add("ARSIV_TARIHI", typeof(DateTime)); view.Columns.Add("ARSIVLEYEN");
        foreach (var row in data.AsEnumerable().GroupBy(r => r.Field<Guid>("PersonId")).Select(g => g.First()).Where(r => active.Checked || !r.IsNull("ArchivedUtc")))
        {
            view.ImportRow(row); var added = view.Rows[view.Rows.Count - 1];
            added["DURUM"] = row.IsNull("ArchivedUtc") ? "Etkin" : "Arşivde";
            added["ARSIV_TARIHI"] = row["ArchivedUtc"]; added["ARSIVLEYEN"] = row["ArchivedBy"];
        }
        view.AcceptChanges(); binding = true;
        try { cards.DataSource = view; cards.ClearSelection(); cards.CurrentCell = null; }
        finally { binding = false; }
        pager.Apply(page);
        payments.DataSource = leaves.DataSource = events.DataSource = leaveChanges.DataSource = null;
        details.Text = $"{view.Rows.Count} personel kartı. Bir kart seçin. Kimliği belirlenemeyen eski ödeme hareketleri genel Ödemeler defterinde korunur.";
    });
    private async Task ShowHistoryAsync()
    {
        var row = Selected; if (row == null) { restore.Enabled = false; return; }
        Guid id = row.Field<Guid>("PersonId", DataRowVersion.Original);
        await RunOperationAsync(async token =>
        {
            var history = await People.HistoryAsync(group, id, token);
            if (IsDisposed || token.IsCancellationRequested) return;
            payments.DataSource = history.Payments; leaves.DataSource = history.Leaves; events.DataSource = history.Events; leaveChanges.DataSource = history.LeaveChanges;
            decimal balance = row["PARA"] is DBNull ? 0 : Convert.ToDecimal(row["PARA"]);
            details.Text = $"Personel kimliği: {id:D}\n{row["AD"]} {row["SOYAD"]} · {row["DURUM"]} · Kalan: {(User.CanViewSalary ? $"{balance:N2} TL" : "Yetki yok")}\nArşivleme borcu/ödemeyi silmez. Arşivlemeyi geri alma aynı dönemi açar. Yeni işe giriş için Çalışma dönemleri düğmesini kullanın.";
        });
    }
    protected override void RefreshEditState()
    {
        pager?.UpdateState();
        restore.Enabled = Session?.CanEdit == true && !IsOperationRunning && Selected is { } row && !row.IsNull("ArchivedUtc");
    }
    private async void Restore(object? sender, EventArgs e)
    {
        if (Selected is not { } row || row.IsNull("ArchivedUtc")) return;
        Guid id = row.Field<Guid>("PersonId", DataRowVersion.Original);
        byte[] version = ((byte[])row[PersonnelConcurrency.ColumnName, DataRowVersion.Original]).ToArray();
        if (MessageBox.Show(this, "Arşivleme aynı çalışma döneminde geri alınacak; izinler ve bakiye korunacak. Yeniden işe giriş için Çalışma dönemleri düğmesini kullanın. Devam edilsin mi?", "Personeli etkinleştir", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (await RunChangeOperationAsync(token => People.RestoreArchivedAsync(group, id, version, token), "Personel tekrar etkinleştirildi.")) await ReloadAsync();
    }
    private async Task OpenEmploymentAsync()
    {
        if (IsOperationRunning || Selected is not { } row) return;
        Guid id = row.Field<Guid>("PersonId", DataRowVersion.Original);
        byte[] version = ((byte[])row[PersonnelConcurrency.ColumnName, DataRowVersion.Original]).ToArray();
        using var dialog = new EmploymentForm(group, id, version, !row.IsNull("ArchivedUtc"), User);
        dialog.SessionRevoked += (_, _) => NotifySessionRevoked();
        dialog.ShowDialog(this);
        if (!IsDisposed) await ReloadAsync();
    }

}
