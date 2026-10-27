namespace WinFormsApp2;

internal sealed partial class PaymentsForm
{
    private void ArrangePayments(Button reverse, Button receipt, Button closing, Button inspect)
    {
        Text = "Muhasebe — Ödemeler"; ClientSize = new Size(1180, 780);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Personel ödemeleri", "Personel ve ödeme seçimini ilgili listeden yapın."), 0, 0);
        pay.Name = "paymentSave"; reverse.Name = "paymentReverse"; receipt.Name = "paymentReceipt";
        inspect.Name = "paymentInspect"; acknowledge.Name = "paymentAcknowledge"; paymentState.Name = "paymentState";
        closing.Name = "paymentClosing"; day.Name = "closingDay"; thermal.Name = "receiptThermal";
        var payment = new PaymentEntryCard(amount, pay, receipt, reverse, paymentState, inspect, acknowledge);
        var daily = new PaymentClosingCard(day, declared, thermal, closing);
        GroupBox List(DataGridView grid, string name, string caption, int order)
        {
            var group = new GroupBox { Name = name, Text = caption, Padding = new Padding(8, 20, 8, 8), TabIndex = order };
            grid.RowHeadersWidth = 28; grid.Dock = DockStyle.Fill; group.Controls.Add(grid); return group;
        }
        var peopleList = List(people, "paymentPeopleList", "Personel listesi — ödeme yapılacak kişiyi seçin", 2);
        var ledgerList = List(ledger, "paymentLedgerList", "Hesap defteri — fiş veya iptal için ödeme seçin", 3);
        var workspace = new PaymentsWorkspace(payment, daily, peopleList, ledgerList) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var targets = new[] { ("paymentsShowEntry", "Ödeme ve fiş", (Control)payment), ("paymentsShowClosing", "Günlük kapanış", daily),
            ("paymentsShowPeople", "Personel listesi", peopleList), ("paymentsShowLedger", "Hesap defteri", ledgerList) };
        foreach (var (name, caption, target) in targets)
        {
            var button = new Button { Name = name, Text = caption, Width = 118, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = navigation.Controls.Count };
            button.Click += (_, _) => { if (ReferenceEquals(target, payment)) workspace.AutoScrollPosition = Point.Empty; else workspace.ScrollControlIntoView(target); };
            navigation.Controls.Add(button);
        }
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2); Controls.Add(outer);
        // Visible columns change only presentation; guarded actions retain the full DataRow.
        people.AutoGenerateColumns = false; people.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (var (name, caption, width) in new[] { ("TC", "TC kimlik no", 110), ("AD", "Adı", 100), ("SOYAD", "Soyadı", 110), ("IBAN", "IBAN", 220), ("PARA", "Kalan (TL)", 105), ("GRUP", "Grup", 65) })
            people.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = LogicalToDeviceUnits(width), FillWeight = width });
        ledger.AutoGenerateColumns = false; ledger.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (var (name, caption, width) in new[]
        {
            ("TC", "TC kimlik no", 110), ("PersonnelGroup", "Grup", 70), ("CreatedUtc", "Kayıt (UTC)", 145), ("Kind", "İşlem", 110),
            ("Delta", "Hareket (TL)", 110), ("RunningBalance", "Kalan (TL)", 110), ("Actor", "Kullanıcı", 120), ("ReversedUtc", "İptal (UTC)", 145)
        })
            ledger.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = LogicalToDeviceUnits(width), FillWeight = width });
        foreach (var column in new[] { people.Columns["PARA"]!, ledger.Columns["Delta"]!, ledger.Columns["RunningBalance"]! })
        { column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; column.DefaultCellStyle.Format = "N2"; }
        ledger.CellFormatting += (_, e) =>
        {
            if (e.ColumnIndex < 0) return;
            string property = ledger.Columns[e.ColumnIndex].DataPropertyName;
            if (e.Value is DateTime date) { e.Value = date.ToString("dd.MM.yyyy HH:mm"); e.FormattingApplied = true; }
            else if (property == "PersonnelGroup" && e.Value is int group) { e.Value = group == (int)PersonnelGroup.Joker ? "Joker" : "Genel"; e.FormattingApplied = true; }
            else if (property == "Kind" && e.Value is string kind) { e.Value = kind switch { "Payment" => "Ödeme", "Reversal" => "Ödeme iptali", "Allocation" => "Bakiye değişimi", "Closure" => "Kayıt kapanışı", _ => kind }; e.FormattingApplied = true; }
        };
        AppTheme.Apply(this);
    }
}
