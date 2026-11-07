using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal static class CalendarCommands
{
    internal static Task RunAsync(object input, UserSession user, CancellationToken ct)
    { var s = new SchedulingService(AppDatabase.ConnectionString, user); return input switch { Wire.WagePolicyCreate x => new WageRuleService(AppDatabase.ConnectionString,user).CreatePolicyAsync(x,ct), Wire.WagePolicyDecision x => new WageRuleService(AppDatabase.ConnectionString,user).DecidePolicyAsync(x,ct), Wire.WageClaimCreate x => new WageRuleService(AppDatabase.ConnectionString,user).CreateClaimAsync(x,ct), Wire.WageClaimAction x => new WageRuleService(AppDatabase.ConnectionString,user).ActAsync(x,ct), Wire.WageClaimTransfer x => new WageRuleService(AppDatabase.ConnectionString,user).TransferAsync(x,ct), Wire.FiscalPeriodFinalize x => new FiscalPeriodService(AppDatabase.ConnectionString,user).FinalizeAsync(x,ct), Wire.BankAccountCreate x => new BankReconciliationService(AppDatabase.ConnectionString,user).CreateAccountAsync(x,ct), Wire.BankImport x => new BankReconciliationService(AppDatabase.ConnectionString,user).ImportAsync(x,ct), Wire.BankMatchCreate x => new BankReconciliationService(AppDatabase.ConnectionString,user).MatchAsync(x,ct), Wire.BankMatchRelease x => new BankReconciliationService(AppDatabase.ConnectionString,user).ReleaseAsync(x,ct), Wire.ProjectBudgetSave x => new ProjectBudgetService(AppDatabase.ConnectionString,user).SaveAsync(x,ct), Wire.PaymentAllocationCreate x => new PaymentAllocationService(AppDatabase.ConnectionString,user).CreateAsync(x,ct), Wire.PaymentAllocationRelease x => new PaymentAllocationService(AppDatabase.ConnectionString,user).ReleaseAsync(x,ct), Wire.AccrualCorrectionCreate x => new AccrualFoundationService(AppDatabase.ConnectionString,user).CreateCorrectionAsync(x,ct), Wire.AccrualCorrectionApprove x => new AccrualFoundationService(AppDatabase.ConnectionString,user).ApproveCorrectionAsync(x,ct), Wire.AccrualCorrectionPost x => new AccrualFoundationService(AppDatabase.ConnectionString,user).PostCorrectionAsync(x,ct), Wire.AccrualApprove x => new AccrualFoundationService(AppDatabase.ConnectionString,user).ApproveAsync(x,ct), Wire.AccrualPost x => new AccrualFoundationService(AppDatabase.ConnectionString,user).PostAsync(x,ct), Wire.HourlyRateCreate x => new AccrualFoundationService(AppDatabase.ConnectionString,user).CreateRateAsync(x,ct), Wire.AccrualCalculate x => new AccrualFoundationService(AppDatabase.ConnectionString,user).CalculateAsync(x,ct), Wire.ManualTimesheetBatch x => new TimesheetService(AppDatabase.ConnectionString, user).SaveManualBatchAsync(x, ct), Wire.AccountPersonChange x => new AccountPersonService(AppDatabase.ConnectionString, user).ChangeAsync(x, ct), Wire.SegmentDecision x => new TimesheetService(AppDatabase.ConnectionString, user).DecideSegmentAsync(x, ct), Wire.WorkAllocation x => new TimesheetService(AppDatabase.ConnectionString, user).SaveAllocationAsync(x, ct), Wire.WorkAllocationRelease x => new TimesheetService(AppDatabase.ConnectionString, user).ReleaseAllocationAsync(x, ct), Wire.AttendanceImport x => new TimesheetService(AppDatabase.ConnectionString, user).ImportEventsAsync(x, ct), Wire.AttendanceMatch x => new TimesheetService(AppDatabase.ConnectionString, user).MatchEventsAsync(x, ct), Wire.AttendanceRelease x => new TimesheetService(AppDatabase.ConnectionString, user).ReleaseMatchAsync(x, ct), Wire.AttendanceIgnore x => new TimesheetService(AppDatabase.ConnectionString, user).IgnoreEventAsync(x, ct), Wire.UnplannedTimesheetWrite x => new TimesheetService(AppDatabase.ConnectionString, user).SaveUnplannedAsync(x, ct), Wire.TimesheetBatch x => new TimesheetService(AppDatabase.ConnectionString, user).SaveAsync(x, ct), Wire.TimesheetState x => new TimesheetService(AppDatabase.ConnectionString, user).StateAsync(x, ct), Wire.ShiftTemplateWrite x => s.SaveTemplateAsync(x, ct), Wire.ShiftCreate x => s.CreateShiftAsync(x, ct), Wire.ShiftState x => s.StateAsync(x, ct), Wire.ShiftPerson x => s.AssignAsync(x, ct), Wire.ShiftPersonCancel x => s.CancelAssignmentAsync(x, ct), Wire.LeaveRequestWrite x => s.RequestLeaveAsync(x, ct), Wire.LeaveRequestState x => s.LeaveStateAsync(x, ct), Wire.WeekCopy x => s.CopyWeekAsync(x, ct), _ => throw new ArgumentException("Takvim komutu geçersiz.") }; }
}
internal abstract partial class CalendarEditDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    protected readonly FlowLayoutPanel Fields = new() { Dock = DockStyle.Fill, Padding = new(16), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
    internal bool Saved { get; private set; }
    private readonly string recordContext;
    private readonly Button save = new() { Text = "Kaydet", AutoSize = true }; private object? intent; private string? intentReason;
    protected CalendarEditDialog(UserSession user, string title, string recordContext = "Takvim kaydı")
    {
        this.recordContext = recordContext; Session = user; PersistGridLayout = false; Text = title; ClientSize = new(650, 600); var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new(10) }; var close = new Button { Text = "Kapat", AutoSize = true }; close.Click += (_, _) => Close(); bar.Controls.AddRange([save, close]); Controls.Add(Fields); Controls.Add(bar); save.Click += async (_, _) => await SubmitAsync(); SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close(); Fields.SizeChanged += (_, _) => { foreach (var label in Fields.Controls.OfType<Label>()) label.MaximumSize = new(Math.Max(1, Fields.ClientSize.Width - 48), 0); };
    }
    protected void Field(string text, Control control) { Fields.Controls.Add(new Label { Text = text, AutoSize = true }); Fields.Controls.Add(control); TrackChanges(recordContext, control); }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(520, 420)); }
    protected override void RefreshEditState() { save.Enabled = !IsOperationRunning && DraftAllowsSave && CanSubmit; RefreshDraftButtons(); save.Text = intent == null ? "Kaydet" : "Aynı isteği yeniden gönder"; foreach (Control c in Fields.Controls) if (c is not Label) c.Enabled = !IsOperationRunning && intent == null && DraftAllowsSave; }
    protected virtual bool CanSubmit=>true;
    protected abstract object MakeRequest(string reason);
    private async Task SubmitAsync()
    {
        async Task Execute(CancellationToken ct)
        {
            if(intent==null){intent=MakeRequest(RemoteApi.Reason);intentReason=RemoteApi.Reason;}
            await BeforeDraftSendAsync(intent,ct);
            await CalendarCommands.RunAsync(intent,User,ct);
            ClearCompletedDraft();
        }
        bool done; if (intent == null) done = await RunChangeOperationAsync(Execute, "Kayıt saklandı.", recordContext); else { using var reason = AuditContext.UseReason(intentReason!); done = await RunOperationAsync(Execute, "İşlem sonucu alındı."); }
        if (done) { Saved = true; AcceptTrackedChanges(); Close(); }
    }
}
internal sealed record ScopedPerson(Guid Id, Wire.PersonnelGroup Group, byte[] Version, string Name);
internal sealed class CalendarPersonPicker : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly Guid project; private readonly DataGridView grid = new() { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells }; private readonly ListPager pager;
    internal ScopedPerson? SelectedPerson { get; private set; }
    internal CalendarPersonPicker(Guid project, UserSession user, string title = "Takvim — Proje personeli seç")
    {
        this.project = project; Session = user; PersistGridLayout = false; Text = title; ClientSize = new(760, 520); RecordSelection.Configure(grid); grid.DataBindingComplete += (_, _) => { foreach (DataGridViewColumn c in grid.Columns) if (c.ValueType == typeof(Guid) || c.ValueType == typeof(byte[])) c.Visible = false; }; Controls.Add(grid); pager = ListPager.Attach(grid, this, ReloadAsync); var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true }; var choose = new Button { Text = "Seç", AutoSize = true }; var cancel = new Button { Text = "Vazgeç", AutoSize = true }; bar.Controls.AddRange([choose, cancel]); Controls.Add(bar); choose.Click += (_, _) => { if (IsOperationRunning || grid.SelectedRows.Count != 1 || grid.SelectedRows[0].DataBoundItem is not DataRowView r) return; SelectedPerson = new(r.Row.Field<Guid>("PersonId", DataRowVersion.Original), (Wire.PersonnelGroup)r.Row.Field<int>("PersonnelGroup", DataRowVersion.Original), ((byte[])r.Row["RecordVersion", DataRowVersion.Original]).ToArray(), r.Row["AD"] + " " + r.Row["SOYAD"]); DialogResult = DialogResult.OK; Close(); }; cancel.Click += (_, _) => Close(); Load += async (_, _) => await ReloadAsync(); RefreshAction = ReloadAsync; SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    private async Task ReloadAsync() => await RunOperationAsync(async ct => { var p = await new ProjectAccessService(AppDatabase.ConnectionString, User).PeopleAsync(project, pager.Index, pager.SizeLimit, ct); if (IsDisposed) return; grid.DataSource = p.Rows; grid.ClearSelection(); pager.Apply(p); });
    protected override void RefreshEditState() => pager?.UpdateState();
}
internal sealed class ShiftTemplateDialog : CalendarEditDialog
{
    private sealed record Team(Guid? Id, string Name) { public override string ToString() => Name; }
    private readonly Guid project, id; private readonly byte[]? version;
    private readonly TextBox name = new() { Width = 340, MaxLength = 100 }, site = new() { Width = 340, MaxLength = 100 };
    private readonly ComboBox team = new() { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly DateTimePicker start = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm", ShowUpDown = true, Value = DateTime.Today.AddHours(9) };
    private readonly NumericUpDown duration = Number(1, 1440, 480), pause = Number(0, 1439, 30), count = Number(1, 500, 1); private readonly CheckBox active = new() { Text = "Şablon etkin", Checked = true, AutoSize = true };
    private static NumericUpDown Number(int min, int max, int value) => new() { Minimum = min, Maximum = max, Value = value, Width = 150 };
    internal ShiftTemplateDialog(Guid project, UserSession user, DataTable teams, DataRow? row) : base(user, "Takvim — Vardiya şablonu")
    {
        this.project = project; id = row?.Field<Guid>("TemplateId") ?? Guid.NewGuid(); version = row?["RecordVersion"] is byte[] v ? v.ToArray() : null; team.Items.Add(new Team(null, "Proje geneli (ekip filtresi yok)")); foreach (DataRow t in teams.Rows) if (t.Field<bool>("IsActive")) team.Items.Add(new Team(t.Field<Guid>("TeamId"), t["Code"] + " · " + t["Name"])); team.SelectedIndex = 0;
        if (row != null) { name.Text = row.Field<string>("Name"); site.Text = row.Field<string>("Site"); start.Value = DateTime.Today.AddMinutes(row.Field<int>("StartMinute")); duration.Value = row.Field<int>("DurationMinutes"); pause.Value = row.Field<int>("BreakMinutes"); count.Value = row.Field<int>("RequiredCount"); active.Checked = row.Field<bool>("IsActive"); var selected = team.Items.Cast<Team>().FirstOrDefault(t => t.Id == row.Field<Guid?>("TeamId")); if (selected != null) team.SelectedItem = selected; else { team.Items.Add(new Team(row.Field<Guid?>("TeamId"), "Eski/pasif ekip")); team.SelectedIndex = team.Items.Count - 1; } }
        Field("Şablon adı", name); Field("Saha / konum", site); Field("Ekip", team); Field("Başlangıç (İstanbul)", start); Field("Süre (dakika, geceyi aşabilir)", duration); Field("Mola (dakika, ücret hesabı değildir)", pause); Field("Gereken kişi sayısı", count); Field("Durum", active); AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason) => new Wire.ShiftTemplateWrite(project, id, version?.ToArray(), (team.SelectedItem as Team)?.Id, name.Text, site.Text, start.Value.Hour * 60 + start.Value.Minute, (int)duration.Value, (int)pause.Value, (int)count.Value, active.Checked, reason);
}
internal sealed class ShiftCreateDialog : CalendarEditDialog
{
    private sealed record Template(DataRow Row) { public override string ToString() => Row["Name"] + " · " + TimeSpan.FromMinutes(Row.Field<int>("StartMinute")).ToString(@"hh\:mm") + " · " + Row["DurationMinutes"] + " dk"; }
    private readonly Guid project, id = Guid.NewGuid(); private readonly ComboBox template = new() { Width = 470, DropDownStyle = ComboBoxStyle.DropDownList }; private readonly DateTimePicker day = new() { Format = DateTimePickerFormat.Short };
    internal ShiftCreateDialog(Guid project, UserSession user, DataTable templates, DateTime first) : base(user, "Takvim — Şablondan vardiya oluştur")
    { this.project = project; day.Value = first; foreach (DataRow row in templates.Rows) if (row.Field<bool>("IsActive")) template.Items.Add(new Template(row)); if (template.Items.Count > 0) template.SelectedIndex = 0; Field("Şablon (değerleri yeni vardiyaya kopyalanır)", template); Field("Vardiyanın başlangıç günü", day); Fields.Controls.Add(new Label { AutoSize = true, Text = "Şablonun süresi geceyi aşıyorsa bitiş sonraki gündür. Önce taslak açılır; kişi atayıp sonra yayınlayın. Yayından sonra değişiklik için iptal ve yeni vardiya gerekir." }); AcceptTrackedChanges(); }
    protected override object MakeRequest(string reason) { var t = (template.SelectedItem as Template)?.Row ?? throw new ArgumentException("Etkin şablon seçin."); return new Wire.ShiftCreate(project, id, t.Field<Guid>("TemplateId"), ((byte[])t["RecordVersion"]).ToArray(), DateTime.SpecifyKind(day.Value.Date, DateTimeKind.Unspecified), reason); }
}
internal sealed class LeaveRequestDialog : CalendarEditDialog
{
    private readonly Guid project, id = Guid.NewGuid(); private readonly ScopedPerson person;
    private readonly DateTimePicker start = Moment(9), end = Moment(17);
    private static DateTimePicker Moment(int hour) => new() { Width = 280, Format = DateTimePickerFormat.Custom, CustomFormat = "dd.MM.yyyy HH:mm", Value = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, DailyClosingService.BusinessZone).Date.AddHours(hour), MinDate = new(1900, 1, 1), MaxDate = new(2200, 12, 31) };
    internal LeaveRequestDialog(Guid project, UserSession user, ScopedPerson person) : base(user, "Takvim — İzin talebi")
    { this.project = project; this.person = person; Fields.Controls.Add(new Label { Text = person.Name, AutoSize = true }); Field("Başlangıç (İstanbul, dahil)", start); Field("Bitiş (İstanbul, hariç)", end); Fields.Controls.Add(new Label { Text = "Tam gün için 00:00 → ertesi gün 00:00 kullanın. Talep onaylanana kadar takvimde talep olarak görünür; yasal izin hakkı veya ücret hesaplanmaz.", AutoSize = true }); AcceptTrackedChanges(); }
    protected override object MakeRequest(string reason) => new Wire.LeaveRequestWrite(project, id, person.Id, person.Group, person.Version.ToArray(), DateTime.SpecifyKind(start.Value, DateTimeKind.Unspecified), DateTime.SpecifyKind(end.Value, DateTimeKind.Unspecified), reason);
}
internal sealed class WeekCopyDialog : CalendarEditDialog
{
    private readonly Guid project, batch = Guid.NewGuid(); private readonly DateTime source; private readonly System.Data.DataTable rows;
    private readonly System.Windows.Forms.DateTimePicker target = new() { Format = System.Windows.Forms.DateTimePickerFormat.Short };
    private readonly System.Windows.Forms.DataGridView preview = new() { ReadOnly = true, Width = 570, Height = 260, AllowUserToAddRows = false, AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.DisplayedCells };
    internal DateTime Target => DateTime.SpecifyKind(target.Value.Date.AddDays(-(((int)target.Value.DayOfWeek + 6) % 7)), DateTimeKind.Unspecified);
    internal WeekCopyDialog(Guid project, WinFormsApp2.UserSession user, DateTime source, System.Data.DataTable shifts) : base(user, "Takvim — Hafta kopyalama önizlemesi")
    {
        this.project = project; this.source = source; RecordSelection.Configure(preview); rows = shifts.Clone(); foreach (System.Data.DataRow r in shifts.Rows) if (r.Field<string>("Status") != "Cancelled" && r.Field<DateTime>("LocalStart") >= source && r.Field<DateTime>("LocalStart") < source.AddDays(7)) rows.ImportRow(r);
        target.Value = source.AddDays(7); Field("Hedef hafta (seçilen günün pazartesisi)", target); Fields.Controls.Add(new System.Windows.Forms.Label { AutoSize = true, Text = "Aşağıdaki planlar ve etkin kişi atamaları hedef haftaya TASLAK olarak kopyalanır. Kaynak şablonun güncel değerleri yerine vardiyanın dondurulmuş değerleri kullanılır. Çakışma, eski dönem veya atama sorunu varsa bütün kopyalama geri alınır. En fazla 50 vardiya / 200 kişi ataması." }); Fields.Controls.Add(preview); target.ValueChanged += (_, _) => Preview(); Fields.SizeChanged += (_, _) => preview.Width = Math.Max(1, Fields.ClientSize.Width - 48); Preview(); AcceptTrackedChanges();
    }
    private void Preview() { var table = new System.Data.DataTable(); table.Columns.Add("Vardiya"); table.Columns.Add("Yeni başlangıç", typeof(DateTime)); table.Columns.Add("Yeni bitiş", typeof(DateTime)); table.Columns.Add("Atanan", typeof(int)); table.Columns.Add("Gereken", typeof(int)); foreach (System.Data.DataRow r in rows.Rows) table.Rows.Add(r["Name"], r.Field<DateTime>("LocalStart") + (Target - source), r.Field<DateTime>("LocalEnd") + (Target - source), r["AssignedCount"], r["RequiredCount"]); preview.DataSource = table; foreach (System.Windows.Forms.DataGridViewColumn c in preview.Columns) if (c.ValueType == typeof(DateTime)) c.DefaultCellStyle.Format = "dd.MM.yyyy HH:mm"; }
    protected override object MakeRequest(string reason) => new AccountingApp.Contracts.WeekCopy(project, batch, source, Target, rows.AsEnumerable().Select(r => new AccountingApp.Contracts.ShiftCopySource(r.Field<Guid>("ShiftId"), ((byte[])r["RecordVersion"]).ToArray())).ToArray(), reason);
}
