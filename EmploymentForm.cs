using System.Data;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class EmploymentDateDialog : Form
{
    private readonly DateTimePicker date;
    internal DateTime? SelectedDate => date.ShowCheckBox && !date.Checked ? null : DateTime.SpecifyKind(date.Value.Date, DateTimeKind.Unspecified);
    internal EmploymentDateDialog(bool start)
    {
        Text = start ? "İşe giriş tarihi" : "Ayrılış tarihi"; ClientSize = new(490, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false;
        date = new() { Format = DateTimePickerFormat.Short, ShowCheckBox = !start, Checked = true, Width = 180 };
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), FlowDirection = FlowDirection.TopDown, WrapContents = false };
        panel.Controls.Add(new Label { Text = start ? "Yeni çalışma döneminin başlangıç tarihini seçin." : "Ayrılış tarihini seçin. Tarih yalnız eski aktarım döneminde bilinmiyor bırakılabilir.", AutoSize = true, MaximumSize = new(450, 0) });
        panel.Controls.Add(date);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        var ok = new Button { Text = "Devam et", AutoSize = true, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Vazgeç", AutoSize = true, DialogResult = DialogResult.Cancel };
        buttons.Controls.AddRange([ok, cancel]); panel.Controls.Add(buttons); Controls.Add(panel);
        AcceptButton = ok; CancelButton = cancel;
    }
}

internal sealed class EmploymentForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly PersonnelGroup group;
    private readonly Guid person;
    private readonly byte[] personVersion;
    private readonly bool archived;
    private readonly DataGridView periods = new() { Name = "EmploymentPeriods", Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly DateTimePicker start = Date(), end = Date();
    private readonly Button save = new() { Text = "Güncel dönemin tarihlerini düzelt", AutoSize = true }, rehire = new() { Text = "Yeni çalışma dönemiyle işe başlat…", AutoSize = true };
    private DataRow? current;
    private EmploymentService Service => new(AppDatabase.ConnectionString, User);
    private static DateTimePicker Date() => new() { Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false, Width = 150 };
    private static DateTime? Value(DateTimePicker d) => d.Checked ? DateTime.SpecifyKind(d.Value.Date, DateTimeKind.Unspecified) : null;
    internal EmploymentForm(PersonnelGroup group, Guid person, byte[] version, bool archived, UserSession user)
    {
        this.group = group; this.person = person; personVersion = version.ToArray(); this.archived = archived;
        RecordSelection.Configure(periods);
        Session = user; PersistGridLayout = false; Text = "Muhasebe — Çalışma dönemleri"; ClientSize = new(950, 550);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), ColumnCount = 1, RowCount = 4 };
        outer.RowStyles.Add(new(SizeType.AutoSize)); outer.RowStyles.Add(new(SizeType.Percent, 100)); outer.RowStyles.Add(new(SizeType.AutoSize)); outer.RowStyles.Add(new(SizeType.AutoSize));
        outer.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Her işe giriş ayrı dönemdir. Eski aktarımın bilinmeyen tarihleri boş görünür.\nTarih düzeltmesi kartın güncel dönemini etkiler. Yeniden işe girişte eski dönem bakiyesi sıfır olmalıdır." }, 0, 0);
        outer.Controls.Add(periods, 0, 1);
        var dates = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        dates.Controls.AddRange([new Label { Text = "Güncel işe giriş", AutoSize = true }, start, new Label { Text = "Güncel ayrılış", AutoSize = true }, end, save]); outer.Controls.Add(dates, 0, 2);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        var close = new Button { Text = "Kapat", AutoSize = true }; close.Click += (_, _) => Close();
        buttons.Controls.AddRange([rehire, close]); outer.Controls.Add(buttons, 0, 3); Controls.Add(outer);
        TrackChanges("Dönem tarihleri", start, end); EditControls.AddRange([save, rehire, start, end]);
        save.Click += async (_, _) => await SaveAsync(); rehire.Click += async (_, _) => await RehireAsync();
        Load += async (_, _) => await LoadPeriodsAsync(); RefreshAction = LoadPeriodsAsync;
        SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(640, 440)); }
    private async Task LoadPeriodsAsync() => await RunOperationAsync(async ct =>
    {
        var data = await Service.ListAsync(group, person, ct); if (IsDisposed) return;
        periods.DataSource = data;
        foreach (var (name, caption) in new[] { ("EmploymentId", "Dönem kimliği"), ("StartDate", "İşe giriş"), ("EndDate", "Ayrılış"), ("IsLegacy", "Eski aktarım"), ("IsClosed", "Kapalı"), ("IsCurrent", "Kartın güncel dönemi"), ("CreatedUtc", "Oluşturulma UTC"), ("CreatedBy", "Oluşturan") })
            if (periods.Columns.Contains(name)) periods.Columns[name]!.HeaderText = caption;
        foreach (string name in new[] { "RecordVersion", "PersonId", "PersonnelGroup" }) if (periods.Columns.Contains(name)) periods.Columns[name]!.Visible = false;
        current = data.AsEnumerable().SingleOrDefault(r => r.Field<bool>("IsCurrent"));
        start.Checked = current?["StartDate"] is DateTime; if (current?["StartDate"] is DateTime s) start.Value = s;
        end.Checked = current?["EndDate"] is DateTime; if (current?["EndDate"] is DateTime e) end.Value = e;
        AcceptTrackedChanges();
    });
    protected override void RefreshEditState()
    { save.Enabled = start.Enabled = end.Enabled = !IsOperationRunning && User.CanEdit && current != null; rehire.Enabled = !IsOperationRunning && User.CanEdit && archived && current != null; }
    private async Task SaveAsync()
    {
        var row = current; if (row == null) return;
        var request = new Wire.EmploymentDates((Wire.PersonnelGroup)group, person, personVersion.ToArray(), row.Field<Guid>("EmploymentId"), ((byte[])row["RecordVersion"]).ToArray(), Value(start), Value(end), "");
        if (await RunChangeOperationAsync(ct => Service.DatesAsync(request with { Reason = RemoteApi.Reason }, ct), "Dönem tarihleri düzeltildi.", "Dönem tarihleri")) Close();
    }
    private async Task RehireAsync()
    {
        if (!archived || !ConfirmDiscardChanges("Yeni çalışma dönemi başlatma")) return;
        using var dialog = new EmploymentDateDialog(true); if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var request = new Wire.EmploymentRehire((Wire.PersonnelGroup)group, person, personVersion.ToArray(), dialog.SelectedDate!.Value, "");
        if (await RunChangeOperationAsync(ct => Service.RehireAsync(request with { Reason = RemoteApi.Reason }, ct), "Personel yeni çalışma dönemiyle işe başlatıldı.")) { AcceptTrackedChanges(); Close(); }
    }
}
