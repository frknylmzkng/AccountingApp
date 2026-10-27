namespace WinFormsApp2;

public partial class Search
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class SearchFilterPanel : GroupBox
{
    private readonly Label[] labels;
    private readonly TextBox[] fields;
    private readonly Button search, clear;
    private readonly Label hint = new() { Text = "Boş filtreler tüm kayıtları getirir.", AutoSize = false, TabStop = false };

    internal SearchFilterPanel(Label[] labels, TextBox[] fields, Button search, Button clear)
    {
        this.labels = labels; this.fields = fields; this.search = search; this.clear = clear;
        SuspendLayout(); Name = "searchFilters"; Text = "Arama filtreleri"; TabIndex = 0; TabStop = false;
        string[] captions = ["TC kimlik no:", "Adı:", "Soyadı:"];
        for (int i = 0; i < fields.Length; i++)
        {
            labels[i].Text = captions[i]; labels[i].AutoSize = false;
            labels[i].Dock = fields[i].Dock = DockStyle.None;
            labels[i].Anchor = fields[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            labels[i].TextAlign = ContentAlignment.MiddleLeft;
            labels[i].TabIndex = i * 2; fields[i].TabIndex = i * 2 + 1;
            fields[i].AutoSize = true; fields[i].TabStop = true;
            Controls.Add(labels[i]); Controls.Add(fields[i]);
        }
        search.Text = "&Ara";
        foreach (var button in new[] { search, clear })
        {
            button.Dock = DockStyle.None; button.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            button.AutoSize = false; button.TabStop = true; Controls.Add(button);
        }
        search.TabIndex = 6; clear.TabIndex = 7; Controls.Add(hint); ResumeLayout(true);
    }

    private int Columns(int width) => width >= LogicalToDeviceUnits(900) ? 3 : width >= LogicalToDeviceUnits(620) ? 2 : 1;
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(98 + (Columns(width) == 3 ? 1 : Columns(width) == 2 ? 2 : 3) * 54);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int columns = Columns(ClientSize.Width), inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14);
        int available = Math.Max(1, ClientSize.Width - inset * 2), top = LogicalToDeviceUnits(26), rowHeight = LogicalToDeviceUnits(54);
        for (int i = 0; i < fields.Length; i++)
        {
            int column = i % columns, row = i / columns;
            int span = columns == 2 && i == 2 ? 2 : 1;
            int left = column * (available + gap) / columns;
            int right = (column + span) * (available + gap) / columns - gap;
            labels[i].SetBounds(inset + left, top + row * rowHeight, right - left, LogicalToDeviceUnits(18));
            fields[i].SetBounds(inset + left, top + row * rowHeight + LogicalToDeviceUnits(20), right - left, fields[i].PreferredHeight);
        }
        int rows = (fields.Length + columns - 1) / columns;
        int commandTop = top + rows * rowHeight + LogicalToDeviceUnits(8);
        int searchWidth = LogicalToDeviceUnits(112), clearWidth = LogicalToDeviceUnits(142);
        search.SetBounds(ClientSize.Width - inset - searchWidth, commandTop, searchWidth, LogicalToDeviceUnits(30));
        clear.SetBounds(search.Left - gap - clearWidth, commandTop, clearWidth, LogicalToDeviceUnits(30));
        hint.SetBounds(inset, commandTop + LogicalToDeviceUnits(36), available, LogicalToDeviceUnits(20));
    }
}

internal sealed class SearchWorkspace : ScrollableControl
{
    private readonly SearchFilterPanel filters;
    private readonly GroupBox results;
    private bool arranging;

    internal SearchWorkspace(SearchFilterPanel filters, GroupBox results)
    {
        this.filters = filters; this.results = results;
        SuspendLayout(); Name = "searchWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(filters); Controls.Add(results); ResumeLayout(true);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging) return;
        arranging = true;
        try
        {
            base.OnLayout(e);
            for (int pass = 0; pass < 4; pass++)
            {
                int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14), minimumListHeight = LogicalToDeviceUnits(240);
                int viewport = Width, width = ContentWidth(viewport, padding);
                // Measure without the old scrollbar first to make growing and
                // shrinking choose the same layout at each available size.
                if (padding * 2 + filters.HeightForWidth(width) + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    width = ContentWidth(viewport, padding);
                }
                int left = (viewport - width) / 2, filtersHeight = filters.HeightForWidth(width);
                int resultsTop = padding + filtersHeight + gap;
                var extent = new Size(0, resultsTop + minimumListHeight + padding);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y;
                filters.SetBounds(left, padding + offset, width, filtersHeight);
                results.SetBounds(left, resultsTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - resultsTop - padding));
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private int ContentWidth(int viewport, int padding) => Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
}
