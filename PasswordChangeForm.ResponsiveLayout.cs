namespace WinFormsApp2;

internal sealed partial class PasswordChangeForm
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class PasswordChangeEditor : GroupBox
{
    private readonly Label notice;
    private readonly TextBox[] secrets;
    private readonly Label[] captions;

    internal PasswordChangeEditor(Label notice, TextBox[] secrets)
    {
        this.notice = notice; this.secrets = secrets;
        string[] text = ["Mevcut / geçici parola:", "Yeni parola:", "Yeni parola tekrar:"];
        captions = text.Select(t => new Label { Text = t, AutoSize = false, TabStop = false }).ToArray();
        SuspendLayout(); Name = "passwordChangeEditor"; Text = "Parola bilgileri"; TabStop = false; TabIndex = 0;
        notice.AutoSize = false; notice.MaximumSize = Size.Empty; notice.Padding = Padding.Empty; notice.TextAlign = ContentAlignment.TopLeft; notice.TabStop = false;
        Controls.Add(notice);
        for (int i = 0; i < secrets.Length; i++)
        {
            secrets[i].AutoSize = true; secrets[i].TabIndex = i; secrets[i].AccessibleName = text[i];
            Controls.Add(captions[i]); Controls.Add(secrets[i]);
        }
        foreach (Control control in Controls) { control.Dock = DockStyle.None; control.Anchor = AnchorStyles.Top | AnchorStyles.Left; }
        // The notice changes on load for temporary-password sessions. Remeasure
        // the label even when its neighbouring card keeps the outer size stable.
        void Remeasure() { PerformLayout(); Parent?.PerformLayout(); }
        notice.TextChanged += (_, _) => Remeasure(); notice.FontChanged += (_, _) => Remeasure();
        ResumeLayout(true);
    }
    private bool Inline(int width) => width >= LogicalToDeviceUnits(620);
    private int NoticeHeight(int width) => Math.Max(LogicalToDeviceUnits(36), TextRenderer.MeasureText(notice.Text, notice.Font,
        new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(50 + secrets.Length * (Inline(width) ? 42 : 64)) + NoticeHeight(width);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), width = Math.Max(1, ClientSize.Width - inset * 2);
        int top = LogicalToDeviceUnits(26), noticeHeight = NoticeHeight(ClientSize.Width);
        notice.SetBounds(inset, top, width, noticeHeight); top += noticeHeight + LogicalToDeviceUnits(12);
        bool inline = Inline(ClientSize.Width); int captionWidth = LogicalToDeviceUnits(176), rowHeight = LogicalToDeviceUnits(inline ? 42 : 64);
        for (int i = 0; i < secrets.Length; i++)
        {
            int y = top + i * rowHeight;
            captions[i].TextAlign = inline ? ContentAlignment.MiddleLeft : ContentAlignment.TopLeft;
            captions[i].SetBounds(inset, y, inline ? captionWidth : width, LogicalToDeviceUnits(22));
            secrets[i].SetBounds(inline ? inset + captionWidth + gap : inset, y + LogicalToDeviceUnits(inline ? 0 : 24),
                inline ? width - captionWidth - gap : width, secrets[i].PreferredHeight);
        }
    }
}

internal sealed class PasswordChangeRules : GroupBox
{
    private readonly Label description = new() { Name = "passwordChangePolicy", Text = PasswordPolicy.Description, AutoSize = false, TabStop = false };
    internal PasswordChangeRules()
    {
        SuspendLayout(); Name = "passwordChangeRules"; Text = "Parola kuralları"; TabStop = false; TabIndex = 1;
        description.Dock = DockStyle.None; description.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        description.FontChanged += (_, _) => { PerformLayout(); Parent?.PerformLayout(); };
        Controls.Add(description); ResumeLayout(true);
    }
    private int TextHeight(int width) => Math.Max(LogicalToDeviceUnits(36), TextRenderer.MeasureText(description.Text, description.Font,
        new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(38) + TextHeight(width);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12);
        description.SetBounds(inset, LogicalToDeviceUnits(26), Math.Max(1, ClientSize.Width - inset * 2), TextHeight(ClientSize.Width));
    }
}

internal sealed class PasswordChangeActions : Panel
{
    private readonly Button save;
    private readonly Label hint = new() { Name = "passwordChangeKeys", Text = "Enter: Değiştir    Tab: Sonraki alan", AutoSize = false, TabStop = false, TextAlign = ContentAlignment.MiddleLeft };
    internal PasswordChangeActions(Button save)
    {
        this.save = save; SuspendLayout(); Name = "passwordChangeActions"; save.Dock = DockStyle.None;
        save.Anchor = AnchorStyles.Top | AnchorStyles.Left; save.AutoSize = false; save.TabIndex = 0; Controls.Add(hint); Controls.Add(save); ResumeLayout(true);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14), buttonWidth = LogicalToDeviceUnits(174);
        int width = Math.Min(LogicalToDeviceUnits(1120), Math.Max(1, ClientSize.Width - padding * 2)), left = (ClientSize.Width - width) / 2;
        hint.SetBounds(left, LogicalToDeviceUnits(7), Math.Max(1, width - buttonWidth - gap), LogicalToDeviceUnits(30));
        save.SetBounds(left + width - buttonWidth, LogicalToDeviceUnits(7), buttonWidth, LogicalToDeviceUnits(30));
    }
}

internal sealed class PasswordChangeWorkspace : ScrollableControl
{
    private readonly PasswordChangeEditor editor;
    private readonly PasswordChangeRules rules;
    private bool arranging;
    internal PasswordChangeWorkspace(PasswordChangeEditor editor, PasswordChangeRules rules)
    {
        this.editor = editor; this.rules = rules;
        SuspendLayout(); Name = "passwordChangeWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(editor); Controls.Add(rules); ResumeLayout(true);
    }
    internal int ContentHeightForWidth(int width) => Measure(width, LogicalToDeviceUnits(8), LogicalToDeviceUnits(14)).Height;
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
                var (width, editorWidth, rulesWidth, editorHeight, rulesHeight, paired, height) = metrics;
                var extent = new Size(0, height); if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int left = (viewport - width) / 2, top = padding + AutoScrollPosition.Y + Math.Max(0, (ClientSize.Height - height) / 2);
                editor.SetBounds(left, top, editorWidth, paired ? Math.Max(editorHeight, rulesHeight) : editorHeight);
                rules.SetBounds(paired ? left + editorWidth + gap : left, paired ? top : top + editorHeight + gap,
                    rulesWidth, paired ? Math.Max(editorHeight, rulesHeight) : rulesHeight);
                base.OnLayout(e); if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }
    private (int Width, int EditorWidth, int RulesWidth, int EditorHeight, int RulesHeight, bool Paired, int Height) Measure(int viewport, int padding, int gap)
    {
        int width = Math.Min(LogicalToDeviceUnits(1120), Math.Max(1, viewport - padding * 2)); bool paired = width >= LogicalToDeviceUnits(1000);
        int rulesWidth = paired ? LogicalToDeviceUnits(340) : width, editorWidth = paired ? width - rulesWidth - gap : width;
        int editorHeight = editor.HeightForWidth(editorWidth), rulesHeight = rules.HeightForWidth(rulesWidth);
        return (width, editorWidth, rulesWidth, editorHeight, rulesHeight, paired, padding * 2 + (paired ? Math.Max(editorHeight, rulesHeight) : editorHeight + gap + rulesHeight));
    }
}

// Centre the complete form, including its action, when there is room. When
// space is short only the fields/rules scroll; the action remains visible.
internal sealed class PasswordChangeBody : Panel
{
    private readonly PasswordChangeWorkspace workspace;
    private readonly PasswordChangeActions actions;
    internal PasswordChangeBody(PasswordChangeWorkspace workspace, PasswordChangeActions actions)
    {
        this.workspace = workspace; this.actions = actions;
        SuspendLayout(); Name = "passwordChangeBody"; workspace.Dock = actions.Dock = DockStyle.None;
        workspace.Anchor = actions.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        Controls.Add(workspace); Controls.Add(actions); ResumeLayout(true);
    }
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int actionHeight = LogicalToDeviceUnits(44);
        int height = Math.Min(ClientSize.Height, workspace.ContentHeightForWidth(ClientSize.Width) + actionHeight);
        int top = Math.Max(0, (ClientSize.Height - height) / 2);
        workspace.SetBounds(0, top, ClientSize.Width, Math.Max(1, height - actionHeight));
        actions.SetBounds(0, top + Math.Max(1, height - actionHeight), ClientSize.Width, actionHeight);
    }
}
