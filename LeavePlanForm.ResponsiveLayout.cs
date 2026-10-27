namespace WinFormsApp2;

internal sealed partial class LeavePlanForm
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class LeavePlanEditor : GroupBox
{
    private readonly DateTimePicker start, end;
    private readonly Label duration, selection;
    private readonly Button save;
    private readonly Label startCaption = new() { Text = "Başlangıç:", AutoSize = false, TabStop = false };
    private readonly Label endCaption = new() { Text = "Bitiş:", AutoSize = false, TabStop = false };
    private readonly Label note = new()
    {
        Name = "leavePlanNote", AutoSize = false, TabStop = false,
        Text = "Ctrl / Shift ile birden fazla satır seçebilirsiniz. Aynı kişi bir kez işlenir; herhangi bir hata olursa planın tamamı geri alınır."
    };

    internal LeavePlanEditor(DateTimePicker start, DateTimePicker end, Label duration, Label selection, Button save)
    {
        this.start = start; this.end = end; this.duration = duration; this.selection = selection; this.save = save;
        SuspendLayout(); Name = "leavePlanEditor"; Text = "Seçili kişilerin izin dönemi"; TabStop = false; TabIndex = 0;
        start.AccessibleName = "İzin başlangıç tarihi"; end.AccessibleName = "İzin bitiş tarihi";
        start.TabIndex = 0; end.TabIndex = 1; save.TabIndex = 2; save.AutoSize = false;
        foreach (var label in new[] { duration, selection }) { label.AutoSize = false; label.TextAlign = ContentAlignment.MiddleLeft; label.TabStop = false; }
        Controls.AddRange([startCaption, start, endCaption, end, duration, selection, save, note]);
        foreach (Control control in Controls) { control.Dock = DockStyle.None; control.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
        note.FontChanged += (_, _) => { PerformLayout(); Parent?.PerformLayout(); };
        ResumeLayout(true);
    }

    private int Columns(int width) => width >= LogicalToDeviceUnits(900) ? 3 : width >= LogicalToDeviceUnits(620) ? 2 : 1;
    private int NoteHeight(int width) => Math.Max(LogicalToDeviceUnits(28),
        TextRenderer.MeasureText(note.Text, note.Font, new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width)
    {
        int columns = Columns(width);
        return LogicalToDeviceUnits(26 + (columns == 1 ? 108 : 54) + (columns == 3 ? 0 : 28) + 42 + 12) + NoteHeight(width);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), width = Math.Max(1, ClientSize.Width - inset * 2), columns = Columns(ClientSize.Width);
        int top = LogicalToDeviceUnits(26), fieldWidth = columns == 1 ? width : (width - (columns - 1) * gap) / columns;
        void Date(Label caption, DateTimePicker field, int index)
        {
            int x = inset + (columns == 1 ? 0 : index * (fieldWidth + gap)), y = top + (columns == 1 ? index * LogicalToDeviceUnits(54) : 0);
            caption.SetBounds(x, y, fieldWidth, LogicalToDeviceUnits(18));
            field.SetBounds(x, y + LogicalToDeviceUnits(20), fieldWidth, field.PreferredSize.Height);
        }
        Date(startCaption, start, 0); Date(endCaption, end, 1);
        int selectionTop = top + LogicalToDeviceUnits(columns == 1 ? 108 : 54);
        if (columns == 3) duration.SetBounds(inset + 2 * (fieldWidth + gap), top + LogicalToDeviceUnits(18), width - 2 * (fieldWidth + gap), LogicalToDeviceUnits(30));
        else { duration.SetBounds(inset, selectionTop, width, LogicalToDeviceUnits(28)); selectionTop += LogicalToDeviceUnits(28); }
        int buttonWidth = LogicalToDeviceUnits(210);
        selection.SetBounds(inset, selectionTop, width - buttonWidth - gap, LogicalToDeviceUnits(30));
        save.SetBounds(inset + width - buttonWidth, selectionTop, buttonWidth, LogicalToDeviceUnits(30));
        note.SetBounds(inset, selectionTop + LogicalToDeviceUnits(42), width, NoteHeight(ClientSize.Width));
    }
}

internal sealed class LeavePlanWorkspace : ScrollableControl
{
    private readonly LeavePlanEditor editor;
    private readonly GroupBox records;
    private bool arranging;
    internal LeavePlanWorkspace(LeavePlanEditor editor, GroupBox records)
    {
        this.editor = editor; this.records = records;
        SuspendLayout(); Name = "leavePlanWorkspace"; AutoScroll = true; DoubleBuffered = true;
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
                int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14), minimumListHeight = LogicalToDeviceUnits(200), viewport = Width;
                int width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2)), editorHeight = editor.HeightForWidth(width);
                if (padding * 2 + editorHeight + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2)); editorHeight = editor.HeightForWidth(width);
                }
                int left = (viewport - width) / 2, recordsTop = padding + editorHeight + gap;
                var extent = new Size(0, recordsTop + minimumListHeight + padding);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y;
                editor.SetBounds(left, padding + offset, width, editorHeight);
                records.SetBounds(left, recordsTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - recordsTop - padding));
                base.OnLayout(e); if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }
}
