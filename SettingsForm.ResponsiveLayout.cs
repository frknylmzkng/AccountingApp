namespace WinFormsApp2;

internal sealed partial class SettingsForm
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed record SettingsPathField(string Caption, TextBox Input, Button Browse);

internal sealed class SettingsCard : GroupBox
{
    private readonly SettingsPathField[] fields;
    private readonly Label[] captions;
    private readonly Label? note;
    private readonly CheckBox[] choices;

    internal SettingsCard(string name, string title, SettingsPathField[] fields, Label? note, CheckBox[]? choices = null)
    {
        this.fields = fields; this.note = note; this.choices = choices ?? [];
        captions = fields.Select(f => new Label { Text = f.Caption, AutoSize = false, TabStop = false }).ToArray();
        SuspendLayout(); Name = name; Text = title; TabStop = false;
        for (int i = 0; i < fields.Length; i++)
        {
            var field = fields[i]; field.Input.TabIndex = i * 2; field.Browse.TabIndex = i * 2 + 1;
            field.Input.AccessibleName = field.Caption; field.Input.AutoSize = true; field.Browse.AutoSize = false;
            Controls.Add(captions[i]); Controls.Add(field.Input); Controls.Add(field.Browse);
        }
        for (int i = 0; i < this.choices.Length; i++)
        { this.choices[i].AutoSize = false; this.choices[i].TabIndex = fields.Length * 2 + i; Controls.Add(this.choices[i]); }
        if (note != null)
        {
            note.AutoSize = false; note.MaximumSize = Size.Empty; note.Padding = Padding.Empty; note.TextAlign = ContentAlignment.TopLeft;
            // A neighbouring card may keep this card at the same outer size.
            // Remeasure the label itself even when the workspace needs no resize.
            void Remeasure() { PerformLayout(); Parent?.PerformLayout(); }
            note.TextChanged += (_, _) => Remeasure(); note.FontChanged += (_, _) => Remeasure(); Controls.Add(note);
        }
        foreach (Control control in Controls) { control.Dock = DockStyle.None; control.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
        ResumeLayout(true);
    }

    private int RowHeight(int width) => LogicalToDeviceUnits(width >= LogicalToDeviceUnits(680) ? 42 : 64);
    private int ChoiceColumns(int width) => width >= LogicalToDeviceUnits(560) ? 2 : 1;
    private int NoteHeight(int width) => note == null ? 0 : Math.Max(LogicalToDeviceUnits(36),
        TextRenderer.MeasureText(note.Text, note.Font, new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(38) + fields.Length * RowHeight(width)
        + ((choices.Length + ChoiceColumns(width) - 1) / ChoiceColumns(width)) * LogicalToDeviceUnits(34) + NoteHeight(width);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(10), width = Math.Max(1, ClientSize.Width - inset * 2);
        int top = LogicalToDeviceUnits(26), browseWidth = LogicalToDeviceUnits(80), rowHeight = RowHeight(ClientSize.Width);
        bool inline = ClientSize.Width >= LogicalToDeviceUnits(680);
        for (int i = 0; i < fields.Length; i++)
        {
            int captionWidth = inline ? LogicalToDeviceUnits(152) : width;
            captions[i].SetBounds(inset, top, captionWidth, LogicalToDeviceUnits(20));
            int left = inline ? inset + captionWidth + gap : inset, fieldTop = top + LogicalToDeviceUnits(inline ? 0 : 22);
            fields[i].Input.SetBounds(left, fieldTop + LogicalToDeviceUnits(4), width - (left - inset) - browseWidth - gap, fields[i].Input.PreferredHeight);
            fields[i].Browse.SetBounds(inset + width - browseWidth, fieldTop, browseWidth, LogicalToDeviceUnits(30));
            top += rowHeight;
        }
        int columns = ChoiceColumns(ClientSize.Width);
        for (int i = 0; i < choices.Length; i++)
        {
            int column = i % columns, left = column * (width + gap) / columns;
            int right = (column + 1) * (width + gap) / columns - gap;
            choices[i].SetBounds(inset + left, top + i / columns * LogicalToDeviceUnits(34), right - left, LogicalToDeviceUnits(30));
        }
        top += ((choices.Length + columns - 1) / columns) * LogicalToDeviceUnits(34);
        note?.SetBounds(inset, top, width, NoteHeight(ClientSize.Width));
    }
}

internal sealed class SettingsActions : GroupBox
{
    private readonly Button[] actions;
    internal SettingsActions(Button[] actions)
    {
        this.actions = actions; SuspendLayout(); Name = "settingsActions"; Text = "İşlemler"; TabStop = false;
        for (int i = 0; i < actions.Length; i++)
        { actions[i].Dock = DockStyle.None; actions[i].Anchor = AnchorStyles.Top | AnchorStyles.Left; actions[i].AutoSize = false; actions[i].TabIndex = i; Controls.Add(actions[i]); }
        ResumeLayout(true);
    }
    private int Columns(int width) => width >= LogicalToDeviceUnits(960) ? 4 : 2;
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(38 + ((actions.Length + Columns(width) - 1) / Columns(width)) * 42);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), width = Math.Max(1, ClientSize.Width - inset * 2), columns = Columns(ClientSize.Width);
        for (int i = 0; i < actions.Length; i++)
        {
            int column = i % columns, left = column * (width + gap) / columns, right = (column + 1) * (width + gap) / columns - gap;
            actions[i].SetBounds(inset + left, LogicalToDeviceUnits(26 + i / columns * 42), right - left, LogicalToDeviceUnits(30));
        }
    }
}

internal sealed class SettingsWorkspace : ScrollableControl
{
    private readonly SettingsCard[] cards;
    private readonly SettingsActions actions;
    private bool arranging;
    internal SettingsWorkspace(SettingsCard[] cards, SettingsActions actions)
    {
        this.cards = cards; this.actions = actions;
        SuspendLayout(); Name = "settingsWorkspace"; AutoScroll = true; DoubleBuffered = true;
        for (int i = 0; i < cards.Length; i++) { cards[i].TabIndex = i; Controls.Add(cards[i]); }
        actions.TabIndex = cards.Length; Controls.Add(actions); ResumeLayout(true);
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
                int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14), viewport = Width;
                var metrics = Measure(viewport, padding, gap);
                if (metrics.Height > Height) { viewport -= SystemInformation.VerticalScrollBarWidth; metrics = Measure(viewport, padding, gap); }
                var (width, columns, heights, height) = metrics;
                var extent = new Size(0, height); if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int left = (viewport - width) / 2, top = padding + AutoScrollPosition.Y;
                for (int i = 0; i < cards.Length; i++)
                {
                    int column = i % columns, x = column * (width + gap) / columns, right = (column + 1) * (width + gap) / columns - gap;
                    cards[i].SetBounds(left + x, top, right - x, heights[i / columns]);
                    if (column == columns - 1 || i == cards.Length - 1) top += heights[i / columns] + gap;
                }
                actions.SetBounds(left, top, width, actions.HeightForWidth(width));
                base.OnLayout(e); if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }
    private (int Width, int Columns, int[] Heights, int Height) Measure(int viewport, int padding, int gap)
    {
        int width = Math.Min(LogicalToDeviceUnits(1560), Math.Max(1, viewport - padding * 2)), columns = width >= LogicalToDeviceUnits(1000) ? 2 : 1;
        var heights = new int[(cards.Length + columns - 1) / columns];
        for (int i = 0; i < cards.Length; i++)
        {
            int column = i % columns, x = column * (width + gap) / columns, right = (column + 1) * (width + gap) / columns - gap;
            heights[i / columns] = Math.Max(heights[i / columns], cards[i].HeightForWidth(right - x));
        }
        return (width, columns, heights, padding * 2 + heights.Sum() + heights.Length * gap + actions.HeightForWidth(width));
    }
}
