namespace WinFormsApp2;

public partial class Admin
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class AdminCreateCard : GroupBox
{
    private readonly Control[] fields;
    private readonly Label[] labels;
    private readonly Button add;

    internal AdminCreateCard(TextBox username, TextBox password, ComboBox role, Button add)
    {
        fields = [username, password, role]; this.add = add;
        labels = [new() { Text = "Kullanıcı adı:" }, new() { Text = "Parola:" }, new() { Text = "Yetki:" }];
        SuspendLayout(); Name = "adminCreate"; Text = "Yeni hesap"; TabStop = false; TabIndex = 0;
        for (int i = 0; i < fields.Length; i++)
        {
            fields[i].Dock = DockStyle.None; fields[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            fields[i].AutoSize = true; fields[i].TabIndex = i * 2 + 1; fields[i].TabStop = true;
            labels[i].AutoSize = false; labels[i].TextAlign = ContentAlignment.MiddleLeft; labels[i].TabIndex = i * 2;
            Controls.Add(labels[i]); Controls.Add(fields[i]);
        }
        add.Text = "Hesap &ekle"; add.AutoSize = false; add.Dock = DockStyle.None;
        add.Anchor = AnchorStyles.Top | AnchorStyles.Left; add.TabIndex = 6; add.TabStop = true;
        Controls.Add(add); ResumeLayout(true);
    }

    private int Columns(int width) => width >= LogicalToDeviceUnits(900) ? 3 : width >= LogicalToDeviceUnits(620) ? 2 : 1;
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(76 + (Columns(width) == 3 ? 1 : Columns(width) == 2 ? 2 : 3) * 54);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int columns = Columns(ClientSize.Width), inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14);
        int available = Math.Max(1, ClientSize.Width - inset * 2), top = LogicalToDeviceUnits(26), rowHeight = LogicalToDeviceUnits(54);
        for (int i = 0; i < fields.Length; i++)
        {
            int column = i % columns, row = i / columns, span = columns == 2 && i == 2 ? 2 : 1;
            int left = column * (available + gap) / columns;
            int right = (column + span) * (available + gap) / columns - gap;
            labels[i].SetBounds(inset + left, top + row * rowHeight, right - left, LogicalToDeviceUnits(18));
            int height = fields[i] is TextBox text ? text.PreferredHeight : fields[i].PreferredSize.Height;
            fields[i].SetBounds(inset + left, top + row * rowHeight + LogicalToDeviceUnits(20), right - left, height);
        }
        add.SetBounds(ClientSize.Width - inset - LogicalToDeviceUnits(138), ClientSize.Height - inset - LogicalToDeviceUnits(30),
            LogicalToDeviceUnits(138), LogicalToDeviceUnits(30));
    }
}

internal sealed class AdminActionsCard : GroupBox
{
    private readonly Button[] actions;
    private readonly Label note = new()
    {
        Name = "adminActionNote", Text = "Pasife alma, hesabın açık oturumlarını sonlandırır. Kendi parolanız için Dosya menüsünü kullanın.",
        AutoSize = false, TabStop = false
    };

    internal AdminActionsCard(Button[] actions)
    {
        this.actions = actions;
        SuspendLayout(); Name = "adminSelected"; Text = "Listeden seçilen hesap"; TabStop = false; TabIndex = 1;
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Dock = DockStyle.None; actions[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            actions[i].AutoSize = false; actions[i].TabIndex = i; actions[i].TabStop = true; Controls.Add(actions[i]);
        }
        Controls.Add(note); ResumeLayout(true);
    }

    private int Columns(int width) => width >= LogicalToDeviceUnits(900) ? 4 : 2;
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(100 + ((actions.Length + Columns(width) - 1) / Columns(width)) * 42);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), columns = Columns(ClientSize.Width);
        int width = Math.Max(1, ClientSize.Width - inset * 2), top = LogicalToDeviceUnits(26);
        for (int i = 0; i < actions.Length; i++)
        {
            int column = i % columns, left = column * (width + gap) / columns;
            int right = (column + 1) * (width + gap) / columns - gap;
            actions[i].SetBounds(inset + left, top + i / columns * LogicalToDeviceUnits(42), right - left, LogicalToDeviceUnits(30));
        }
        int rows = (actions.Length + columns - 1) / columns;
        note.SetBounds(inset, top + rows * LogicalToDeviceUnits(42) + LogicalToDeviceUnits(8), width, LogicalToDeviceUnits(54));
    }
}

internal sealed class AdminWorkspace : ScrollableControl
{
    private readonly AdminCreateCard create;
    private readonly AdminActionsCard actions;
    private readonly GroupBox records;
    private bool arranging;

    internal AdminWorkspace(AdminCreateCard create, AdminActionsCard actions, GroupBox records)
    {
        this.create = create; this.actions = actions; this.records = records;
        SuspendLayout(); Name = "adminWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(create); Controls.Add(actions); Controls.Add(records); ResumeLayout(true);
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
                var metrics = Measure(viewport, padding, gap);
                // Start without the old scrollbar so shrinking and growing
                // choose the same layout at a given available size.
                if (padding * 2 + metrics.EditorHeight + gap + minimumListHeight > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    metrics = Measure(viewport, padding, gap);
                }
                var (width, createWidth, actionsWidth, createHeight, actionsHeight, editorHeight, paired) = metrics;
                int left = (viewport - width) / 2, recordsTop = padding + editorHeight + gap;
                var extent = new Size(0, recordsTop + minimumListHeight + padding);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y;
                create.SetBounds(left, padding + offset, createWidth, paired ? editorHeight : createHeight);
                actions.SetBounds(paired ? left + createWidth + gap : left,
                    padding + (paired ? 0 : createHeight + gap) + offset, actionsWidth, paired ? editorHeight : actionsHeight);
                records.SetBounds(left, recordsTop + offset, width, Math.Max(minimumListHeight, ClientSize.Height - recordsTop - padding));
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private (int Width, int CreateWidth, int ActionsWidth, int CreateHeight, int ActionsHeight, int EditorHeight, bool Paired)
        Measure(int viewport, int padding, int gap)
    {
        int width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
        bool paired = width >= LogicalToDeviceUnits(1080);
        int actionsWidth = paired ? Math.Clamp(width * 2 / 5, LogicalToDeviceUnits(340), LogicalToDeviceUnits(440)) : width;
        int createWidth = paired ? width - actionsWidth - gap : width;
        int createHeight = create.HeightForWidth(createWidth), actionsHeight = actions.HeightForWidth(actionsWidth);
        int editorHeight = paired ? Math.Max(createHeight, actionsHeight) : createHeight + gap + actionsHeight;
        return (width, createWidth, actionsWidth, createHeight, actionsHeight, editorHeight, paired);
    }
}
