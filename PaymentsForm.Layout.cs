namespace WinFormsApp2;

internal sealed partial class PaymentsForm
{
    private void ArrangePayments(Button reverse, Button receipt, Button closing, Button inspect)
    {
        Text = "Muhasebe — Ödemeler"; ClientSize = new Size(1180, 740);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 246)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Personel ödemeleri", "Üst listeden personel, alt defterden fiş veya iptal işlemi için ödeme seçin."), 0, 0);
        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65)); cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35)); cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var payment = new GroupBox { Text = "Ödeme ve sonuç kontrolü", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4 };
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); entries.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        entries.Controls.Add(new Label { Text = "Tutar (TL):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        amount.Dock = DockStyle.Fill; amount.Margin = new Padding(3, 9, 8, 3); amount.TabIndex = 0; entries.Controls.Add(amount, 1, 0);
        void Action(TableLayoutPanel table, Button button, int column, int row, int order)
        {
            button.AutoSize = false; button.Dock = DockStyle.Fill; button.Margin = new Padding(3, 5, 3, 5); button.TabIndex = order;
            table.Controls.Add(button, column, row);
        }
        Action(entries, pay, 2, 0, 1); Action(entries, receipt, 1, 1, 2); Action(entries, reverse, 2, 1, 3);
        paymentState.AutoSize = false; paymentState.MaximumSize = Size.Empty; paymentState.Dock = DockStyle.Fill; paymentState.TextAlign = ContentAlignment.MiddleLeft;
        paymentState.Text = "Ödeme sonucu kontrol ediliyor…"; entries.Controls.Add(paymentState, 0, 2); entries.SetColumnSpan(paymentState, 3);
        var recovery = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        recovery.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); recovery.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); recovery.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Action(recovery, inspect, 0, 0, 0); Action(recovery, acknowledge, 1, 0, 1); entries.Controls.Add(recovery, 0, 3); entries.SetColumnSpan(recovery, 3);
        payment.Controls.Add(entries); cards.Controls.Add(payment, 0, 0);
        var daily = new GroupBox { Text = "Günlük kapanış ve fiş", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8) };
        var closingFields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5 };
        closingFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); closingFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        closingFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); closingFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); closingFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); closingFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); closingFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        closingFields.Controls.Add(new Label { Text = "Kapanış günü:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        day.Dock = DockStyle.Fill; day.Margin = new Padding(3, 6, 3, 3); day.TabIndex = 0; closingFields.Controls.Add(day, 1, 0);
        closingFields.Controls.Add(new Label { Text = "Beyan (TL):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        declared.Dock = DockStyle.Fill; declared.Margin = new Padding(3, 6, 3, 3); declared.TabIndex = 1; closingFields.Controls.Add(declared, 1, 1);
        thermal.Text = "Fişleri 80 mm yazıcıya göre hazırla"; thermal.Dock = DockStyle.Fill; thermal.TabIndex = 2; closingFields.Controls.Add(thermal, 0, 2); closingFields.SetColumnSpan(thermal, 2);
        Action(closingFields, closing, 0, 3, 3); closingFields.SetColumnSpan(closing, 2);
        daily.Controls.Add(closingFields); cards.Controls.Add(daily, 1, 0); outer.Controls.Add(cards, 0, 1);
        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        lists.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); lists.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        void List(DataGridView grid, string caption, int row)
        {
            var group = new GroupBox { Text = caption, Dock = DockStyle.Fill, Padding = new Padding(8, 20, 8, 8) };
            grid.RowHeadersWidth = 28; group.Controls.Add(grid); lists.Controls.Add(group, 0, row);
        }
        List(people, "Personel listesi — ödeme yapılacak kişiyi seçin", 0); List(ledger, "Hesap defteri — fiş veya iptal için ödeme satırı seçin", 1);
        outer.Controls.Add(lists, 0, 2); Controls.Add(outer);
        // Visible columns change only presentation; guarded actions retain the full DataRow.
        people.AutoGenerateColumns = false;
        foreach (var (name, caption, width) in new[] { ("TC", "TC kimlik no", 120), ("AD", "Adı", 170), ("SOYAD", "Soyadı", 170), ("IBAN", "IBAN", 290), ("PARA", "Kalan (TL)", 140), ("GRUP", "Grup", 100) })
            people.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = width, Width = width });
        ledger.AutoGenerateColumns = false;
        foreach (var (name, caption, width) in new[]
        {
            ("TC", "TC kimlik no", 120), ("PersonnelGroup", "Grup", 80), ("CreatedUtc", "Kayıt (UTC)", 155), ("Kind", "İşlem", 120),
            ("Delta", "Hareket (TL)", 120), ("RunningBalance", "Kalan (TL)", 120), ("Actor", "Kullanıcı", 130), ("ReversedUtc", "İptal (UTC)", 155)
        })
            ledger.Columns.Add(new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, MinimumWidth = width, Width = width });
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
        Load += (_, _) => MinimumSize = LogicalToDeviceUnits(new Size(1000, 740));
        AppTheme.Apply(this);
    }
}
