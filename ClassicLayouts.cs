namespace WinFormsApp2;

internal static class ClassicLayouts
{
    internal static void Personnel(Form form, string title, DataGridView records, Label[] labels, TextBox[] fields, Button[] actions)
    {
        form.SuspendLayout();
        // Keep the original controls and their handlers/selection guards; only
        // move their layout. No data or editing semantics change here.
        var retained = labels.Cast<Control>().Concat(fields).Concat(actions).Append(records).ToArray();
        foreach (var c in retained) c.Parent?.Controls.Remove(c);
        var discarded = form.Controls.Cast<Control>().ToArray(); form.Controls.Clear();
        foreach (var c in discarded) c.Dispose();
        form.Text = "Muhasebe — " + title; form.ClientSize = new Size(1040, 570);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 204)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(Banner(title, "Düzenlemek için listeden bir satır seçin. Yeni kayıt için alanları temizleyin."), 0, 0);
        var card = new GroupBox { Text = "Personel bilgileri", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 8, 3, 8) };
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        entries.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Field(int index, string caption, int column, int row, int order)
        {
            labels[index].Text = caption; labels[index].AutoSize = false; labels[index].Dock = DockStyle.Fill; labels[index].TextAlign = ContentAlignment.MiddleLeft; labels[index].TabIndex = order * 2;
            fields[index].Dock = DockStyle.Fill; fields[index].Margin = new Padding(3, 6, 8, 3); fields[index].TabIndex = order * 2 + 1;
            entries.Controls.Add(labels[index], column, row); entries.Controls.Add(fields[index], column + 1, row);
        }
        Field(0, "TC kimlik no:", 0, 0, 0); Field(4, "Kalan (TL):", 2, 0, 1);
        Field(1, "Adı:", 0, 1, 2); Field(2, "Soyadı:", 2, 1, 3);
        Field(3, "IBAN:", 0, 2, 4); entries.SetColumnSpan(fields[3], 3);
        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 4, 0, 0) };
        commands.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        string[] names = ["&Ekle", "&Düzenle", "&Sil", "Alanları &temizle"];
        for (int i = 0; i < actions.Length; i++)
        {
            actions[i].Text = names[i]; actions[i].AutoSize = false; actions[i].Dock = DockStyle.Fill; actions[i].TabStop = true; actions[i].TabIndex = i;
            commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); actions[i].Margin = new Padding(3);
            commands.Controls.Add(actions[i], i, 0);
        }
        entries.Controls.Add(commands, 0, 3); entries.SetColumnSpan(commands, 4); card.Controls.Add(entries); outer.Controls.Add(card, 0, 1);
        var list = new GroupBox { Text = "Personel listesi — satır seçerek düzenleyin", Dock = DockStyle.Fill, Padding = new Padding(8, 20, 8, 8) };
        records.Dock = DockStyle.Fill; records.RowHeadersWidth = 28; records.Margin = Padding.Empty;
        // Joker has no leave workflow. Bind only its personnel columns while
        // retaining the original DataRow (including its version) for guarded edits.
        records.AutoGenerateColumns = false; records.Columns.Clear();
        foreach (var (name, caption, width, type) in new[]
        {
            ("TC", "TC kimlik no", 110, typeof(long)), ("AD", "Adı", 160, typeof(string)), ("SOYAD", "Soyadı", 160, typeof(string)),
            ("IBAN", "IBAN", 270, typeof(string)), ("PARA", "Kalan (TL)", 120, typeof(decimal)), ("GRUP", "Grup", 80, typeof(string))
        })
        {
            var column = new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption, Width = width, ValueType = type };
            if (name == "PARA") { column.DefaultCellStyle.Format = "N2"; column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
            records.Columns.Add(column);
        }
        list.Controls.Add(records); outer.Controls.Add(list, 0, 2); form.Controls.Add(outer); form.ResumeLayout(true);
    }
    internal static Panel Banner(string title, string subtitle)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Tag = AppTheme.TitleTag, Padding = new Padding(14, 8, 8, 6) };
        var lines = new TableLayoutPanel { Dock = DockStyle.Fill, Tag = AppTheme.TitleTag, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        lines.RowStyles.Add(new RowStyle(SizeType.Percent, 60)); lines.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
        lines.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, Font = AppTheme.TitleFont, Tag = AppTheme.TitleTag }, 0, 0);
        lines.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Fill, Tag = AppTheme.TitleTag }, 0, 1);
        panel.Controls.Add(lines);
        return panel;
    }
}

public partial class Genel
{
    private void ArrangePersonnel()
    {
        var leave = new GroupBox { Text = "Seçili personelin izni", Width = 338, Height = 208, Padding = new Padding(12, 20, 12, 8), Margin = new Padding(3, 10, 3, 3) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        label7.Text = "Başlangıç:"; label6.Text = "Bitiş:"; label7.AutoSize = label6.AutoSize = false;
        label7.Dock = label6.Dock = DockStyle.Fill; label7.TextAlign = label6.TextAlign = ContentAlignment.MiddleLeft;
        dtpBaslangic.Dock = dtpBitis.Dock = DockStyle.Fill; dtpBaslangic.Margin = dtpBitis.Margin = new Padding(3, 5, 3, 3);
        izinKaydet_btn.Text = "İzni &kaydet"; izinKaydet_btn.AutoSize = false; izinKaydet_btn.Dock = DockStyle.Fill;
        izinDurumLabel.Dock = DockStyle.Fill; izinDurumLabel.AutoSize = false;
        table.Controls.Add(label7, 0, 0); table.Controls.Add(dtpBaslangic, 1, 0); table.Controls.Add(label6, 0, 1); table.Controls.Add(dtpBitis, 1, 1);
        table.Controls.Add(izinKaydet_btn, 0, 2); table.SetColumnSpan(izinKaydet_btn, 2);
        table.Controls.Add(izinDurumLabel, 0, 3); table.SetColumnSpan(izinDurumLabel, 2); leave.Controls.Add(table);
        ArrangeGeneralLayout(leave);
    }

    private void ArrangeGeneralLayout(GroupBox leave)
    {
        SuspendLayout();
        var retained = new Control[] { label5, label2, label4, label3, label8, TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt, ekle_btn, edit_btn, sil_btn, temizle_btn, calisanView };
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — Genel Personel"; ClientSize = new Size(1120, 610);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 204)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Genel Personel", "Personel bilgileri ve izin işlemleri üstte; kayıt listesi alttadır."), 0, 0);
        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 8, 0, 8) };
        top.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        var card = new GroupBox { Text = "Personel bilgileri", Dock = DockStyle.Fill, Padding = new Padding(12, 18, 12, 8), Margin = new Padding(3, 3, 8, 3) };
        var entries = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 4 };
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        entries.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); entries.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) entries.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        entries.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        void Field(Label label, string title, TextBox field, int column, int row, int order)
        {
            label.Text = title; label.AutoSize = false; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.TabIndex = order * 2;
            field.Dock = DockStyle.Fill; field.Margin = new Padding(3, 6, 8, 3); field.TabIndex = order * 2 + 1;
            entries.Controls.Add(label, column, row); entries.Controls.Add(field, column + 1, row);
        }
        Field(label5, "TC kimlik no:", TC_txt, 0, 0, 0); Field(label8, "Kalan (TL):", money_txt, 2, 0, 1);
        Field(label2, "Adı:", ad_txt, 0, 1, 2); Field(label4, "Soyadı:", soyad_txt, 2, 1, 3);
        Field(label3, "IBAN:", IBAN_txt, 0, 2, 4); entries.SetColumnSpan(IBAN_txt, 3);
        var commands = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = new Padding(0, 4, 0, 0) };
        commands.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var buttons = new[] { ekle_btn, edit_btn, sil_btn, temizle_btn }; string[] captions = ["&Ekle", "&Düzenle", "&Sil", "Alanları &temizle"];
        for (int i = 0; i < buttons.Length; i++)
        {
            commands.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            buttons[i].Text = captions[i]; buttons[i].Dock = DockStyle.Fill; buttons[i].AutoSize = false; buttons[i].Margin = new Padding(3); buttons[i].TabIndex = i;
            commands.Controls.Add(buttons[i], i, 0);
        }
        entries.Controls.Add(commands, 0, 3); entries.SetColumnSpan(commands, 4); card.Controls.Add(entries);
        leave.Dock = DockStyle.Fill; leave.Margin = new Padding(4, 3, 3, 3);
        top.Controls.Add(card, 0, 0); top.Controls.Add(leave, 1, 0); outer.Controls.Add(top, 0, 1);
        var list = new GroupBox { Text = "Personel listesi — düzenleme ve izin için bir satır seçin", Dock = DockStyle.Fill, Padding = new Padding(8, 18, 8, 8) };
        calisanView.Dock = DockStyle.Fill; calisanView.RowHeadersWidth = 28; calisanView.Margin = Padding.Empty;
        calisanView.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 96), "AD" => ("Adı", 105), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 215),
                "PARA" => ("Kalan (TL)", 100), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 70), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption; e.Column.Width = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        // Set defaults when columns are created, before saved user column widths
        // are restored by the normal binding lifecycle.
        list.Controls.Add(calisanView); outer.Controls.Add(list, 0, 2); Controls.Add(outer); ResumeLayout(true);
    }
}

public partial class Joker
{
    private void ArrangePersonnel()
    {
        var clear = new Button(); clear.Click += (_, _) => Temizle();
        ClassicLayouts.Personnel(this, "Joker Personel", jokerView, [label5, label2, label4, label3, label6], [TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt], [ekle_btn, edit_btn, sil_btn, clear]);
    }
}

public partial class Giris
{
    private void ArrangeLogin()
    {
        SuspendLayout(); Controls.Clear(); Text = "Muhasebe — Oturum Aç";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; WindowState = FormWindowState.Normal;
        MinimumSize = Size.Empty; ClientSize = new Size(620, 400);
        var outer = new TableLayoutPanel { Name = "loginCard", Dock = DockStyle.Fill, Padding = new Padding(14), RowCount = 5, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        outer.Controls.Add(ClassicLayouts.Banner("MUHASEBE", "Personel, izin ve ödeme takibi"), 0, 0);
        outer.Controls.Add(new Label { Text = "Devam etmek için kullanıcı bilgilerinizi girin.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        var group = new GroupBox { Text = "Oturum aç", Dock = DockStyle.Fill, Padding = new Padding(14, 20, 14, 8) };
        var fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3 };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        label5.Text = "&Kullanıcı adı:"; label4.Text = "&Parola:";
        label5.Dock = label4.Dock = DockStyle.Fill; label5.TextAlign = label4.TextAlign = ContentAlignment.MiddleLeft;
        label5.AutoSize = label4.AutoSize = false;
        username_txt.Dock = password_txt.Dock = DockStyle.Fill;
        username_txt.Margin = password_txt.Margin = new Padding(3, 7, 3, 3);
        label5.TabIndex = 0; username_txt.TabIndex = 1; label4.TabIndex = 2; password_txt.TabIndex = 3;
        giris_btn.Size = new Size(112, 30); giris_btn.Anchor = AnchorStyles.Right; giris_btn.TabIndex = 4;
        fields.Controls.Add(label5, 0, 0); fields.Controls.Add(username_txt, 1, 0);
        fields.Controls.Add(label4, 0, 1); fields.Controls.Add(password_txt, 1, 1); fields.Controls.Add(giris_btn, 1, 2);
        group.Controls.Add(fields); outer.Controls.Add(group, 0, 2);
        statusLabel.Dock = DockStyle.Fill; statusLabel.TextAlign = ContentAlignment.MiddleLeft; statusLabel.Padding = new Padding(2, 6, 2, 0);
        outer.Controls.Add(statusLabel, 0, 3);
        outer.Controls.Add(new Label { Text = "Enter: Giriş     Tab: Sonraki alan", Dock = DockStyle.Fill, BorderStyle = BorderStyle.Fixed3D, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
        Controls.Add(outer); ResumeLayout(true);
    }
}

public partial class Dashboard
{
    private readonly TreeView shortcuts = new() { Dock = DockStyle.Fill, ShowLines = true, ShowRootLines = true, FullRowSelect = true, HideSelection = false };
    private void ArrangeDashboard()
    {
        SuspendLayout();
        foreach (var control in new Control[] { button1, button2, button3, button4, button5, button6, logout_btn, userLabel }) control.Parent?.Controls.Remove(control);
        Controls.Clear(); panel1.Dispose(); panel2.Dispose(); panel3.Dispose();
        Text = "Muhasebe — Ana Ekran"; ClientSize = new Size(940, 510); MinimumSize = new Size(800, 560);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        outer.Controls.Add(ClassicLayouts.Banner("Muhasebe Yönetimi", "Personel • İzin • Ödeme"), 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 10, 0, 8) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var navigation = new GroupBox { Text = "Bölümler", Dock = DockStyle.Fill, Padding = new Padding(8, 18, 8, 8) };
        navigation.Controls.Add(shortcuts); body.Controls.Add(navigation, 0, 0);
        var tasks = new FlowLayoutPanel { Name = "dashboardTasks", Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Margin = new Padding(12, 3, 3, 3) };
        button1.Text = "Genel &personel"; button2.Text = "&Joker personel"; button3.Text = "&Kişi ara";
        button5.Text = "Genel &durum"; button4.Text = "&Yönetici hesapları"; button6.Text = "İle&tişim";
        var payments = new Button { Text = "&Ödemeler" }; payments.Click += (_, _) => RequestPage(AppPage.Payments);
        var leave = new Button { Text = "&İzin planı" }; leave.Click += (_, _) => RequestPage(AppPage.LeavePlan);
        void AddGroup(string title, Button[] buttons)
        {
            var group = new GroupBox { Text = title, Width = 670, Height = buttons.Length > 2 ? 124 : 76, Padding = new Padding(12, 18, 12, 10), Margin = new Padding(0, 0, 0, 10) };
            int rows = (buttons.Length + 1) / 2;
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = rows };
            for (int i = 0; i < 2; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < rows; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i]; button.Dock = DockStyle.None; button.AutoSize = false; button.Size = new Size(200, 32); button.Anchor = AnchorStyles.Left | AnchorStyles.Right; button.Margin = new Padding(4);
                button.TextAlign = ContentAlignment.MiddleLeft; button.Padding = new Padding(12, 0, 4, 0); button.TabStop = true; button.TabIndex = i;
                grid.Controls.Add(button, i % 2, i / 2);
            }
            group.Controls.Add(grid);
            group.Layout += (_, _) =>
            {
                int buttonHeight = (int)Math.Round(32 * DeviceDpi / 96f);
                int height = group.Height - group.DisplayRectangle.Height + rows * (buttonHeight + buttons.Max(b => b.Margin.Vertical));
                if (group.Height != height) group.Height = height;
                foreach (var button in buttons) if (button.Height != buttonHeight) button.Height = buttonHeight;
            };
            tasks.Controls.Add(group);
        }
        AddGroup("Personel ve izin", [button1, button2, button3, leave]);
        AddGroup("Ödeme ve rapor", [payments, button5]);
        AddGroup("Hesap ve destek", [button4, button6]);
        tasks.Layout += (_, _) =>
        {
            int maximum = (int)Math.Round(700 * DeviceDpi / 96f);
            int width = Math.Min(maximum, Math.Max(1, tasks.ClientSize.Width));
            foreach (var group in tasks.Controls.OfType<GroupBox>()) if (group.Width != width) group.Width = width;
        };
        body.Controls.Add(tasks, 1, 0); outer.Controls.Add(body, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        userLabel.Dock = DockStyle.Fill; userLabel.TextAlign = ContentAlignment.MiddleLeft;
        logout_btn.Text = "Oturumu &kapat"; logout_btn.Dock = DockStyle.None; logout_btn.Size = new Size(134, 28); logout_btn.Margin = new Padding(3, 0, 3, 0); logout_btn.Anchor = AnchorStyles.Right; logout_btn.TabStop = true;
        footer.Controls.Add(userLabel, 0, 0); footer.Controls.Add(logout_btn, 1, 0); outer.Controls.Add(footer, 0, 2); Controls.Add(outer);
        // Finish the native tree selection notification before disposing the
        // current screen during navigation.
        shortcuts.AfterSelect += (_, e) => { if (e.Node?.Tag is AppPage page) BeginInvoke(() => { if (!IsDisposed) RequestPage(page); }); };
        ResumeLayout(true);
    }
    private void PopulateShortcuts()
    {
        shortcuts.Nodes.Clear();
        var people = shortcuts.Nodes.Add("Personel");
        people.Nodes.Add(new TreeNode("Genel personel") { Tag = AppPage.General });
        people.Nodes.Add(new TreeNode("Joker personel") { Tag = AppPage.Joker });
        people.Nodes.Add(new TreeNode("Kişi ara") { Tag = AppPage.Search });
        var work = shortcuts.Nodes.Add("Takip ve rapor");
        work.Nodes.Add(new TreeNode("Ödemeler") { Tag = AppPage.Payments });
        work.Nodes.Add(new TreeNode("İzin planı") { Tag = AppPage.LeavePlan });
        work.Nodes.Add(new TreeNode("Genel durum") { Tag = AppPage.Summary });
        if (Session?.Role == UserRole.Admin)
        {
            var admin = shortcuts.Nodes.Add("Yönetim");
            admin.Nodes.Add(new TreeNode("Yönetici hesapları") { Tag = AppPage.Admin });
            admin.Nodes.Add(new TreeNode("Ayarlar ve yedekleme") { Tag = AppPage.Settings });
        }
        shortcuts.ExpandAll();
    }
}
