using System.Data;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class ProjectAccessForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private sealed record AccountChoice(DataRow Row) { public override string ToString() => Row["username"].ToString()!; }
    private readonly Guid project;
    private readonly byte[] projectVersion;
    private readonly ComboBox account = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly DataGridView grants = new() { Dock = DockStyle.Fill, Name = "ProjectGrants", AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly CheckBox responsible = Check("Proje sorumlusu"), manage = Check("Ekip ve atama düzenleme"), costs = Check("Maliyet görüntüleme"), export = Check("Proje raporu CSV alma");
    private readonly CheckBox prepare = Check("Ödeme hazırla"), approve = Check("Ödeme onayla / reddet");
    private readonly CheckBox plan = Check("Vardiya / izin talebi planla"), leaveApproval = Check("İzin onayla / reddet / iptal et");
    private readonly CheckBox timeEntry = Check("Puantaj girişi / gönderme"), timeApproval = Check("Puantaj onayı / iade / yeniden açma");
    private Guid? grant; private byte[]? grantVersion;
    private ProjectAccessService Service => new(AppDatabase.ConnectionString, User);
    private static CheckBox Check(string caption) => new() { Text = caption, AutoSize = true };
    internal ProjectAccessForm(Guid project, byte[] version, UserSession actor)
    {
        this.project = project; projectVersion = version.ToArray(); Session = actor; PersistGridLayout = false;
        Text = "Muhasebe — Proje sorumlusu ve yetkiler"; ClientSize = new(1000, 620); RecordSelection.Configure(grants);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.AutoSize)); layout.RowStyles.Add(new(SizeType.AutoSize));
        layout.Controls.Add(new Label { Dock = DockStyle.Fill, AutoSize = true, Text = "Yalnız etkin Proje kullanıcısı hesapları atanabilir. Üyelik proje/ekip/atama okuma izni verir.\nSorumluluk ek işlem yetkisi vermez. Bir projede en fazla bir etkin sorumlu bulunur.\nGenel rollerin merkez erişimi sürer; bu ekran onları kısıtlı role dönüştürmez." }, 0, 0);
        layout.SizeChanged += (_,_) => { foreach(var label in layout.Controls.OfType<Label>())label.MaximumSize=new Size(Math.Max(1,layout.ClientSize.Width-layout.Padding.Horizontal),0); };
        layout.Controls.Add(grants, 0, 1);
        var editor = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        editor.Controls.AddRange([account, responsible, manage, costs, export, prepare, approve, plan, leaveApproval, timeEntry, timeApproval]); layout.Controls.Add(editor, 0, 2);
        var commands = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        Button add = new() { Text = "Yeni yetki", AutoSize = true }, edit = new() { Text = "Seçili yetkiyi düzenle", AutoSize = true }, save = new() { Text = "Kaydet", AutoSize = true }, revoke = new() { Text = "Seçili yetkiyi geri al", AutoSize = true }, refresh = new() { Text = "Yenile", AutoSize = true }, close = new() { Text = "Kapat", AutoSize = true };
        commands.Controls.AddRange([add, edit, save, revoke, refresh, close]); layout.Controls.Add(commands, 0, 3); Controls.Add(layout);
        prepare.CheckedChanged += (_, _) => { if (prepare.Checked) costs.Checked = true; }; approve.CheckedChanged += (_, _) => { if (approve.Checked) costs.Checked = true; };
        TrackChanges("Proje yetkileri", account, responsible, manage, costs, export, prepare, approve, plan, leaveApproval, timeEntry, timeApproval);
        add.Click += (_, _) => { if (ConfirmDiscardChanges("Yeni yetki")) Reset(); };
        edit.Click += (_, _) => Edit(); save.Click += async (_, _) => await SaveAsync(); revoke.Click += async (_, _) => await RevokeAsync();
        refresh.Click += async (_, _) => { if (ConfirmDiscardChanges("Yetkileri yenileme")) await ReloadAsync(); }; close.Click += (_, _) => Close();
        Load += async (_, _) => await ReloadAsync(); RefreshAction = ReloadAsync;
        SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(680, 460)); }
    protected override void RefreshEditState() => account.Enabled = !IsOperationRunning && grant == null;
    private DataRow? Selected => grants.SelectedRows.Count == 1 && grants.SelectedRows[0].DataBoundItem is DataRowView r ? r.Row : null;
    private void Reset() { grant = null; grantVersion = null; account.Enabled = true; account.SelectedIndex = -1; responsible.Checked = manage.Checked = costs.Checked = export.Checked = prepare.Checked = approve.Checked = plan.Checked = leaveApproval.Checked = timeEntry.Checked = timeApproval.Checked = false; AcceptTrackedChanges(); }
    private async Task ReloadAsync() => await RunOperationAsync(async ct =>
    {
        var accounts = await Service.AccountsAsync(ct); var data = await Service.GrantsAsync(project, ct); if (IsDisposed) return;
        account.Items.Clear(); foreach (DataRow row in accounts.Rows) account.Items.Add(new AccountChoice(row)); grants.DataSource = data;
        foreach (DataGridViewColumn c in grants.Columns) if (c.ValueType == typeof(Guid) || c.ValueType == typeof(byte[])) c.Visible = false;
        foreach (var (name, title) in new[] { ("username", "Hesap"), ("IsActive", "Yetki etkin"), ("AccountActive", "Hesap etkin"), ("IsResponsible", "Sorumlu"), ("CanManageWorkforce", "Ekip/atama düzenleme"), ("CanViewCosts", "Maliyet"), ("CanExport", "CSV raporu"), ("CanPreparePayments", "Ödeme hazırla"), ("CanApprovePayments", "Ödeme onayla"), ("CanPlanShifts", "Takvim planla"), ("CanApproveLeaves", "İzin onayla"), ("CanEnterTimesheets", "Puantaj gir"), ("CanApproveTimesheets", "Puantaj onayla"), ("CreatedUtc", "Verilme UTC"), ("CreatedBy", "Veren") }) grants.Columns[name]!.HeaderText = title;
        grants.ClearSelection(); Reset();
    });
    private void Edit()
    {
        var row = Selected; if (row == null || !row.Field<bool>("IsActive") || !ConfirmDiscardChanges("Yetki seçimini değiştirme")) return;
        var choice = account.Items.Cast<AccountChoice>().FirstOrDefault(a => a.Row.Field<Guid>("AccountId") == row.Field<Guid>("AccountId"));
        if (choice == null) { MessageBox.Show(this, "Hesap silinmiş/pasif veya hesap listesi yenilenmeli. Eski yetkiyi geri alabilirsiniz."); return; }
        grant = row.Field<Guid>("GrantId"); grantVersion = ((byte[])row["RecordVersion", DataRowVersion.Original]).ToArray(); account.SelectedItem = choice; account.Enabled = false;
        responsible.Checked = row.Field<bool>("IsResponsible"); manage.Checked = row.Field<bool>("CanManageWorkforce"); costs.Checked = row.Field<bool>("CanViewCosts"); export.Checked = row.Field<bool>("CanExport"); prepare.Checked = row.Field<bool>("CanPreparePayments"); approve.Checked = row.Field<bool>("CanApprovePayments"); plan.Checked = row.Field<bool>("CanPlanShifts"); leaveApproval.Checked = row.Field<bool>("CanApproveLeaves"); timeEntry.Checked = row.Field<bool>("CanEnterTimesheets"); timeApproval.Checked = row.Field<bool>("CanApproveTimesheets"); AcceptTrackedChanges();
    }
    private async Task SaveAsync()
    {
        if (account.SelectedItem is not AccountChoice choice) return;
        var row = choice.Row;
        var request = new Wire.ProjectGrantWrite(project, projectVersion.ToArray(), grant, grantVersion?.ToArray(), row.Field<Guid>("AccountId"), ((byte[])row["SessionVersion"]).ToArray(), row.Field<Guid>("SessionEpoch"), responsible.Checked, manage.Checked, costs.Checked, export.Checked, "", prepare.Checked, approve.Checked, plan.Checked, leaveApproval.Checked, timeEntry.Checked, timeApproval.Checked);
        if (await RunChangeOperationAsync(ct => Service.SaveAsync(request with { Reason = RemoteApi.Reason }, ct), "Proje yetkileri kaydedildi.", "Proje yetkileri")) await ReloadAsync();
    }
    private async Task RevokeAsync()
    {
        var row = Selected; if (row == null || !row.Field<bool>("IsActive") || !ConfirmDiscardChanges("Yetkiyi geri alma")) return;
        var request = new Wire.ProjectGrantRevoke(project, projectVersion.ToArray(), row.Field<Guid>("GrantId"), ((byte[])row["RecordVersion", DataRowVersion.Original]).ToArray(), "");
        if (await RunChangeOperationAsync(ct => Service.RevokeAsync(request with { Reason = RemoteApi.Reason }, ct), "Proje yetkisi geri alındı.")) await ReloadAsync();
    }
}
