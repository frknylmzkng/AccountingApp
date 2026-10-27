namespace WinFormsApp2;

public partial class Contact
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 420));
    }
}

internal sealed class ContactCard : GroupBox
{
    internal TextBox Phone { get; }
    internal Button Copy { get; }
    private readonly Label caption = new() { Text = "Telefon", AutoSize = false, TabStop = false };

    internal ContactCard(string name, TextBox phone, int index)
    {
        Phone = phone;
        Copy = new Button { Name = "contactCopy" + index, Text = "Numarayı kopyala", TabIndex = 1, AutoSize = false, TabStop = true };
        SuspendLayout(); Name = "ContactCard" + index; Text = name; TabIndex = index; TabStop = false;
        phone.Dock = DockStyle.None; phone.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        phone.AutoSize = true; phone.ReadOnly = true; phone.Font = AppTheme.TitleFont;
        phone.TextAlign = HorizontalAlignment.Left; phone.TabIndex = 0; phone.TabStop = true; phone.HideSelection = false;
        void Describe()
        {
            phone.AccessibleName = Text + " — telefon";
            Copy.AccessibleName = Text + " — numarayı kopyala";
        }
        TextChanged += (_, _) => Describe(); Describe();
        Controls.Add(caption); Controls.Add(phone); Controls.Add(Copy); ResumeLayout(true);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(16), width = Math.Max(1, ClientSize.Width - inset * 2);
        caption.SetBounds(inset, LogicalToDeviceUnits(28), width, LogicalToDeviceUnits(20));
        Phone.SetBounds(inset, LogicalToDeviceUnits(54), width, Phone.PreferredHeight);
        Copy.SetBounds(inset, LogicalToDeviceUnits(112), LogicalToDeviceUnits(148), LogicalToDeviceUnits(30));
    }
}

internal sealed class ContactWorkspace : ScrollableControl
{
    private readonly ContactCard[] cards;
    private bool arranging;

    internal ContactWorkspace(ContactCard[] cards)
    {
        this.cards = cards;
        SuspendLayout(); Name = "contactWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.AddRange(cards); ResumeLayout(true);
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
                int padding = LogicalToDeviceUnits(16), gap = LogicalToDeviceUnits(16), cardHeight = LogicalToDeviceUnits(184);
                int viewport = Width;
                var metrics = Measure(viewport, padding, gap, cardHeight);
                // Measure without the previous scrollbar so widening can return
                // naturally to two columns and clear the old scroll position.
                if (metrics.Height > Height)
                {
                    viewport -= SystemInformation.VerticalScrollBarWidth;
                    metrics = Measure(viewport, padding, gap, cardHeight);
                }
                var (width, columns, height) = metrics;
                var extent = new Size(0, height);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int left = (viewport - width) / 2;
                int offset = AutoScrollPosition.Y + Math.Max(0, (ClientSize.Height - height) / 2);
                for (int i = 0; i < cards.Length; i++)
                {
                    int column = i % columns;
                    int x = column * (width + gap) / columns;
                    int right = (column + 1) * (width + gap) / columns - gap;
                    cards[i].SetBounds(left + x, padding + i / columns * (cardHeight + gap) + offset, right - x, cardHeight);
                }
                base.OnLayout(e);
                if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }

    private (int Width, int Columns, int Height) Measure(int viewport, int padding, int gap, int cardHeight)
    {
        int width = Math.Min(LogicalToDeviceUnits(1040), Math.Max(1, viewport - padding * 2));
        int columns = width >= LogicalToDeviceUnits(700) ? 2 : 1;
        int rows = (cards.Length + columns - 1) / columns;
        return (width, columns, padding * 2 + rows * cardHeight + (rows - 1) * gap);
    }
}
