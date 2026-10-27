namespace WinFormsApp2;

public partial class Genel
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class GeneralPersonnelWorkspace : ScrollableControl
{
    private readonly PersonnelEditor personnel;
    private readonly GeneralLeaveEditor leave;
    private readonly GroupBox records;
    private bool arranging;

    internal GeneralPersonnelWorkspace(PersonnelEditor personnel, GeneralLeaveEditor leave, GroupBox records)
    {
        this.personnel = personnel; this.leave = leave; this.records = records;
        SuspendLayout(); Name = "generalWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(personnel); Controls.Add(leave); Controls.Add(records);
        ResumeLayout(true);
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
                // Measure without a scrollbar first, rather than making the
                // previous scrollbar decide the next breakpoint. This avoids
                // getting stuck in a stacked/scrolling layout after growing.
                int viewport = Width;
                var metrics = Measure(viewport, padding, gap);
                if (padding * 2 + metrics.EditorHeight + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    metrics = Measure(viewport, padding, gap);
                }
                var (width, personnelWidth, leaveWidth, personnelHeight, leaveHeight, editorHeight, sideBySide) = metrics;
                int left = (viewport - width) / 2;
                int recordsTop = padding + editorHeight + gap;
                int contentHeight = recordsTop + minimumListHeight + padding;
                var extent = new Size(0, contentHeight);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y;
                personnel.SetBounds(left, padding + offset, personnelWidth, sideBySide ? editorHeight : personnelHeight);
                leave.SetBounds(sideBySide ? left + personnelWidth + gap : left,
                    padding + (sideBySide ? 0 : personnelHeight + gap) + offset, leaveWidth, leaveHeight);
                records.SetBounds(left, recordsTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - recordsTop - padding));
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private (int Width, int PersonnelWidth, int LeaveWidth, int PersonnelHeight, int LeaveHeight, int EditorHeight, bool SideBySide)
        Measure(int viewport, int padding, int gap)
    {
        int width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
        bool sideBySide = width >= LogicalToDeviceUnits(1080);
        int leaveWidth = sideBySide ? Math.Clamp(width / 3, LogicalToDeviceUnits(340), LogicalToDeviceUnits(440)) : width;
        int personnelWidth = sideBySide ? width - leaveWidth - gap : width;
        int personnelHeight = personnel.HeightForWidth(personnelWidth);
        int leaveHeight = leave.HeightForWidth(leaveWidth);
        int editorHeight = sideBySide ? Math.Max(personnelHeight, leaveHeight) : personnelHeight + gap + leaveHeight;
        return (width, personnelWidth, leaveWidth, personnelHeight, sideBySide ? editorHeight : leaveHeight, editorHeight, sideBySide);
    }
}

internal sealed class GeneralLeaveEditor : GroupBox
{
    private readonly Label startLabel, endLabel, status;
    private readonly DateTimePicker start, end;
    private readonly Button save;

    internal GeneralLeaveEditor(Label startLabel, Label endLabel, DateTimePicker start, DateTimePicker end, Button save, Label status)
    {
        this.startLabel = startLabel; this.endLabel = endLabel; this.start = start; this.end = end; this.save = save; this.status = status;
        SuspendLayout(); Text = "Seçili personelin izni"; TabStop = false;
        startLabel.Text = "Başlangıç:"; endLabel.Text = "Bitiş:"; save.Text = "İzni &kaydet";
        foreach (var control in new Control[] { startLabel, start, endLabel, end, save, status })
        {
            control.Dock = DockStyle.None; control.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            control.AutoSize = false; control.TabIndex = Controls.Count; Controls.Add(control);
        }
        startLabel.TextAlign = endLabel.TextAlign = ContentAlignment.MiddleLeft;
        save.TabStop = start.TabStop = end.TabStop = true;
        ResumeLayout(true);
    }

    internal int HeightForWidth(int width) => LogicalToDeviceUnits(width >= LogicalToDeviceUnits(620) ? 180 : 234);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), top = LogicalToDeviceUnits(26);
        bool paired = ClientSize.Width >= LogicalToDeviceUnits(620);
        int width = Math.Max(1, ClientSize.Width - inset * 2);
        int fieldWidth = paired ? (width - gap) / 2 : width;
        startLabel.SetBounds(inset, top, fieldWidth, LogicalToDeviceUnits(18));
        start.SetBounds(inset, top + LogicalToDeviceUnits(20), fieldWidth, start.PreferredSize.Height);
        int endLeft = paired ? inset + fieldWidth + gap : inset;
        int endTop = top + (paired ? 0 : LogicalToDeviceUnits(54));
        endLabel.SetBounds(endLeft, endTop, fieldWidth, LogicalToDeviceUnits(18));
        end.SetBounds(endLeft, endTop + LogicalToDeviceUnits(20), fieldWidth, end.PreferredSize.Height);
        int buttonTop = top + LogicalToDeviceUnits(paired ? 54 : 108) + LogicalToDeviceUnits(8);
        int buttonWidth = Math.Min(width, LogicalToDeviceUnits(200));
        save.SetBounds(ClientSize.Width - inset - buttonWidth, buttonTop, buttonWidth, LogicalToDeviceUnits(30));
        status.SetBounds(inset, buttonTop + LogicalToDeviceUnits(38), width, LogicalToDeviceUnits(42));
    }
}
