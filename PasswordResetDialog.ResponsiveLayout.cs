namespace WinFormsApp2;

internal sealed partial class PasswordResetDialog
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = true; MinimizeBox = false;
        MinimumSize = LogicalToDeviceUnits(new Size(560, 420));
    }
}

internal sealed partial class TemporaryPasswordDialog
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 420));
    }
}

internal sealed class SecretDialogInfoCard : GroupBox
{
    private readonly Label account, details;
    internal SecretDialogInfoCard(string name, string title, string identity, string description)
    {
        SuspendLayout(); Name = name; Text = title; TabStop = false; TabIndex = 0;
        account = new Label { Name = name + "Account", Text = identity, AutoSize = false, UseMnemonic = false, TabStop = false };
        details = new Label { Name = name + "Details", Text = description, AutoSize = false, UseMnemonic = false, TabStop = false };
        Controls.Add(account); Controls.Add(details);
        foreach (var label in new[] { account, details }) label.FontChanged += (_, _) => { PerformLayout(); Parent?.PerformLayout(); Parent?.Parent?.PerformLayout(); };
        ResumeLayout(true);
    }
    private int TextHeight(Label label, int width, int minimum) => Math.Max(LogicalToDeviceUnits(minimum), TextRenderer.MeasureText(label.Text, label.Font,
        new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(48) + TextHeight(account, width, 24) + TextHeight(details, width, 36);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), width = Math.Max(1, ClientSize.Width - inset * 2), top = LogicalToDeviceUnits(26);
        int identityHeight = TextHeight(account, ClientSize.Width, 24);
        account.SetBounds(inset, top, width, identityHeight);
        details.SetBounds(inset, top + identityHeight + LogicalToDeviceUnits(10), width, TextHeight(details, ClientSize.Width, 36));
    }
}

internal sealed class SecretDialogEntryCard : GroupBox
{
    private readonly Label caption;
    private readonly TextBox secret;
    private readonly CheckBox? show;
    internal SecretDialogEntryCard(string name, string title, string prompt, TextBox secret, CheckBox? show = null)
    {
        this.secret = secret; this.show = show;
        SuspendLayout(); Name = name; Text = title; TabStop = false; TabIndex = 1;
        caption = new Label { Name = name + "Prompt", Text = prompt, AutoSize = false, TabStop = false, UseMnemonic = false };
        secret.Dock = DockStyle.None; secret.Anchor = AnchorStyles.Top | AnchorStyles.Left; secret.AutoSize = true; secret.TabIndex = 0; secret.AccessibleName = prompt;
        Controls.Add(caption); Controls.Add(secret);
        if (show != null) { show.AutoSize = false; show.TabIndex = 1; Controls.Add(show); }
        caption.FontChanged += (_, _) => { PerformLayout(); Parent?.PerformLayout(); Parent?.Parent?.PerformLayout(); };
        ResumeLayout(true);
    }
    private int CaptionHeight(int width) => Math.Max(LogicalToDeviceUnits(22), TextRenderer.MeasureText(caption.Text, caption.Font,
        new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height + LogicalToDeviceUnits(4));
    internal int HeightForWidth(int width) => Math.Max(LogicalToDeviceUnits(104), LogicalToDeviceUnits(46 + (show == null ? 0 : 40)) + CaptionHeight(width) + secret.PreferredHeight);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), width = Math.Max(1, ClientSize.Width - inset * 2), top = LogicalToDeviceUnits(26);
        int captionHeight = CaptionHeight(ClientSize.Width), inputTop = top + captionHeight + LogicalToDeviceUnits(8);
        caption.SetBounds(inset, top, width, captionHeight); secret.SetBounds(inset, inputTop, width, secret.PreferredHeight);
        show?.SetBounds(inset, inputTop + secret.PreferredHeight + LogicalToDeviceUnits(10), width, LogicalToDeviceUnits(30));
    }
}

internal sealed class SecretDialogActions : Panel
{
    private readonly (Button Button, int Width)[] buttons;
    internal SecretDialogActions((Button Button, int Width)[] buttons)
    {
        this.buttons = buttons; SuspendLayout(); Name = "secretDialogActions"; TabIndex = 1;
        for (int i = 0; i < buttons.Length; i++)
        { var button = buttons[i].Button; button.Dock = DockStyle.None; button.Anchor = AnchorStyles.Top | AnchorStyles.Left; button.AutoSize = false; button.TabIndex = i; Controls.Add(button); }
        ResumeLayout(true);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14);
        int width = Math.Min(LogicalToDeviceUnits(1120), Math.Max(1, ClientSize.Width - padding * 2));
        int left = (ClientSize.Width - width) / 2 + width;
        for (int i = buttons.Length - 1; i >= 0; i--)
        {
            int buttonWidth = LogicalToDeviceUnits(buttons[i].Width); left -= buttonWidth;
            buttons[i].Button.SetBounds(left, LogicalToDeviceUnits(7), buttonWidth, LogicalToDeviceUnits(30)); left -= gap;
        }
    }
}

internal sealed class SecretDialogWorkspace : ScrollableControl
{
    private readonly SecretDialogInfoCard info;
    private readonly SecretDialogEntryCard entry;
    private bool arranging;
    internal SecretDialogWorkspace(SecretDialogInfoCard info, SecretDialogEntryCard entry)
    {
        this.info = info; this.entry = entry;
        SuspendLayout(); Name = "secretDialogWorkspace"; AutoScroll = true; DoubleBuffered = true; TabIndex = 0;
        Controls.Add(info); Controls.Add(entry); ResumeLayout(true);
    }
    internal int ContentHeightForWidth(int width) => Measure(width).Height;
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
                var metrics = Measure(viewport);
                if (metrics.Height > Height) { viewport -= SystemInformation.VerticalScrollBarWidth; metrics = Measure(viewport); }
                var (width, infoWidth, entryWidth, infoHeight, entryHeight, paired, height) = metrics;
                var extent = new Size(0, height); if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int left = (viewport - width) / 2, top = padding + AutoScrollPosition.Y;
                info.SetBounds(left, top, infoWidth, paired ? Math.Max(infoHeight, entryHeight) : infoHeight);
                entry.SetBounds(paired ? left + infoWidth + gap : left, paired ? top : top + infoHeight + gap,
                    entryWidth, paired ? Math.Max(infoHeight, entryHeight) : entryHeight);
                base.OnLayout(e); if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }
    private (int Width, int InfoWidth, int EntryWidth, int InfoHeight, int EntryHeight, bool Paired, int Height) Measure(int viewport)
    {
        int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14);
        int width = Math.Min(LogicalToDeviceUnits(1120), Math.Max(1, viewport - padding * 2)); bool paired = width >= LogicalToDeviceUnits(900);
        int entryWidth = paired ? (width - gap) * 55 / 100 : width, infoWidth = paired ? width - entryWidth - gap : width;
        int infoHeight = info.HeightForWidth(infoWidth), entryHeight = entry.HeightForWidth(entryWidth);
        return (width, infoWidth, entryWidth, infoHeight, entryHeight, paired, padding * 2 + (paired ? Math.Max(infoHeight, entryHeight) : infoHeight + gap + entryHeight));
    }
}

internal sealed class SecretDialogBody : Panel
{
    private readonly SecretDialogWorkspace workspace;
    private readonly SecretDialogActions actions;
    internal SecretDialogBody(SecretDialogWorkspace workspace, SecretDialogActions actions)
    {
        this.workspace = workspace; this.actions = actions;
        SuspendLayout(); Name = "secretDialogBody"; workspace.Dock = actions.Dock = DockStyle.None;
        workspace.Anchor = actions.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(workspace); Controls.Add(actions); ResumeLayout(true);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int actionHeight = LogicalToDeviceUnits(44), height = Math.Min(ClientSize.Height, workspace.ContentHeightForWidth(ClientSize.Width) + actionHeight);
        int top = Math.Max(0, (ClientSize.Height - height) / 2), contentHeight = Math.Max(1, height - actionHeight);
        workspace.SetBounds(0, top, ClientSize.Width, contentHeight); actions.SetBounds(0, top + contentHeight, ClientSize.Width, actionHeight);
    }
}
