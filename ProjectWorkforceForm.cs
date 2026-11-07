using System.Data;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class ProjectWorkforceForm : SessionForm
{
    private sealed record ProjectTarget(Guid Id, byte[] Version);
    private sealed record TeamTarget(Guid Id, byte[] Version);
    private readonly DataGridView projects = Grid("WorkforceProjects"), teams = Grid("ProjectTeams"), assignments = Grid("ProjectAssignments");
    private readonly TextBox name = new() { Width = 210, MaxLength = 100 }, site = new() { Width = 210, MaxLength = 100 }, customer = new() { Width = 210, MaxLength = 100 };
    private readonly TextBox teamCode = new() { Width = 130, MaxLength = 30 }, teamName = new() { Width = 210, MaxLength = 100 };
    private readonly CheckBox active = new() { Text = "Proje etkin", Checked = true, AutoSize = true }, teamActive = new() { Text = "Ekip etkin", Checked = true, AutoSize = true };
    private readonly DateTimePicker projectStart = Date(true), projectEnd = Date(true), assignmentStart = Date(false), assignmentEnd = Date(true);
    private readonly CheckBox supplementary = new() { Text = "Gün içi ek proje/ekip ataması", AutoSize = true };
    private readonly ComboBox assignmentTeam = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };
    private readonly Label chosen = new() { Text = "Personel seçilmedi", AutoSize = true }, context = new() { Text = "Listeden projeyi seçip Projeyi aç düğmesine basın.", Dock = DockStyle.Top, Height = 28 };
    private readonly ListPager pager;
    private ProjectTarget? projectTarget; private TeamTarget? teamTarget; private PersonnelChoice? person;
    private ProjectWorkforceService Service => new(AppDatabase.ConnectionString, User);
    private sealed record TeamChoice(Guid? Id, byte[]? Version, string Caption) { public override string ToString() => Caption; }
    private static DateTimePicker Date(bool optional) => new() { Format = DateTimePickerFormat.Short, ShowCheckBox = optional, Checked = !optional, Width = 140 };
    private static DateTime? Value(DateTimePicker d) => d.ShowCheckBox && !d.Checked ? null : DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Unspecified);
    private static DataGridView Grid(string title) => new() { Name = title, Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private static DataRow? Row(DataGridView grid) => grid.SelectedRows.Count == 1 && (grid.SelectedRows[0].DataBoundItem is DataRowView row) ? row.Row : null;
    private static FlowLayoutPanel Bar(params Control[] controls) { var p = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new Padding(6) }; p.Controls.AddRange(controls); return p; }
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(5, 8, 2, 0) };
    internal ProjectWorkforceForm()
    {
        Text = "Muhasebe — Proje, ekip ve personel atamaları"; ClientSize = new Size(1150, 780);
        var tabs = new TabControl { Dock = DockStyle.Fill }; TabPage p = new("Projeler"), t = new("Ekipler"), a = new("Tarihli asıl atamalar"); tabs.TabPages.AddRange([p, t, a]);
        Button open = new() { Text = "Projeyi aç", AutoSize = true }, create = new() { Text = "Yeni proje…", AutoSize = true }, saveProject = new() { Text = "Proje bilgilerini kaydet", AutoSize = true };
        p.Controls.Add(projects); p.Controls.Add(Bar(Label("Ad"), name, Label("Saha"), site, Label("Müşteri etiketi"), customer, Label("Başlangıç"), projectStart, Label("Bitiş"), projectEnd, active, saveProject)); var access = new Button { Text = "Proje sorumlusu ve yetkiler…", AutoSize = true };
        access.Click += async (_, _) => { var target = projectTarget; if(target==null || !ConfirmDiscardChanges("Proje yetkileri"))return;using var dialog=new ProjectAccessForm(target.Id,target.Version,User);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed){ResetContext();await LoadProjectsAsync();} };
        var paymentQueue = new Button { Text = "Proje ödeme hazırlama / onay…", AutoSize = true };
        paymentQueue.Click += async (_, _) => { var target = projectTarget; if (target == null || !ConfirmDiscardChanges("Proje ödemeleri")) return; using var dialog = new ProjectPaymentsForm(target.Id, User, User.Role == UserRole.Admin, User.Role == UserRole.Admin); dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this); if (!IsDisposed) await LoadProjectsAsync(); };
        var calendar = new Button { Text = "Vardiya ve izin takvimi…", AutoSize = true };
        calendar.Click += async (_, _) => { var target = projectTarget; if (target == null || !ConfirmDiscardChanges("Takvim")) return; using var d = new ScheduleCalendarForm(target.Id, User, User.Role == UserRole.Admin, User.Role == UserRole.Admin); d.SessionRevoked += (_, _) => NotifySessionRevoked(); d.ShowDialog(this); if (!IsDisposed) await LoadProjectsAsync(); };
        var timesheets = new Button { Text = "Puantaj ve amir onayı…", AutoSize = true };
        timesheets.Click += async (_, _) => { var target=projectTarget;if(target==null||!ConfirmDiscardChanges("Puantaj"))return;using var d=new TimesheetForm(target.Id,User,User.Role==UserRole.Admin,User.Role==UserRole.Admin);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await LoadProjectsAsync(); };
        p.Controls.Add(Bar(open, create, access, paymentQueue, calendar, timesheets));
        Button editTeam = new() { Text = "Ekibi düzenle", AutoSize = true }, newTeam = new() { Text = "Yeni ekip", AutoSize = true }, saveTeam = new() { Text = "Ekibi kaydet", AutoSize = true };
        t.Controls.Add(teams); t.Controls.Add(Bar(editTeam, newTeam, Label("Kod"), teamCode, Label("Ad"), teamName, teamActive, saveTeam));
        Button choose = new() { Text = "Personel seç…", AutoSize = true }, assign = new() { Text = "Atamayı kaydet", AutoSize = true }, cancel = new() { Text = "Seçili atamayı iptal et", AutoSize = true };
        a.Controls.Add(assignments); a.Controls.Add(Bar(choose, chosen, Label("Ekip"), assignmentTeam, Label("Başlangıç"), assignmentStart, Label("Bitiş (dahil)"), assignmentEnd, supplementary, assign, cancel));
        Controls.Add(tabs); Controls.Add(context); pager = ListPager.Attach(assignments, this, LoadAssignmentsAsync);
        foreach (var grid in new[] { projects, teams, assignments }) { RecordSelection.Configure(grid); grid.DataBindingComplete += (_, _) => { if(grid.Columns.Contains("IsSupplementary"))grid.Columns["IsSupplementary"]!.HeaderText="Gün içi ek atama";foreach (DataGridViewColumn c in grid.Columns) if (c.ValueType == typeof(byte[]) || c.ValueType == typeof(Guid)) c.Visible = false; }; }
        TrackChanges("Proje", name, site, customer, projectStart, projectEnd, active); TrackChanges("Ekip", teamCode, teamName, teamActive); TrackChanges("Atama", assignmentTeam, assignmentStart, assignmentEnd, chosen, supplementary);
        open.Click += async (_, _) => await OpenAsync(); create.Click += async (_, _) => await CreateAsync(); saveProject.Click += async (_, _) => await SaveProjectAsync();
        newTeam.Click += (_, _) => { if (!ConfirmDiscardChanges("Yeni ekip")) return; teamTarget = null; teamCode.ReadOnly = false; teamCode.Clear(); teamName.Clear(); teamActive.Checked = true; AcceptTrackedChanges("Ekip"); };
        editTeam.Click += (_, _) => { var row = Row(teams); if (row == null || !ConfirmDiscardChanges("Ekip seçimini değiştirme")) return; teamTarget = new(row.Field<Guid>("TeamId"), ((byte[])row["RecordVersion"]).ToArray()); teamCode.Text = row.Field<string>("Code"); teamCode.ReadOnly = true; teamName.Text = row.Field<string>("Name"); teamActive.Checked = row.Field<bool>("IsActive"); AcceptTrackedChanges("Ekip"); };
        saveTeam.Click += async (_, _) => await SaveTeamAsync();
        choose.Click += (_, _) => { if (!ConfirmDiscardChanges("Personel seçimini değiştirme")) return; using var dialog = new PersonnelPickerDialog(User); if (dialog.ShowDialog(this) == DialogResult.OK) { person = dialog.Selected; chosen.Text = person == null ? "Personel seçilmedi" : person.Caption + " · " + person.Tc; MarkTrackedChanges(); } if (dialog.SessionInvalidated) NotifySessionRevoked(); };
        assign.Click += async (_, _) => await AssignAsync(); cancel.Click += async (_, _) => await CancelAsync();
        Load += async (_, _) => { foreach (var b in new[] { access, create, saveProject, newTeam, editTeam, saveTeam, choose, assign, cancel }) b.Enabled = User.Role == UserRole.Admin; await LoadProjectsAsync(); };
        RefreshAction = async () => { if (ConfirmDiscardChanges("Projeleri yenileme")) { ResetContext(); await LoadProjectsAsync(); } };
    }
    private void ResetContext() { projectTarget = null; teamTarget = null; person = null; supplementary.Checked=false; chosen.Text = "Personel seçilmedi"; teams.DataSource = null; assignments.DataSource = null; assignmentTeam.Items.Clear(); name.Clear(); site.Clear(); customer.Clear(); teamCode.Clear(); teamName.Clear(); teamCode.ReadOnly = false; context.Text = "Listeden projeyi açın."; AcceptTrackedChanges(); }
    private async Task LoadProjectsAsync() => await RunOperationAsync(async ct => { var data = await Service.ProjectsAsync(ct); if (!IsDisposed) { projects.DataSource = data; projects.ClearSelection(); } });
    private async Task OpenAsync()
    {
        var row = Row(projects); if (row == null || !ConfirmDiscardChanges("Projeyi değiştirme")) return;
        var target = new ProjectTarget(row.Field<Guid>("ProjectId"), ((byte[])row["RecordVersion"]).ToArray());
        if (await RunOperationAsync(async ct => { var data = await Service.TeamsAsync(target.Id, ct); if (IsDisposed) return; ResetContext(); projectTarget = target; name.Text = row.Field<string>("Name"); site.Text = row["Site"].ToString(); customer.Text = row["CustomerLabel"].ToString(); active.Checked = row.Field<bool>("IsActive"); SetDate(projectStart, row["StartDate"]); SetDate(projectEnd, row["EndDate"]); context.Text = "Açık proje: " + row["Code"] + " · " + row["Name"]; BindTeams(data); pager.Reset(); AcceptTrackedChanges(); })) await LoadAssignmentsAsync();
    }
    private static void SetDate(DateTimePicker control, object value) { control.Checked = value != DBNull.Value; if (value is DateTime date) control.Value = date; }
    private void BindTeams(DataTable data) { var previous = assignmentTeam.SelectedItem as TeamChoice; teams.DataSource = data; teams.ClearSelection(); assignmentTeam.Items.Clear(); assignmentTeam.Items.Add(new TeamChoice(null, null, "Ekipsiz proje ataması")); foreach (DataRow row in data.Rows) if (row.Field<bool>("IsActive")) assignmentTeam.Items.Add(new TeamChoice(row.Field<Guid>("TeamId"), ((byte[])row["RecordVersion"]).ToArray(), row["Code"] + " · " + row["Name"])); assignmentTeam.SelectedIndex = previous == null ? 0 : -1; for (int i = 0; i < assignmentTeam.Items.Count; i++) if (assignmentTeam.Items[i] is TeamChoice choice && previous != null && choice.Id == previous.Id) { assignmentTeam.SelectedIndex = i; break; } }
    private async Task LoadAssignmentsAsync() { var target = projectTarget; if (target == null) return; await RunOperationAsync(async ct => { var page = await Service.AssignmentsAsync(target.Id, pager.Index, pager.SizeLimit, ct); if (!IsDisposed && projectTarget == target) { assignments.DataSource = page.Rows; assignments.ClearSelection(); pager.Apply(page); } }); }
    private async Task SaveProjectAsync() { var target = projectTarget; if (target == null || !ConfirmDiscardChanges("Proje kaydından sonra ekranı yenileme", "Proje")) return; var request = new Wire.ProjectDetails(target.Id, target.Version, name.Text, site.Text, customer.Text, Value(projectStart), Value(projectEnd), active.Checked, ""); if (await RunChangeOperationAsync(ct => Service.SaveProjectAsync(request with { Reason = RemoteApi.Reason }, ct), "Proje bilgileri kaydedildi.", "Proje")) { ResetContext(); await LoadProjectsAsync(); } }
    private async Task SaveTeamAsync() { var target = projectTarget; if (target == null) return; var team = teamTarget; var request = new Wire.TeamWrite(target.Id, team?.Id, team?.Version, teamCode.Text, teamName.Text, teamActive.Checked, ""); if (await RunChangeOperationAsync(ct => Service.SaveTeamAsync(request with { Reason = RemoteApi.Reason }, ct), "Ekip kaydedildi.", "Ekip")) { teamTarget = null; teamCode.Clear(); teamCode.ReadOnly = false; teamName.Clear(); await RunOperationAsync(async ct => { var data = await Service.TeamsAsync(target.Id, ct); if (!IsDisposed) BindTeams(data); }); AcceptTrackedChanges("Ekip"); } }
    private async Task AssignAsync() { var target = projectTarget; var selected = person; if (target == null || selected == null || assignmentTeam.SelectedItem is not TeamChoice team) return; var request = new Wire.AssignmentWrite(target.Id, target.Version, team.Id, team.Version, selected.Id, (Wire.PersonnelGroup)selected.Group, selected.Tc, selected.Version, Value(assignmentStart)!.Value, Value(assignmentEnd), "", supplementary.Checked); if (await RunChangeOperationAsync(ct => Service.AssignAsync(request with { Reason = RemoteApi.Reason }, ct), "Proje/ekip ataması kaydedildi.", "Atama")) await LoadAssignmentsAsync(); }
    private async Task CancelAsync() { var row = Row(assignments); if (row == null || !ConfirmDiscardChanges("Atamayı iptal etme")) return; var request = new Wire.AssignmentCancel(row.Field<Guid>("AssignmentId"), ((byte[])row["RecordVersion"]).ToArray(), ""); if (await RunChangeOperationAsync(ct => Service.CancelAsync(request with { Reason = RemoteApi.Reason }, ct), "Atama gerekçesiyle iptal edildi.")) await LoadAssignmentsAsync(); }
    private async Task CreateAsync()
    {
        if (!ConfirmDiscardChanges("Yeni proje oluşturma")) return;
        using var dialog = new Form { Text = "Yeni proje", ClientSize = new Size(450, 170), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false };
        TextBox code = new() { Width = 380, MaxLength = 30 }, title = new() { Width = 380, MaxLength = 100 }; var save = new Button { Text = "Oluştur", DialogResult = DialogResult.OK }; var panel = Bar(Label("Kod"), code, Label("Ad"), title, save); dialog.Controls.Add(panel); dialog.AcceptButton = save; AppTheme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return; string value = code.Text, caption = title.Text;
        if (await RunChangeOperationAsync(async ct => { await new ProjectFinanceService(AppDatabase.ConnectionString, User).CreateProjectAsync(value, caption, ct); }, "Proje oluşturuldu; ayrıntılar için listeden açın.")) { ResetContext(); await LoadProjectsAsync(); }
    }
}
