namespace WinFormsApp2;

// The action buttons keep their original handlers and keyboard access. Only their
// containers reflow; scrollbars are used when the available height is insufficient.
internal sealed class DashboardActionCard : GroupBox
{
    internal Button Action { get; }
    internal Label Description { get; }

    internal DashboardActionCard(string category, Button action, string description)
    {
        Text = category; Name = "dashboardCard"; TabStop = false;
        Action = action;
        Action.Dock = DockStyle.None; Action.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Action.AutoSize = false; Action.TabStop = true; Action.TabIndex = 0;
        Action.TextAlign = ContentAlignment.MiddleLeft; Action.Padding = new Padding(12, 0, 8, 0);
        Description = new Label { Text = description, AutoSize = false, TabStop = false, UseMnemonic = false };
        Action.AccessibleDescription = description;
        Controls.Add(Action); Controls.Add(Description);
        Description.Click += (_, _) => Action.PerformClick();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), top = LogicalToDeviceUnits(23), buttonHeight = LogicalToDeviceUnits(40);
        Action.SetBounds(inset, top, Math.Max(1, ClientSize.Width - inset * 2), buttonHeight);
        int descriptionTop = top + buttonHeight + LogicalToDeviceUnits(8);
        Description.SetBounds(inset + LogicalToDeviceUnits(2), descriptionTop,
            Math.Max(1, ClientSize.Width - inset * 2 - LogicalToDeviceUnits(4)), Math.Max(1, ClientSize.Height - descriptionTop - LogicalToDeviceUnits(8)));
    }
}

internal sealed class DashboardWorkspace : ScrollableControl
{
    private readonly GroupBox navigation;
    private readonly DashboardActionCard[] cards;
    private readonly Label heading = new() { Text = "Hızlı erişim", Font = AppTheme.TitleFont, AutoSize = false };
    private readonly Label explanation = new() { Text = "Başlamak için bir işlem seçin.", AutoSize = false };
    private bool arranging;

    internal DashboardWorkspace(GroupBox navigation, DashboardActionCard[] cards)
    {
        this.navigation = navigation; this.cards = cards;
        SuspendLayout();
        Name = "dashboardWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(navigation); Controls.Add(heading); Controls.Add(explanation);
        for (int i = 0; i < cards.Length; i++) { cards[i].TabIndex = i + 1; Controls.Add(cards[i]); }
        ResumeLayout(true);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        if (arranging) return;
        arranging = true;
        try
        {
            base.OnLayout(e);
            // Adding/removing a scrollbar changes the usable width. Let the
            // scroll container recalculate after placing children, then reflow
            // again if that changed the viewport (including after a resize).
            for (int pass = 0; pass < 4; pass++)
            {
                int width = ClientSize.Width;
                int padding = LogicalToDeviceUnits(width < LogicalToDeviceUnits(760) ? 8 : 16);
                int gap = LogicalToDeviceUnits(16), intro = LogicalToDeviceUnits(58);
                int contentWidth = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, width - padding * 2));
                int left = (width - contentWidth) / 2;
                bool showNavigation = width >= LogicalToDeviceUnits(1100);
                int navigationWidth = showNavigation ? LogicalToDeviceUnits(224) : 0;
                int gridLeft = left + (showNavigation ? navigationWidth + gap : 0);
                int gridWidth = Math.Max(1, contentWidth - (showNavigation ? navigationWidth + gap : 0));
                int columns = Math.Clamp((gridWidth + gap) / LogicalToDeviceUnits(276), 1, 4);
                int rows = (cards.Length + columns - 1) / columns;
                int cardHeight = LogicalToDeviceUnits(124);
                int contentHeight = padding * 2 + intro + rows * cardHeight + Math.Max(0, rows - 1) * gap;
                var extent = new Size(0, contentHeight);
                if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                if (ClientSize.Width != width) continue;

                // Center the complete workspace when it fits vertically; short
                // windows start at the top and expose the same actions by scroll.
                int offset = AutoScrollPosition.Y + Math.Max(0, (ClientSize.Height - contentHeight) / 2);
                heading.SetBounds(gridLeft, padding + offset, gridWidth, LogicalToDeviceUnits(30));
                explanation.SetBounds(gridLeft, padding + LogicalToDeviceUnits(34) + offset, gridWidth, LogicalToDeviceUnits(22));
                navigation.Visible = showNavigation;
                navigation.SetBounds(left, padding + offset, Math.Max(1, navigationWidth), contentHeight - padding * 2);
                for (int i = 0; i < cards.Length; i++)
                {
                    int column = i % columns;
                    // Fractional column boundaries distribute rounding pixels evenly.
                    int x = column * (gridWidth + gap) / columns;
                    int right = (column + 1) * (gridWidth + gap) / columns - gap;
                    cards[i].SetBounds(gridLeft + x, padding + intro + i / columns * (cardHeight + gap) + offset, right - x, cardHeight);
                }
                base.OnLayout(e);
                if (ClientSize.Width == width) break;
            }
        }
        finally { arranging = false; }
    }
}
