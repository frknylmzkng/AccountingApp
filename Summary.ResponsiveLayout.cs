namespace WinFormsApp2;

public partial class Summary
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class SummaryMetricCard : GroupBox
{
    internal Label Value { get; }
    private Font? fittedFont;
    private bool fitting;

    internal SummaryMetricCard(string title, Label value)
    {
        Value = value;
        SuspendLayout(); Text = title; TabStop = false; Padding = new Padding(12, 22, 12, 10);
        value.AutoSize = false; value.Dock = DockStyle.None; value.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        value.TextAlign = ContentAlignment.MiddleCenter;
        value.Font = AppTheme.TitleFont; value.Text = "—"; value.AutoEllipsis = true; value.UseMnemonic = false;
        value.AccessibleName = title;
        value.TextChanged += (_, _) => FitValue();
        value.FontChanged += (_, _) => FitValue();
        Controls.Add(value); ResumeLayout(true);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (!fitting && !Value.IsDisposed) FitValue();
    }

    private void FitValue()
    {
        var available = DisplayRectangle;
        if (fitting || Value.IsDisposed || available.Width <= 0 || available.Height <= 0) return;
        fitting = true;
        try
        {
            Value.AccessibleDescription = Value.Text;
            // Keep the actual amount intact; fit its type rather than shortening
            // the value or changing its numeric precision when the card narrows.
            float size = 14f;
            Font next;
            while (true)
            {
                next = new Font(Value.Font.FontFamily, size, FontStyle.Bold);
                var measured = TextRenderer.MeasureText(Value.Text, next, Size.Empty, TextFormatFlags.SingleLine);
                if ((measured.Width <= available.Width && measured.Height <= available.Height) || size <= 9f) break;
                next.Dispose(); size -= .5f;
            }
            if (Value.Font.SizeInPoints == next.SizeInPoints && Value.Font.Style == next.Style)
            {
                next.Dispose();
                if (fittedFont != null && !ReferenceEquals(Value.Font, fittedFont)) { fittedFont.Dispose(); fittedFont = null; }
            }
            else
            {
                var previous = fittedFont; fittedFont = next; Value.Font = next; previous?.Dispose();
            }
            // Label adjusts its requested bounds when its font changes. Set
            // explicit bounds after fitting instead of resizing a docked label
            // inside its SizeChanged event, which can restore its default size.
            Value.Bounds = available;
        }
        finally { fitting = false; }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { fittedFont?.Dispose(); fittedFont = null; }
    }
}

internal sealed class SummaryLeavePanel : GroupBox
{
    private readonly DataGridView grid;
    private readonly Label note = new()
    {
        Name = "summaryLeaveNote", Text = "Son 30 gün / bugün / gelecek 30 gün. Süresi biten izinler koyu kırmızı, diğer kayıtlar sarıdır.",
        AutoSize = false, TabStop = false
    };

    internal SummaryLeavePanel(DataGridView grid)
    {
        this.grid = grid;
        SuspendLayout(); Name = "summaryLeaves"; Text = "İzin takibi"; TabStop = false;
        grid.Anchor = AnchorStyles.Top | AnchorStyles.Left; grid.TabIndex = 0;
        Controls.Add(note); Controls.Add(grid); ResumeLayout(true);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(8), top = LogicalToDeviceUnits(24), gap = LogicalToDeviceUnits(8);
        int width = Math.Max(1, ClientSize.Width - inset * 2);
        int noteHeight = TextRenderer.MeasureText(note.Text, note.Font, new Size(width, 0), TextFormatFlags.WordBreak).Height;
        note.SetBounds(inset, top, width, noteHeight);
        int gridTop = top + noteHeight + gap;
        grid.SetBounds(inset, gridTop, width, Math.Max(1, ClientSize.Height - gridTop - inset));
    }
}

internal sealed class SummaryWorkspace : ScrollableControl
{
    private readonly SummaryMetricCard[] counts, amounts;
    private readonly SummaryLeavePanel leaves;
    private bool arranging;

    internal SummaryWorkspace(SummaryMetricCard[] counts, SummaryMetricCard[] amounts, SummaryLeavePanel leaves)
    {
        this.counts = counts; this.amounts = amounts; this.leaves = leaves;
        SuspendLayout(); Name = "summaryWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.AddRange(counts); Controls.AddRange(amounts); Controls.Add(leaves); ResumeLayout(true);
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
                int countHeight = LogicalToDeviceUnits(88), amountHeight = LogicalToDeviceUnits(96);
                int viewport = Width;
                var metrics = Measure(viewport, padding, gap, countHeight, amountHeight);
                // A previous scrollbar must not determine the new breakpoint.
                if (padding * 2 + metrics.SummaryHeight + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    metrics = Measure(viewport, padding, gap, countHeight, amountHeight);
                }
                var (width, countColumns, amountColumns, countsHeight, summaryHeight) = metrics;
                int left = (viewport - width) / 2, offset = AutoScrollPosition.Y;
                void Place(SummaryMetricCard[] cards, int columns, int top, int height)
                {
                    for (int i = 0; i < cards.Length; i++)
                    {
                        int column = i % columns;
                        int x = column * (width + gap) / columns;
                        int right = (column + 1) * (width + gap) / columns - gap;
                        cards[i].SetBounds(left + x, top + i / columns * (height + gap) + offset, right - x, height);
                    }
                }
                int leavesTop = padding + summaryHeight + gap;
                var extent = new Size(0, leavesTop + minimumListHeight + padding);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                offset = AutoScrollPosition.Y;
                Place(counts, countColumns, padding, countHeight);
                Place(amounts, amountColumns, padding + countsHeight + gap, amountHeight);
                leaves.SetBounds(left, leavesTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - leavesTop - padding));
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private (int Width, int CountColumns, int AmountColumns, int CountsHeight, int SummaryHeight)
        Measure(int viewport, int padding, int gap, int countHeight, int amountHeight)
    {
        int width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
        int countColumns = width >= LogicalToDeviceUnits(1050) ? 4 : 2;
        int amountColumns = width >= LogicalToDeviceUnits(760) ? 2 : 1;
        int countRows = (counts.Length + countColumns - 1) / countColumns;
        int amountRows = (amounts.Length + amountColumns - 1) / amountColumns;
        int countsHeight = countRows * countHeight + (countRows - 1) * gap;
        return (width, countColumns, amountColumns, countsHeight, countsHeight + gap + amountRows * amountHeight + (amountRows - 1) * gap);
    }
}
