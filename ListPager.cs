namespace WinFormsApp2;

internal sealed class ListPager : FlowLayoutPanel
{
    private readonly SessionForm owner;
    private readonly Func<Task> reload;
    private readonly Button previous = new() { Text = "Önceki", AutoSize = true }, next = new() { Text = "Sonraki", AutoSize = true }, go = new() { Text = "Git", AutoSize = true };
    private readonly Label info = new() { AutoSize = true, Margin = new(6, 8, 6, 0) };
    private readonly NumericUpDown page = new() { Minimum = 1, Maximum = long.MaxValue, Width = 85 };
    private readonly ComboBox sizes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 65 };
    private long appliedIndex, total;
    private int appliedSize = 50;
    private bool changing;
    internal long Index { get; private set; }
    internal int SizeLimit { get; private set; } = 50;
    private readonly string unit;
    internal ListPager(SessionForm owner, Func<Task> reload, string unit = "personel")
    {
        this.owner = owner; this.reload = reload; this.unit = unit;
        AutoSize = true; Dock = DockStyle.Fill; WrapContents = true; Margin = Padding.Empty; Padding = new(4);
        AccessibleName = "Liste sayfalama";
        previous.AccessibleName = "Önceki sayfa";
        next.AccessibleName = "Sonraki sayfa";
        page.AccessibleName = "Sayfa numarası";
        go.AccessibleName = "Girilen sayfaya git";
        sizes.AccessibleName = "Sayfa başına " + unit;
        sizes.Items.AddRange([25, 50, 100, 200]); sizes.SelectedItem = 50;
        Controls.AddRange([previous, next, info, page, go, sizes]);
        previous.Click += async (_, _) => await NavigateAsync(Math.Max(0, appliedIndex - 1), appliedSize);
        next.Click += async (_, _) => await NavigateAsync(appliedIndex + 1, appliedSize);
        go.Click += async (_, _) => await NavigateAsync((long)page.Value - 1, appliedSize);
        sizes.SelectedIndexChanged += async (_, _) => { if (!changing && sizes.SelectedItem is int size) await NavigateAsync(0, size); };
        UpdateState();
    }
    internal void Reset() { Index = 0; }
    internal void Apply(PersonnelPage result)
    { Index = appliedIndex = result.Index; SizeLimit = appliedSize = result.Size; total = result.Total; UpdateState(); }
    private async Task NavigateAsync(long index, int size)
    {
        if (owner.IsOperationRunning || owner.IsDisposed) { UpdateState(); return; }
        if (SessionUiState.IsLocked || !owner.ConfirmDiscardChanges("Liste sayfasını değiştirme")) { UpdateState(); return; }
        Index = index; SizeLimit = size;
        try { await reload(); }
        finally { if (!IsDisposed) { Index = appliedIndex; SizeLimit = appliedSize; UpdateState(); } }
    }
    internal void UpdateState()
    {
        if (IsDisposed) return;
        changing = true;
        try
        {
            long pages = total == 0 ? 1 : (total - 1) / appliedSize + 1;
            info.Text = $"Sayfa {appliedIndex + 1} / {pages} · Toplam {total:N0} {unit}";
            page.Maximum = pages; page.Value = Math.Min(pages, appliedIndex + 1); sizes.SelectedItem = appliedSize;
            bool ready = !owner.IsOperationRunning;
            previous.Enabled = ready && appliedIndex > 0; next.Enabled = ready && appliedIndex + 1 < pages;
            go.Enabled = sizes.Enabled = page.Enabled = ready;
        }
        finally { changing = false; }
    }
    internal static ListPager Attach(DataGridView grid, SessionForm owner, Func<Task> reload, string unit = "personel")
    {
        var parent = grid.Parent ?? throw new InvalidOperationException("Liste yerleşimi kurulmamış.");
        var pager = new ListPager(owner, reload, unit);
        var host = new TableLayoutPanel { Dock = grid.Dock, Anchor = grid.Anchor, Location = grid.Location, Size = grid.Size, Margin = grid.Margin, TabIndex = grid.TabIndex, ColumnCount = 1, RowCount = 2 };
        host.ColumnStyles.Add(new(SizeType.Percent, 100));
        host.RowStyles.Add(new(SizeType.Percent, 100)); host.RowStyles.Add(new(SizeType.AutoSize));
        int index = parent.Controls.GetChildIndex(grid);
        if (parent is TableLayoutPanel table)
        {
            var position = table.GetPositionFromControl(grid); int columns = table.GetColumnSpan(grid), rows = table.GetRowSpan(grid);
            table.Controls.Remove(grid); table.Controls.Add(host, position.Column, position.Row); table.SetColumnSpan(host, columns); table.SetRowSpan(host, rows);
        }
        else { parent.Controls.Remove(grid); parent.Controls.Add(host); parent.Controls.SetChildIndex(host, index); }
        grid.Dock = DockStyle.Fill;
        grid.TabIndex = 0;
        pager.TabIndex = 1;
        host.Controls.Add(grid, 0, 0);
        host.Controls.Add(pager, 0, 1);
        grid.AccessibleDescription ??= "Sayfalı sonuçlar. Sayfa seçimi tablonun altındadır.";
        grid.Tag = "AccountingApp.Paged";
        return pager;
    }
}
