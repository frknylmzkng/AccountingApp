namespace WinFormsApp2;

public partial class Joker
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class JokerPersonnelWorkspace : ScrollableControl
{
    private readonly PersonnelEditor editor;
    private readonly GroupBox records;
    private bool arranging;

    internal JokerPersonnelWorkspace(PersonnelEditor editor, GroupBox records)
    {
        this.editor = editor; this.records = records;
        SuspendLayout(); Name = "jokerWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(editor); Controls.Add(records); ResumeLayout(true);
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
                int viewport = Width;
                int width = ContentWidth(viewport, padding);
                // Decide whether scrolling is needed from the full available
                // width, so a previous narrow layout cannot keep its scrollbar.
                if (padding * 2 + editor.HeightForWidth(width) + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    width = ContentWidth(viewport, padding);
                }
                int left = (viewport - width) / 2, editorHeight = editor.HeightForWidth(width);
                int recordsTop = padding + editorHeight + gap;
                var extent = new Size(0, recordsTop + minimumListHeight + padding);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y;
                editor.SetBounds(left, padding + offset, width, editorHeight);
                records.SetBounds(left, recordsTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - recordsTop - padding));
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private int ContentWidth(int viewport, int padding) => Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
}
