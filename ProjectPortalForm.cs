using System.Data;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class ProjectPortalForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private sealed record ProjectChoice(DataRow Row) { internal Guid Id => Row.Field<Guid>("ProjectId"); public override string ToString() => Row["Code"] + " · " + Row["Name"]; }
    private sealed record TeamChoice(Guid? Id, byte[]? Version, string Caption) { public override string ToString() => Caption; }
    private readonly ComboBox project = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320 }, team = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230 };
    private readonly DataGridView people = Grid("ScopedPeople"), assignments = Grid("ScopedAssignments"), teams = Grid("ScopedTeams"), costs = Grid("ScopedCosts");
    private readonly Label note = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new(8) }, totals = new() { AutoSize = true, Dock = DockStyle.Top, Padding = new(8) };
    private readonly CheckBox supplementary = new() { Text = "Gün içi ek proje/ekip ataması", AutoSize = true };
    private readonly DateTimePicker start = new() { Format = DateTimePickerFormat.Short, Width = 140 }, end = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 140 };
    private readonly Button assign = Button("Atamayı kaydet"), cancel = Button("Seçili atamayı iptal et"), addTeam = Button("Yeni ekip…"), editTeam = Button("Seçili ekibi düzenle…"), loadCosts = Button("Maliyetleri yükle"), exportAssignments = Button("Atama raporu CSV…"), exportCosts = Button("Maliyet belgeleri CSV…");
    private readonly Button workHours=Button("Plan / gerçekleşen kişi-saat…");
    private readonly Button calendar = Button("Vardiya ve izin takvimi…");
    private readonly Button wageRules=Button("Ücret/gider kural ve talep geçmişi…");
    private readonly Button fiscal=Button("Kesin mali dönem kayıtları…");
    private readonly Button budget=Button("Proje bütçesi / maliyet sapması…");
    private readonly Button reconciliation=Button("Proje mutabakat raporu…");
    private readonly Button allocations=new(){Text="Ödeme / avans tahsisi…",AutoSize=true};
    private readonly Button accrualFoundation=Button("Tarihli tarife / dönem hesabı…");
    private readonly Button paymentQueue = Button("Ödeme hazırlama / onay…");
    private readonly ListPager peoplePager, assignmentPager;
    private ProjectChoice? chosen; private bool loading;
    private ProjectWorkforceService Workforce => new(AppDatabase.ConnectionString, User);
    private ProjectAccessService Access => new(AppDatabase.ConnectionString, User);
    private static Button Button(string caption) => new() { Text = caption, AutoSize = true };
    private static DataGridView Grid(string name) { var g = new DataGridView { Name = name, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells }; RecordSelection.Configure(g); return g; }
    private static DataRow? Row(DataGridView g) => g.SelectedRows.Count == 1 && g.SelectedRows[0].DataBoundItem is DataRowView r ? r.Row : null;
    private static FlowLayoutPanel Bar(params Control[] controls) { var p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(8) }; p.Controls.AddRange(controls); return p; }
    private static Label Label(string caption) => new() { Text = caption, AutoSize = true, Margin = new(4, 7, 4, 0) };
    internal ProjectPortalForm()
    {
        PersistGridLayout = false; Text = "Muhasebe — Projelerim"; ClientSize = new(1120, 760);
        var tabs = new TabControl { Dock = DockStyle.Fill }; TabPage pool = new("Proje personeli"), roster = new("Atamalar"), groups = new("Ekipler"), finance = new("Maliyetler"); tabs.TabPages.AddRange([pool, roster, groups, finance]);
        pool.Controls.Add(people); pool.Controls.Add(Bar(Label("Ekip"), team, Label("Başlangıç"), start, Label("Bitiş (dahil)"), end, supplementary, assign));
        roster.Controls.Add(assignments); roster.Controls.Add(Bar(cancel, exportAssignments)); groups.Controls.Add(teams); groups.Controls.Add(Bar(addTeam, editTeam));
        finance.Controls.Add(costs); finance.Controls.Add(totals); finance.Controls.Add(Bar(loadCosts, exportCosts, paymentQueue, accrualFoundation, allocations,reconciliation,budget,fiscal,wageRules)); Controls.Add(tabs); Controls.Add(note);
        var timesheets = Button("Puantaj ve amir onayı…"); var refresh = Button("Yenile"); var password = Button("Parolamı değiştir"); var logout = Button("Oturumu kapat"); var lockSession = Button("Kilitle");
        var reportCenter=Button("Rapor merkezi…");reportCenter.Click+=(_,_)=>{if(IsOperationRunning||chosen==null||!ConfirmDiscardChanges("Rapor merkezini açma"))return;using var d=new ReportCenterForm(chosen.Id,User,HasRight("CanExport"),HasRight("CanViewCosts"),HasRight("CanPreparePayments")||HasRight("CanApprovePayments"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        Controls.Add(Bar(Label("Proje"), project, refresh, calendar, timesheets, workHours,reportCenter, password, lockSession, logout));
        peoplePager = ListPager.Attach(people, this, LoadPeopleAsync); assignmentPager = ListPager.Attach(assignments, this, LoadAssignmentsAsync);
        foreach (var g in new[] { people, assignments, teams, costs }) g.DataBindingComplete += (_, _) =>
        {
            var titles = new Dictionary<string,string> { ["IsSupplementary"]="Gün içi ek atama",["AD"]="Ad",["SOYAD"]="Soyad",["Code"]="Kod",["Name"]="Ad",["TeamName"]="Ekip",["StartDate"]="Başlangıç",["EndDate"]="Bitiş",["IsActive"]="Etkin",["CreatedUtc"]="Kayıt UTC",["CreatedBy"]="Kaydeden",["CancelledUtc"]="İptal UTC",["CancelledBy"]="İptal eden",["Kind"]="Tür",["Reference"]="Belge no",["PeriodStart"]="Dönem başlangıcı",["PeriodEnd"]="Dönem bitişi",["Amount"]="Tutar TL",["Description"]="Açıklama",["ReversedUtc"]="İptal UTC",["ReversedBy"]="İptal eden" };
            foreach(var pair in titles) if(g.Columns.Contains(pair.Key)) g.Columns[pair.Key]!.HeaderText=pair.Value;
            foreach (DataGridViewColumn c in g.Columns) if (c.ValueType == typeof(Guid) || c.ValueType == typeof(byte[]) || c.Name is "TC" or "PersonnelGroup") c.Visible = false;
        };
        TrackChanges("Atama", team, start, end, supplementary); GuardRecordSelection(people);
        people.SelectionChanged += (_, _) => { if (!loading && !IsOperationRunning) { start.Value = DateTime.Today; end.Checked = false; supplementary.Checked=false; AcceptTrackedChanges(); } };
        project.SelectedIndexChanged += async (_, _) =>
        {
            if (loading) return;
            if (!ConfirmDiscardChanges("Projeyi değiştirme")) { loading = true; project.SelectedItem = chosen; loading = false; return; }
            chosen = project.SelectedItem as ProjectChoice; await OpenAsync();
        };
        refresh.Click += async (_, _) => { if (ConfirmDiscardChanges("Projeleri yenileme")) await ReloadAsync(); }; RefreshAction = ReloadAsync;
        password.Click += (_, _) => RequestPage(AppPage.PasswordChange); logout.Click += (_, _) => RequestLogout(); lockSession.Click += (_, _) => RequestSessionLock();
        assign.Click += async (_, _) => await AssignAsync(); cancel.Click += async (_, _) => await CancelAsync(); addTeam.Click += async (_, _) => await TeamAsync(false); editTeam.Click += async (_, _) => await TeamAsync(true);
        workHours.Click+=(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Proje kişi-saat karşılaştırması"))return;using var d=new ProjectWorkHoursForm(chosen.Id,User,HasRight("CanExport"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        timesheets.Click += async (_, _) => { if(chosen==null||!ConfirmDiscardChanges("Puantaj"))return;using var d=new TimesheetForm(chosen.Id,User,HasRight("CanEnterTimesheets"),HasRight("CanApproveTimesheets"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await OpenAsync(); };
        calendar.Click += async (_, _) => { if (chosen == null || !ConfirmDiscardChanges("Takvim")) return; using var d = new ScheduleCalendarForm(chosen.Id, User, HasRight("CanPlanShifts"), HasRight("CanApproveLeaves")); d.SessionRevoked += (_, _) => NotifySessionRevoked(); d.ShowDialog(this); if (!IsDisposed) await OpenAsync(); };
        paymentQueue.Click += async (_, _) => { if (chosen == null || !ConfirmDiscardChanges("Proje ödemeleri")) return; using var dialog = new ProjectPaymentsForm(chosen.Id, User, HasRight("CanPreparePayments"), HasRight("CanApprovePayments")); dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this); if (!IsDisposed) await OpenAsync(); };
        wageRules.Click+=(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Ücret/gider geçmişi"))return;using var d=new WageRulesForm(chosen.Id,User);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        fiscal.Click+=(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Kesin mali dönem kayıtları"))return;using var d=new FiscalPeriodForm(chosen.Id,User,HasRight("CanExport"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        budget.Click+=(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Proje bütçesi"))return;using var d=new ProjectBudgetForm(chosen.Id,User,HasRight("CanExport"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        reconciliation.Click+=(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Proje mutabakat raporu"))return;using var d=new ProjectReconciliationForm(chosen.Id,User,HasRight("CanExport"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        allocations.Click+=async(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Ödeme/avans tahsisi"))return;using var d=new PaymentAllocationsForm(chosen.Id,User,HasRight("CanApprovePayments"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await OpenAsync();};
        accrualFoundation.Click+=async(_,_)=>{if(chosen==null||!ConfirmDiscardChanges("Dönem hesabı"))return;using var d=new AccrualFoundationForm(chosen.Id,User);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await OpenAsync();};
        loadCosts.Click += async (_, _) => await CostsAsync(); exportAssignments.Click += async (_, _) => await ExportAsync("Assignments"); exportCosts.Click += async (_, _) => await ExportAsync("Costs");
        ClientSizeChanged += (_, _) => { note.MaximumSize = totals.MaximumSize = new Size(Math.Max(1, ClientSize.Width - 30), 0); };
        Load += async (_, _) => await ReloadAsync();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(740, 520)); }
    private bool HasRight(string name) => chosen?.Row.Field<bool>(name) == true;
    protected override void RefreshEditState()
    {
        bool ready = !IsOperationRunning && chosen != null;
        assign.Enabled = cancel.Enabled = addTeam.Enabled = editTeam.Enabled = team.Enabled = start.Enabled = end.Enabled = supplementary.Enabled = ready && HasRight("CanManageWorkforce");
        calendar.Enabled = ready;workHours.Enabled=ready;
        wageRules.Enabled=ready&&HasRight("CanViewCosts")&&Session?.CanViewSalary==true;
        fiscal.Enabled=ready&&HasRight("CanViewCosts")&&(HasRight("CanPreparePayments")||HasRight("CanApprovePayments"))&&Session?.CanViewSalary==true;
        budget.Enabled=ready&&HasRight("CanViewCosts")&&Session?.CanViewSalary==true;
        reconciliation.Enabled=ready&&HasRight("CanViewCosts")&&(HasRight("CanPreparePayments")||HasRight("CanApprovePayments"))&&Session?.CanViewSalary==true;
        allocations.Enabled=ready&&HasRight("CanViewCosts")&&(HasRight("CanPreparePayments")||HasRight("CanApprovePayments"));
        paymentQueue.Enabled = ready && (HasRight("CanPreparePayments") || HasRight("CanApprovePayments"));
        accrualFoundation.Enabled=ready&&HasRight("CanViewCosts")&&Session?.CanViewSalary==true;
        loadCosts.Enabled = ready && HasRight("CanViewCosts"); exportAssignments.Enabled = ready && HasRight("CanExport"); exportCosts.Enabled = ready && HasRight("CanViewCosts") && HasRight("CanExport");
        peoplePager?.UpdateState(); assignmentPager?.UpdateState();
    }
    private void ClearData() { people.DataSource = assignments.DataSource = teams.DataSource = costs.DataSource = null; team.Items.Clear(); totals.Text = ""; AcceptTrackedChanges(); }
    private async Task ReloadAsync()
    {
        Guid? prior = chosen?.Id; chosen = null; ClearData();
        if (!await RunOperationAsync(async ct =>
        {
            var data = await Workforce.ProjectsAsync(ct); if (IsDisposed) return;
            loading = true; project.Items.Clear(); foreach (DataRow row in data.Rows) project.Items.Add(new ProjectChoice(row));
            chosen = project.Items.Cast<ProjectChoice>().FirstOrDefault(p => p.Id == prior) ?? project.Items.Cast<ProjectChoice>().FirstOrDefault(); project.SelectedItem = chosen; loading = false;
            note.Text = chosen == null ? "Atanmış proje yok. Merkez yöneticiniz proje yetkisi vermelidir." : "Yalnız yetkili olduğunuz projeler. Personel havuzuna ilk atamayı merkez yöneticisi yapar; TC, IBAN ve genel bakiyeler bu ekranda bulunmaz.";
        })) return;
        await OpenAsync();
    }
    private async Task OpenAsync()
    {
        ClearData(); peoplePager.Reset(); assignmentPager.Reset(); var p = chosen; if (p == null) return;
        if (!await RunOperationAsync(async ct =>
        {
            var data = await Workforce.TeamsAsync(p.Id, ct); if (IsDisposed) return; teams.DataSource = data; teams.ClearSelection();
            team.Items.Add(new TeamChoice(null, null, "Ekipsiz atama")); foreach (DataRow row in data.Rows) if (row.Field<bool>("IsActive")) team.Items.Add(new TeamChoice(row.Field<Guid>("TeamId"), ((byte[])row["RecordVersion"]).ToArray(), row["Code"] + " · " + row["Name"])); team.SelectedIndex = 0;
            note.Text = p + " · Sorumlu: " + (p.Row["ResponsibleUser"] is DBNull ? "atanmamış" : p.Row["ResponsibleUser"].ToString()); AcceptTrackedChanges();
        })) return;
        await LoadPeopleAsync(); await LoadAssignmentsAsync();
    }
    private async Task LoadPeopleAsync() { var p = chosen; if (p == null) return; await RunOperationAsync(async ct => { var page = await Access.PeopleAsync(p.Id, peoplePager.Index, peoplePager.SizeLimit, ct); if (!IsDisposed && chosen == p) { loading = true; people.DataSource = page.Rows; people.ClearSelection(); peoplePager.Apply(page); loading = false; } }); }
    private async Task LoadAssignmentsAsync() { var p = chosen; if (p == null) return; await RunOperationAsync(async ct => { var page = await Workforce.AssignmentsAsync(p.Id, assignmentPager.Index, assignmentPager.SizeLimit, ct); if (!IsDisposed && chosen == p) { assignments.DataSource = page.Rows; assignments.ClearSelection(); assignmentPager.Apply(page); } }); }
    private async Task AssignAsync()
    {
        var p = chosen; var r = Row(people); if (p == null || r == null || team.SelectedItem is not TeamChoice t) return;
        var request = new Wire.AssignmentWrite(p.Id, ((byte[])p.Row["RecordVersion"]).ToArray(), t.Id, t.Version?.ToArray(), r.Field<Guid>("PersonId"), (Wire.PersonnelGroup)r.Field<int>("PersonnelGroup"), 0, ((byte[])r["RecordVersion"]).ToArray(), DateTime.SpecifyKind(start.Value.Date, DateTimeKind.Unspecified), end.Checked ? DateTime.SpecifyKind(end.Value.Date, DateTimeKind.Unspecified) : null, "", supplementary.Checked);
        if (await RunChangeOperationAsync(ct => Workforce.AssignAsync(request with { Reason = RemoteApi.Reason }, ct), "Atama kaydedildi.", "Atama")) await LoadAssignmentsAsync();
    }
    private async Task CancelAsync()
    {
        var r = Row(assignments); if (r == null || !ConfirmDiscardChanges("Atamayı iptal etme")) return;
        var request = new Wire.AssignmentCancel(r.Field<Guid>("AssignmentId"), ((byte[])r["RecordVersion"]).ToArray(), "");
        if (await RunChangeOperationAsync(ct => Workforce.CancelAsync(request with { Reason = RemoteApi.Reason }, ct), "Atama iptal edildi.")) await LoadAssignmentsAsync();
    }
    private async Task TeamAsync(bool edit)
    {
        if (!ConfirmDiscardChanges("Ekip düzenleme")) return;
        var p = chosen; var r = edit ? Row(teams) : null; if (p == null || (edit && r == null)) return;
        using var dialog = new Form { Text = edit ? "Ekibi düzenle" : "Yeni ekip", ClientSize = new(520, 190), FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, StartPosition = FormStartPosition.CenterParent };
        TextBox code = new() { Width = 150, MaxLength = 30, Text = r?["Code"].ToString() ?? "", ReadOnly = edit }, name = new() { Width = 240, MaxLength = 100, Text = r?["Name"].ToString() ?? "" };
        var active = new CheckBox { Text = "Etkin", Checked = r?.Field<bool>("IsActive") ?? true, AutoSize = true }; var ok = Button("Kaydet"); ok.DialogResult = DialogResult.OK; dialog.Controls.Add(Bar(Label("Kod"), code, Label("Ad"), name, active, ok)); dialog.AcceptButton = ok; AppTheme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var request = new Wire.TeamWrite(p.Id, r?.Field<Guid>("TeamId"), r == null ? null : ((byte[])r["RecordVersion"]).ToArray(), code.Text, name.Text, active.Checked, "");
        if (await RunChangeOperationAsync(ct => Workforce.SaveTeamAsync(request with { Reason = RemoteApi.Reason }, ct), "Ekip kaydedildi.")) await OpenAsync();
    }
    private async Task CostsAsync()
    {
        var p = chosen; if (p == null) return; costs.DataSource = null; totals.Text = "";
        await RunOperationAsync(async ct => { var rows = await new ProjectFinanceService(AppDatabase.ConnectionString, User).EntriesAsync(p.Id, ct); var summary = await new ProjectFinanceService(AppDatabase.ConnectionString, User).ProjectsAsync(ct); if (IsDisposed) return; costs.DataSource = rows; costs.ClearSelection(); var r = summary.AsEnumerable().SingleOrDefault(r => r.Field<Guid>("ProjectId") == p.Id); if (r != null) totals.Text = $"Toplam proje maliyeti: {r["TotalCost"]:N2} TL · Belgeler: en yeni 500 kayıt"; });
    }
    private async Task ExportAsync(string report)
    {
        var p = chosen; if (p == null) return; using var dialog = new SaveFileDialog { Filter = "CSV raporu|*.csv", DefaultExt = "csv", FileName = "Proje-" + p.Row["Code"] + "-" + report + ".csv" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunChangeOperationAsync(ct => new ProjectReportService(AppDatabase.ConnectionString, User).SaveCsvAsync(p.Id, report, dialog.FileName, ct), "Proje raporu kaydedildi.");
    }
}
