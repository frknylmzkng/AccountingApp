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
        form.Text = "Muhasebe — " + title; form.ClientSize = new Size(1040, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 3, ColumnCount = 1 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(Banner(title, "Kayıt seçerek düzenleyin veya yeni kayıt ekleyin."), 0, 0);
        var editor = new PersonnelEditor(labels, fields, actions) { Name = "jokerPersonnelEditor" };
        var list = new GroupBox { Name = "jokerRecords", Text = "Personel listesi — satır seçerek düzenleyin", Padding = new Padding(8, 20, 8, 8), TabIndex = 1 };
        records.Dock = DockStyle.Fill; records.RowHeadersWidth = 28; records.Margin = Padding.Empty;
        // Joker has no leave workflow. Bind only its personnel columns while
        // retaining the original DataRow (including its version) for guarded edits.
        records.AutoGenerateColumns = false; records.Columns.Clear();
        records.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        foreach (var (name, caption, width, type) in new[]
        {
            ("TC", "TC kimlik no", 110, typeof(long)), ("AD", "Adı", 100, typeof(string)), ("SOYAD", "Soyadı", 110, typeof(string)),
            ("IBAN", "IBAN", 220, typeof(string)), ("PARA", "Kalan (TL)", 105, typeof(decimal)), ("GRUP", "Grup", 65, typeof(string))
        })
        {
            var column = new DataGridViewTextBoxColumn { Name = name, DataPropertyName = name, HeaderText = caption,
                MinimumWidth = records.LogicalToDeviceUnits(width), FillWeight = width, ValueType = type };
            if (name == "PARA") { column.DefaultCellStyle.Format = "N2"; column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight; }
            records.Columns.Add(column);
        }
        list.Controls.Add(records);
        var workspace = new JokerPersonnelWorkspace(editor, list) { Dock = DockStyle.Fill, Margin = Padding.Empty, TabIndex = 0 };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toEditor = new Button { Name = "jokerShowEditor", Text = "Personel &bilgileri", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toList = new Button { Name = "jokerShowList", Text = "Personel &listesi", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 1 };
        toEditor.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toList.Click += (_, _) => workspace.ScrollControlIntoView(list);
        navigation.Controls.Add(toEditor); navigation.Controls.Add(toList);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2);
        form.Controls.Add(outer); form.ResumeLayout(true);
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
        var leave = new GeneralLeaveEditor(label7, label6, dtpBaslangic, dtpBitis, izinKaydet_btn, izinDurumLabel);
        ArrangeGeneralLayout(leave);
    }
    private void ArrangeGeneralLayout(GeneralLeaveEditor leave)
    {
        SuspendLayout();
        var retained = new Control[] { label5, label2, label4, label3, label8, TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt, ekle_btn, edit_btn, sil_btn, temizle_btn, calisanView };
        foreach (var control in retained) control.Parent?.Controls.Remove(control);
        var discarded = Controls.Cast<Control>().ToArray(); Controls.Clear();
        foreach (var control in discarded) control.Dispose();
        Text = "Muhasebe — Genel Personel"; ClientSize = new Size(1120, 650);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        outer.Controls.Add(ClassicLayouts.Banner("Genel Personel", "Bilgileri düzenleyin; izin tarihlerini ayrıca kaydedin."), 0, 0);
        var personnel = new PersonnelEditor(
            [label5, label2, label4, label3, label8], [TC_txt, ad_txt, soyad_txt, IBAN_txt, money_txt],
            [ekle_btn, edit_btn, sil_btn, temizle_btn]) { Name = "generalPersonnelEditor" };
        leave.Name = "generalLeaveCard"; leave.Dock = DockStyle.None; leave.Anchor = AnchorStyles.Top | AnchorStyles.Left; leave.TabIndex = 1;
        var list = new GroupBox { Name = "generalRecords", Text = "Personel listesi — düzenleme ve izin için bir satır seçin", Padding = new Padding(8, 20, 8, 8), TabIndex = 2 };
        calisanView.Dock = DockStyle.Fill; calisanView.RowHeadersWidth = 28; calisanView.Margin = Padding.Empty;
        calisanView.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        calisanView.ColumnAdded += (_, e) =>
        {
            var (caption, width) = e.Column.DataPropertyName switch
            {
                "TC" => ("TC kimlik no", 110), "AD" => ("Adı", 100), "SOYAD" => ("Soyadı", 110), "IBAN" => ("IBAN", 220),
                "PARA" => ("Kalan (TL)", 105), "GRUP" => ("Grup", 65), "IZINBASLANGIC" => ("İzin başlangıcı", 110),
                "IZINBITIS" => ("İzin bitişi", 110), "IzinSuresi" => ("Gün", 55), _ => (e.Column.HeaderText, 100)
            };
            e.Column.HeaderText = caption;
            e.Column.MinimumWidth = calisanView.LogicalToDeviceUnits(width);
            e.Column.FillWeight = width;
            if (e.Column.DataPropertyName == "PARA") e.Column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        };
        // Defaults are applied before the existing saved column order/visibility
        // is restored. Internal row versions remain in their original bound row.
        list.Controls.Add(calisanView);
        var workspace = new GeneralPersonnelWorkspace(personnel, leave, list) { Dock = DockStyle.Fill, TabIndex = 0, Margin = Padding.Empty };
        var navigation = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty, TabIndex = 1 };
        var toEditor = new Button { Name = "generalShowEditor", Text = "&Bilgi ve izin", Width = 140, Height = 28, Margin = new Padding(3, 4, 6, 4), TabIndex = 0 };
        var toList = new Button { Name = "generalShowList", Text = "Personel &listesi", Width = 140, Height = 28, Margin = new Padding(3, 4, 3, 4), TabIndex = 1 };
        toEditor.Click += (_, _) => workspace.AutoScrollPosition = Point.Empty;
        toList.Click += (_, _) => workspace.ScrollControlIntoView(list);
        navigation.Controls.Add(toEditor); navigation.Controls.Add(toList);
        outer.Controls.Add(navigation, 0, 1); outer.Controls.Add(workspace, 0, 2);
        Controls.Add(outer); ResumeLayout(true);
    }
}
public partial class Joker
{
    private void ArrangePersonnel()
    {
        var clear = new Button { Name = "jokerClear" }; clear.Click += (_, _) => Temizle();
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
    private Label? accountShortcutDescription;
    private void ArrangeDashboard()
    {
        SuspendLayout();
        foreach (var control in new Control[] { button1, button2, button3, button4, button5, button6, logout_btn, userLabel }) control.Parent?.Controls.Remove(control);
        Controls.Clear(); panel1.Dispose(); panel2.Dispose(); panel3.Dispose();
        Text = "Muhasebe — Ana Ekran"; ClientSize = new Size(1040, 640);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 3 };
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        outer.Controls.Add(ClassicLayouts.Banner("Muhasebe Yönetimi", "Personel • İzin • Ödeme"), 0, 0);
        button1.Text = "Genel &personel"; button2.Text = "&Joker personel"; button3.Text = "&Kişi ara";
        button5.Text = "Genel &durum"; button4.Text = "&Yönetici hesapları"; button6.Text = "İle&tişim";
        var payments = new Button { Text = "&Ödemeler" }; payments.Click += (_, _) => RequestPage(AppPage.Payments);
        var leave = new Button { Text = "&İzin planı" }; leave.Click += (_, _) => RequestPage(AppPage.LeavePlan);
        var navigation = new GroupBox { Name = "dashboardNavigation", Text = "Bölümler", Padding = new Padding(8, 20, 8, 8), TabIndex = 0 };
        navigation.Controls.Add(shortcuts);
        var cards = new[]
        {
            new DashboardActionCard("Personel", button1, "Genel personel bilgileri ve izinler."),
            new DashboardActionCard("Personel", button2, "Joker personel listesi ve kayıt bilgileri."),
            new DashboardActionCard("Arama", button3, "TC, ad veya soyada göre personel bulun."),
            new DashboardActionCard("İzin", leave, "Seçili personellerin izin dönemleri."),
            new DashboardActionCard("Ödeme", payments, "Ödeme hareketleri, fiş ve günlük kapanış."),
            new DashboardActionCard("Rapor", button5, "Personel sayıları ve güncel bakiye özeti."),
            new DashboardActionCard("Hesap", button4, "Yönetici hesapları ve erişim işlemleri."),
            new DashboardActionCard("Destek", button6, "İletişim ve destek bilgileri.")
        };
        accountShortcutDescription = cards[6].Description;
        outer.Controls.Add(new DashboardWorkspace(navigation, cards) { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 8) }, 0, 1);
        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        userLabel.Dock = DockStyle.Fill; userLabel.TextAlign = ContentAlignment.MiddleLeft; userLabel.AutoEllipsis = true;
        logout_btn.Text = "Oturumu &kapat"; logout_btn.Dock = DockStyle.None; logout_btn.Size = new Size(134, 28); logout_btn.Margin = new Padding(3, 0, 3, 0); logout_btn.Anchor = AnchorStyles.Right; logout_btn.TabStop = true;
        footer.Controls.Add(userLabel, 0, 0); footer.Controls.Add(logout_btn, 1, 0); outer.Controls.Add(footer, 0, 2); Controls.Add(outer);
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
