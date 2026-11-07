namespace WinFormsApp2;

internal sealed class AccountPermissionsDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly AccountTarget target;
    private readonly bool administrator;
    private readonly CheckBox pay = new() { Text = "Ödeme ve bakiye işlemleri", AutoSize = true };
    private readonly CheckBox export = new() { Text = "Kişisel veri dışa aktarma (Excel/CSV ve yazdırma)", AutoSize = true };
    private readonly CheckBox salary = new() { Text = "Ücret / bakiye / finansal tutarları görüntüle", AutoSize = true };
    private readonly CheckBox iban = new() { Text = "IBAN görüntüle", AutoSize = true };
    private readonly Button save = new() { Text = "Yetkileri kaydet", AutoSize = true };
    internal bool SessionInvalidated { get; private set; }
    internal AccountPermissionsDialog(UserSession actor, AccountTarget selected, bool administrator, bool canPay, bool canExportPersonalData, bool canViewSalary, bool canViewIban)
    {
        Session = actor; target = AccountTarget.RequireSnapshot(selected); this.administrator = administrator;
        Text = "Muhasebe — Ek hesap yetkileri"; ClientSize = new(680, 440); ShowInTaskbar = false; PersistGridLayout = false;
        pay.Checked = administrator || canPay; export.Checked = administrator || canExportPersonalData;
        salary.Checked = administrator || canViewSalary; iban.Checked = administrator || canViewIban;
        TrackChanges("Hesap yetkileri", pay, export, salary, iban);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new(16), ColumnCount = 1, RowCount = 7 };
        var heading = new Label { Text = "Hesap: " + target.Username, AutoSize = true, Margin = new(0, 0, 0, 14) };
        var note = new Label { AutoSize = true, Margin = new(0, 14, 0, 14), Text = administrator
            ? "Yönetici bu yetkilerin tamamına sahiptir. Yönetici yetkileri bu ekrandan azaltılamaz."
            : "Bu yetkiler personel düzenlemeden bağımsızdır. Ödeme için ücret görüntüleme de gerekir. Genel Excel/CSV/pano aktarımı her iki görüntüleme iznini gerektirir. Yetki değişikliği hesabın açık oturumlarını sonlandırır; yeniden giriş gerekir." };
        var commands = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true };
        var cancel = new Button { Text = "Vazgeç", AutoSize = true }; cancel.Click += (_, _) => { if (!IsOperationRunning) Close(); };
        commands.Controls.AddRange([save, cancel]);
        content.Controls.Add(heading, 0, 0); content.Controls.Add(pay, 0, 1); content.Controls.Add(export, 0, 2); content.Controls.Add(salary, 0, 3); content.Controls.Add(iban, 0, 4); content.Controls.Add(note, 0, 5); content.Controls.Add(commands, 0, 6); Controls.Add(content);
        void Fit() { int width = Math.Max(1, content.ClientSize.Width - content.Padding.Horizontal - 24); heading.MaximumSize = note.MaximumSize = new(width, 0); }
        content.SizeChanged += (_, _) => Fit(); Fit();
        salary.CheckedChanged += (_, _) => { if (!salary.Checked) pay.Checked = false; };
        pay.CheckedChanged += (_, _) => { if (pay.Checked) salary.Checked = true; };
        save.Click += Save; PageRequested += (_, _) => Close();
        SessionRevoked += (_, _) => { SessionInvalidated = true; Close(); };
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(540, 420)); RefreshEditState(); }
    protected override void RefreshEditState()
    {
        bool allowed = Session?.Role == UserRole.Admin && !administrator && !IsOperationRunning;
        pay.Enabled = export.Enabled = salary.Enabled = iban.Enabled = save.Enabled = allowed;
    }
    private async void Save(object? sender, EventArgs e)
    {
        bool payment = pay.Checked, personalExport = export.Checked, salaryView = salary.Checked, ibanView = iban.Checked;
        if (await RunChangeOperationAsync(async token =>
        {
            pay.Enabled = export.Enabled = false;
            await new AccountService(AppDatabase.ConnectionString).SetPermissionsAsync(User, target, payment, personalExport, salaryView, ibanView, token);
        }, savedGroup: "Hesap yetkileri"))
        { DialogResult = DialogResult.OK; Close(); }
    }
}
