namespace WinFormsApp2;

internal sealed partial class PaymentsForm
{
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        MinimumSize = LogicalToDeviceUnits(new Size(560, 480));
    }
}

internal sealed class PaymentEntryCard : GroupBox
{
    private readonly TextBox amount;
    private readonly Button pay, receipt, reverse, inspect, acknowledge;
    private readonly Label state;
    private readonly Label caption = new() { Text = "Tutar (TL):", AutoSize = false, TabStop = false };

    internal PaymentEntryCard(TextBox amount, Button pay, Button receipt, Button reverse, Label state, Button inspect, Button acknowledge)
    {
        this.amount = amount; this.pay = pay; this.receipt = receipt; this.reverse = reverse;
        this.state = state; this.inspect = inspect; this.acknowledge = acknowledge;
        SuspendLayout(); Name = "paymentEntry"; Text = "Ödeme ve sonuç kontrolü"; TabStop = false; TabIndex = 0;
        var controls = new Control[] { amount, pay, receipt, reverse, state, inspect, acknowledge };
        for (int i = 0; i < controls.Length; i++)
        {
            controls[i].Dock = DockStyle.None; controls[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            controls[i].TabIndex = i; if (controls[i] is Button button) { button.AutoSize = false; button.TabStop = true; }
            Controls.Add(controls[i]);
        }
        amount.AutoSize = true;
        state.AutoSize = false; state.MaximumSize = Size.Empty; state.TextAlign = ContentAlignment.MiddleLeft;
        state.Text = "Ödeme sonucu kontrol ediliyor…";
        state.TextChanged += (_, _) => Parent?.PerformLayout();
        state.FontChanged += (_, _) => Parent?.PerformLayout();
        Controls.Add(caption); ResumeLayout(true);
    }

    private bool Paired(int width) => width >= LogicalToDeviceUnits(620);
    private int StateHeight(int width) => Math.Max(LogicalToDeviceUnits(54),
        TextRenderer.MeasureText(state.Text, state.Font, new Size(Math.Max(1, width - LogicalToDeviceUnits(24)), 0), TextFormatFlags.WordBreak).Height + LogicalToDeviceUnits(8));
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(Paired(width) ? 192 : 234) + StateHeight(width);

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), top = LogicalToDeviceUnits(26);
        int width = Math.Max(1, ClientSize.Width - inset * 2), half = (width - gap) / 2;
        bool paired = Paired(ClientSize.Width);
        caption.SetBounds(inset, top, paired ? half : width, LogicalToDeviceUnits(18));
        amount.SetBounds(inset, top + LogicalToDeviceUnits(20), paired ? half : width, amount.PreferredHeight);
        pay.SetBounds(paired ? inset + half + gap : inset, top + LogicalToDeviceUnits(paired ? 20 : 54), paired ? width - half - gap : width, LogicalToDeviceUnits(30));
        int secondaryTop = top + LogicalToDeviceUnits(paired ? 54 : 96);
        receipt.SetBounds(inset, secondaryTop, half, LogicalToDeviceUnits(30));
        reverse.SetBounds(inset + half + gap, secondaryTop, width - half - gap, LogicalToDeviceUnits(30));
        int stateTop = secondaryTop + LogicalToDeviceUnits(50), stateHeight = StateHeight(ClientSize.Width);
        state.SetBounds(inset, stateTop, width, stateHeight);
        int recoveryTop = stateTop + stateHeight + LogicalToDeviceUnits(8);
        inspect.SetBounds(inset, recoveryTop, half, LogicalToDeviceUnits(30));
        acknowledge.SetBounds(inset + half + gap, recoveryTop, width - half - gap, LogicalToDeviceUnits(30));
    }
}

internal sealed class PaymentClosingCard : GroupBox
{
    private readonly DateTimePicker day;
    private readonly TextBox declared;
    private readonly CheckBox thermal;
    private readonly Button closing;
    private readonly Label dayLabel = new() { Text = "Kapanış günü:", AutoSize = false, TabStop = false };
    private readonly Label declaredLabel = new() { Text = "Beyan (TL):", AutoSize = false, TabStop = false };

    internal PaymentClosingCard(DateTimePicker day, TextBox declared, CheckBox thermal, Button closing)
    {
        this.day = day; this.declared = declared; this.thermal = thermal; this.closing = closing;
        SuspendLayout(); Name = "paymentDaily"; Text = "Günlük kapanış ve fiş"; TabStop = false; TabIndex = 1;
        var controls = new Control[] { day, declared, thermal, closing };
        for (int i = 0; i < controls.Length; i++)
        {
            controls[i].Dock = DockStyle.None; controls[i].Anchor = AnchorStyles.Top | AnchorStyles.Left;
            controls[i].TabIndex = i; controls[i].TabStop = true; Controls.Add(controls[i]);
        }
        thermal.Text = "Fişleri 80 mm yazıcıya göre hazırla"; thermal.AutoSize = false; closing.AutoSize = false;
        Controls.Add(dayLabel); Controls.Add(declaredLabel); ResumeLayout(true);
    }
    internal int HeightForWidth(int width) => LogicalToDeviceUnits(width >= LogicalToDeviceUnits(620) ? 170 : 224);
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset = LogicalToDeviceUnits(12), gap = LogicalToDeviceUnits(14), top = LogicalToDeviceUnits(26);
        int width = Math.Max(1, ClientSize.Width - inset * 2); bool paired = ClientSize.Width >= LogicalToDeviceUnits(620);
        int fieldWidth = paired ? (width - gap) / 2 : width;
        dayLabel.SetBounds(inset, top, fieldWidth, LogicalToDeviceUnits(18));
        day.SetBounds(inset, top + LogicalToDeviceUnits(20), fieldWidth, day.PreferredSize.Height);
        int otherLeft = paired ? inset + fieldWidth + gap : inset, otherTop = top + LogicalToDeviceUnits(paired ? 0 : 54);
        declaredLabel.SetBounds(otherLeft, otherTop, fieldWidth, LogicalToDeviceUnits(18));
        declared.SetBounds(otherLeft, otherTop + LogicalToDeviceUnits(20), fieldWidth, declared.PreferredHeight);
        int paperTop = top + LogicalToDeviceUnits(paired ? 62 : 116);
        thermal.SetBounds(inset, paperTop, width, LogicalToDeviceUnits(30));
        closing.SetBounds(inset, paperTop + LogicalToDeviceUnits(40), width, LogicalToDeviceUnits(30));
    }
}

internal sealed class PaymentsWorkspace : ScrollableControl
{
    private readonly PaymentEntryCard payment;
    private readonly PaymentClosingCard daily;
    private readonly GroupBox people, ledger;
    private bool arranging;
    internal PaymentsWorkspace(PaymentEntryCard payment, PaymentClosingCard daily, GroupBox people, GroupBox ledger)
    {
        this.payment = payment; this.daily = daily; this.people = people; this.ledger = ledger;
        SuspendLayout(); Name = "paymentsWorkspace"; AutoScroll = true; DoubleBuffered = true;
        Controls.Add(payment); Controls.Add(daily); Controls.Add(people); Controls.Add(ledger); ResumeLayout(true);
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
                int padding = LogicalToDeviceUnits(8), gap = LogicalToDeviceUnits(14), minListHeight = LogicalToDeviceUnits(180);
                int viewport = Width; var metrics = Measure(viewport, padding, gap, minListHeight);
                if (metrics.MinHeight > Height) { viewport -= SystemInformation.VerticalScrollBarWidth; metrics = Measure(viewport, padding, gap, minListHeight); }
                var (width, paymentWidth, dailyWidth, paymentHeight, dailyHeight, editorHeight, paired, listsPaired, minHeight) = metrics;
                int left = (viewport - width) / 2;
                var extent = new Size(0, minHeight); if (AutoScrollMinSize != extent) AutoScrollMinSize = extent;
                int offset = AutoScrollPosition.Y, listsTop = padding + editorHeight + gap;
                payment.SetBounds(left, padding + offset, paymentWidth, paired ? editorHeight : paymentHeight);
                daily.SetBounds(paired ? left + paymentWidth + gap : left, padding + (paired ? 0 : paymentHeight + gap) + offset, dailyWidth, paired ? editorHeight : dailyHeight);
                int availableHeight = Math.Max(listsPaired ? minListHeight : minListHeight * 2 + gap, ClientSize.Height - listsTop - padding);
                if (listsPaired)
                {
                    int peopleWidth = (width - gap) * 44 / 100;
                    people.SetBounds(left, listsTop + offset, peopleWidth, availableHeight);
                    ledger.SetBounds(left + peopleWidth + gap, listsTop + offset, width - peopleWidth - gap, availableHeight);
                }
                else
                {
                    int peopleHeight = (availableHeight - gap) / 2;
                    people.SetBounds(left, listsTop + offset, width, peopleHeight);
                    ledger.SetBounds(left, listsTop + peopleHeight + gap + offset, width, availableHeight - peopleHeight - gap);
                }
                base.OnLayout(e); if (ClientSize.Width == viewport) break;
            }
        }
        finally { arranging = false; }
    }
    private (int Width, int PaymentWidth, int DailyWidth, int PaymentHeight, int DailyHeight, int EditorHeight, bool Paired, bool ListsPaired, int MinHeight)
        Measure(int viewport, int padding, int gap, int minListHeight)
    {
        int width = Math.Min(LogicalToDeviceUnits(1760), Math.Max(1, viewport - padding * 2));
        bool paired = width >= LogicalToDeviceUnits(1080), listsPaired = width >= LogicalToDeviceUnits(1740);
        int dailyWidth = paired ? Math.Clamp(width / 3, LogicalToDeviceUnits(340), LogicalToDeviceUnits(440)) : width;
        int paymentWidth = paired ? width - dailyWidth - gap : width;
        int paymentHeight = payment.HeightForWidth(paymentWidth), dailyHeight = daily.HeightForWidth(dailyWidth);
        int editorHeight = paired ? Math.Max(paymentHeight, dailyHeight) : paymentHeight + gap + dailyHeight;
        int minHeight = padding * 2 + editorHeight + gap + (listsPaired ? minListHeight : minListHeight * 2 + gap);
        return (width, paymentWidth, dailyWidth, paymentHeight, dailyHeight, editorHeight, paired, listsPaired, minHeight);
    }
}
