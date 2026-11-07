using System.Data;

namespace WinFormsApp2;

internal sealed class PersonnelCardForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private sealed record PeriodChoice(Guid? Id, string Caption) { public override string ToString() => Caption; }
    private readonly PersonnelGroup group; private readonly Guid person;
    private readonly Label heading = new() { Dock = DockStyle.Fill, AutoSize = true }, money = new() { Dock = DockStyle.Fill, AutoSize = true }, scope = new() { Dock = DockStyle.Fill, AutoSize = true };
    private readonly TextBox first = new() { Width = 180, MaxLength = 50 }, last = new() { Width = 180, MaxLength = 50 }, iban = new() { Width = 290, MaxLength = 34 };
    private readonly ComboBox periods = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    private readonly Button groupTransition = Button("Tarihli grup geçişi…"), save = Button("Ad / IBAN kaydet"), employment = Button("Çalışma dönemlerini yönet…"), archive = Button("Arşivle…"), leave = Button("Güncel dönem iznini düzenle…"), export = Button("Açık sayfayı CSV…");
    private readonly DataGridView jobs = new() { Name = "CardPeriods", Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly Dictionary<PersonnelCardSection, (DataGridView Grid, ListPager Pager, TabPage Tab)> sections = [];
    private DataRow? header; private bool loading; private long generation;
    private PersonnelCardService Service => new(AppDatabase.ConnectionString, User);
    internal PersonnelCardForm(PersonnelGroup group, Guid person, UserSession user)
    {
        this.group = group; this.person = person; Session = user; PersistGridLayout = false;
        Text = "Muhasebe — Bütünleşik personel kartı"; ClientSize = new(1180, 800);
        var outer = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new(12), ColumnCount = 1, RowCount = 6 };
        for (int i = 0; i < 5; i++) outer.RowStyles.Add(new(SizeType.AutoSize)); outer.RowStyles.Add(new(SizeType.Percent, 100));
        var refresh = Button("Kartı yenile"); var close = Button("Kapat");
        outer.Controls.Add(Bar(refresh, employment, groupTransition, archive, leave, export, close), 0, 0); outer.Controls.Add(heading, 0, 1);
        outer.Controls.Add(Bar(Label("Ad"), first, Label("Soyad"), last, Label("IBAN"), iban, save), 0, 2);
        outer.Controls.Add(Bar(Label("Geçmiş dönemi"), periods), 0, 3);
        var summary = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2 }; summary.Controls.Add(money, 0, 0); summary.Controls.Add(scope, 0, 1); outer.Controls.Add(summary, 0, 4); outer.Controls.Add(tabs, 0, 5); Controls.Add(outer);
        RecordSelection.Configure(jobs); var jobsTab = new TabPage("Çalışma dönemleri"); jobsTab.Controls.Add(jobs); tabs.TabPages.Add(jobsTab); jobs.DataBindingComplete += (_, _) => Format(jobs);
        foreach (var (section, caption) in new[] { (PersonnelCardSection.Assignments, "Proje ve ekip atamaları"), (PersonnelCardSection.Leaves, "İzinler"), (PersonnelCardSection.LeaveChanges, "İzin değişiklikleri"), (PersonnelCardSection.Finance, "Hakediş belgeleri"), (PersonnelCardSection.Ledger, "Ödeme ve defter"), (PersonnelCardSection.ArchiveEvents, "Arşiv olayları"), (PersonnelCardSection.GroupTransitions, "Grup geçişleri"), (PersonnelCardSection.Audit, "İşlem geçmişi (yönetici)") })
        {
            if (section == PersonnelCardSection.Audit && user.Role != UserRole.Admin) continue;
            var tab = new TabPage(caption) { Tag = section }; var grid = new DataGridView { Name = "Card" + section, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
            RecordSelection.Configure(grid); tab.Controls.Add(grid); tabs.TabPages.Add(tab);
            var page = ListPager.Attach(grid, this, () => LoadSectionAsync(section), "kayıt"); sections[section] = (grid, page, tab);
            grid.DataBindingComplete += (_, _) => Format(grid);
            if (section == PersonnelCardSection.Audit) grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name is "OldValues" or "NewValues") ShowAudit(grid[e.ColumnIndex, e.RowIndex].Value?.ToString() ?? ""); };
        }
        TrackChanges("Personel bilgileri", first, last, iban);
        refresh.Click += async (_, _) => { if (ConfirmDiscardChanges("Kartı yenileme")) await ReloadAsync(); }; RefreshAction = ReloadAsync;
        groupTransition.Click += async (_, _) => await TransitionGroupAsync();
        close.Click += (_, _) => Close(); save.Click += async (_, _) => await SaveAsync(); employment.Click += async (_, _) => await EmploymentAsync(); archive.Click += async (_, _) => await ArchiveAsync(); leave.Click += async (_, _) => await LeaveAsync(); export.Click += async (_, _) => await ExportAsync();
        periods.SelectedIndexChanged += async (_, _) =>
        {
            if (loading) return;
            if (!ConfirmDiscardChanges("Geçmiş dönemini değiştirme")) { loading = true; periods.SelectedItem = priorPeriod; loading = false; return; }
            await ReloadAsync();
        };
        tabs.SelectedIndexChanged += async (_, _) => { if (!loading && header != null && tabs.SelectedTab?.Tag is PersonnelCardSection s) await LoadSectionAsync(s); };
        outer.SizeChanged += (_, _) => { int width = Math.Max(1, outer.ClientSize.Width - outer.Padding.Horizontal); heading.MaximumSize = money.MaximumSize = scope.MaximumSize = new(width, 0); };
        Load += async (_, _) => await ReloadAsync(); PageRequested += (_, _) => Close(); SessionRevoked += (_, _) => Close();
    }
    private PeriodChoice? priorPeriod;
    private static Button Button(string text) => new() { Text = text, AutoSize = true };
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new(4, 7, 4, 0) };
    private static FlowLayoutPanel Bar(params Control[] controls) { var p = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true }; p.Controls.AddRange(controls); return p; }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(740, 550)); }
    private Guid? SelectedPeriod => (periods.SelectedItem as PeriodChoice)?.Id;
    private byte[] Version => ((byte[])header!["RecordVersion", DataRowVersion.Original]).ToArray();
    private static string Date(object value) => value is DateTime d ? d.ToString("dd.MM.yyyy") : "bilinmiyor";
    private async Task ReloadAsync()
    {
        Guid? selected = SelectedPeriod; long read = ++generation; header = null;
        jobs.DataSource = null;
        foreach (var item in sections.Values) { item.Grid.DataSource = null; item.Pager.Reset(); }
        money.Text = ""; scope.Text = "Kart yükleniyor…";
        if (!await RunOperationAsync(async ct =>
        {
            var card = await Service.ReadAsync(group, person, selected, ct); if (IsDisposed || read != generation) return;
            if (card.Header.Rows.Count != 1) throw new InvalidOperationException("Personel kartı bulunamadı."); header = card.Header.Rows[0]; jobs.DataSource = card.Periods; jobs.ClearSelection();
            first.Text = header["AD"].ToString(); last.Text = header["SOYAD"].ToString(); iban.Text = header["IBAN"].ToString();
            heading.Text = $"{header["AD"]} {header["SOYAD"]} · {(group == PersonnelGroup.General ? "Genel" : "Joker")} · TC: {header["TC"]} · {(header.IsNull("ArchivedUtc") ? "Etkin" : "Arşivde")}\nKartın güncel bakiyesi: {(User.CanViewSalary ? $"{header["PARA"]:N2} TL" : "Yetki yok")} · Güncel dönem: {Date(header["CurrentStart"])} – {Date(header["CurrentEnd"])} · {(header.Field<bool>("IsLegacy") ? "Eski aktarım" : "Tarihli dönem")}\nSon kart/özet okuması: {card.ReadUtc.ToLocalTime():dd.MM.yyyy HH:mm:ss}";
            loading = true;
            try
            {
                periods.Items.Clear(); periods.Items.Add(new PeriodChoice(null, "Tüm dönemler"));
                foreach (DataRow r in card.Periods.Rows) periods.Items.Add(new PeriodChoice(r.Field<Guid>("EmploymentId"), $"{(r.Field<int>("PersonnelGroup") == 0 ? "Genel" : "Joker")} · {Date(r["StartDate"])} – {Date(r["EndDate"])} · {(r.Field<Guid>("EmploymentId") == header.Field<Guid>("CurrentEmploymentId") ? "Güncel" : "Önceki")} · {(r.Field<bool>("IsLegacy") ? "Eski aktarım" : "Tarihli")} · {(r.Field<bool>("IsClosed") ? "Kapalı" : "Açık")}"));
                periods.SelectedItem = periods.Items.Cast<PeriodChoice>().First(p => p.Id == selected); priorPeriod = periods.SelectedItem as PeriodChoice;
            }
            finally { loading = false; }
            var t = card.Totals.Rows[0]; money.Text = $"Seçili kapsam · Geçerli hakediş: {t["Accrual"]:N2} TL · Avans: {t["Advance"]:N2} TL · Bakiye ödemesi: {t["Settlement"]:N2} TL · Eski/sınıflandırılmamış ödeme: {t["Unclassified"]:N2} TL";
            if (!User.CanViewSalary) money.Text = "Ücret / bakiye görüntüleme yetkisi yok.";
            scope.Text = "Geçmiş, kişinin iki gruptaki dönemlerini kapsar. Grup geçişleri daima tüm dönemlerdir. Sekmeler kendi yüklenme anındaki veriyi gösterir. Arşiv olayları ve işlem geçmişi daima tüm dönemlerdir. Kimliği bilinmeyen eski ödemeler karta tahminle bağlanmaz. Dönem seçimi üstteki güncel bakiyeyi değiştirmez.";
            AcceptTrackedChanges();
        })) { heading.Text = "Kart yüklenemedi. Yenileyin veya kapatın."; scope.Text = "Önceki veriler temizlendi."; return; }
        if (tabs.SelectedTab?.Tag is PersonnelCardSection section) await LoadSectionAsync(section);
    }
    private async Task LoadSectionAsync(PersonnelCardSection section)
    {
        if (header == null || !sections.TryGetValue(section, out var view)) return; long read = generation; Guid? period = SelectedPeriod; view.Grid.DataSource = null;
        await RunOperationAsync(async ct => { var page = await Service.PageAsync(group, person, period, section, view.Pager.Index, view.Pager.SizeLimit, ct); if (IsDisposed || read != generation || period != SelectedPeriod) return; view.Grid.DataSource = page.Rows; view.Grid.ClearSelection(); view.Pager.Apply(page); view.Tab.ToolTipText = "Yüklendi: " + DateTime.Now.ToString("HH:mm:ss"); });
    }
    protected override void RefreshEditState()
    {
        bool ready = !IsOperationRunning && header != null; bool editable = ready && User.CanEdit;
        save.Enabled = editable && header!.IsNull("ArchivedUtc"); first.ReadOnly = last.ReadOnly = !save.Enabled; iban.ReadOnly = !save.Enabled || !User.CanViewIban;
        employment.Enabled = archive.Enabled = editable;
        groupTransition.Enabled = editable && User.CanViewSalary && header!.IsNull("ArchivedUtc");
        archive.Text = header?.IsNull("ArchivedUtc") == false ? "Arşivlemeyi geri al…" : "Arşivle…";
        leave.Enabled = editable && group == PersonnelGroup.General && header!.IsNull("ArchivedUtc") && header.Field<int>("CurrentLeaveCount") <= 1;
        periods.Enabled = ready; export.Enabled = ready && User.CanExportPersonalData;
        foreach (var view in sections.Values) view.Pager.UpdateState();
    }
    private async Task SaveAsync()
    {
        if (header == null) return;
        var request = new AccountingApp.Contracts.PersonnelCardDetails((AccountingApp.Contracts.PersonnelGroup)group, person, Version, first.Text, last.Text, User.CanViewIban ? iban.Text : null, "");
        if (await RunChangeOperationAsync(ct => Service.SaveDetailsAsync(request with { Reason = RemoteApi.Reason }, ct), "Personel bilgileri kaydedildi; bakiye korundu.", "Personel bilgileri")) await ReloadAsync();
    }
    private async Task TransitionGroupAsync()
    {
        if (header == null || !header.IsNull("ArchivedUtc") || !ConfirmDiscardChanges("Tarihli grup geçişi")) return;
        using var dialog = new PersonnelGroupTransitionDialog(User, group, person, Version, header.Field<Guid>("CurrentEmploymentId"), ((byte[])header["CurrentEmploymentVersion"]).ToArray());
        dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this);
        if (IsDisposed) return;
        if (dialog.Completed) { AcceptTrackedChanges(); Close(); } else await ReloadAsync();
    }
    private async Task EmploymentAsync()
    {
        if (header == null || !ConfirmDiscardChanges("Çalışma dönemlerini yönetme")) return;
        using var dialog = new EmploymentForm(group, person, Version, !header.IsNull("ArchivedUtc"), User); dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this);
        if (!IsDisposed) await ReloadAsync();
    }
    private async Task ArchiveAsync()
    {
        if (header == null || !ConfirmDiscardChanges("Arşiv işlemi")) return; byte[] version = Version; long tc = header.Field<long>("TC"); bool archived = !header.IsNull("ArchivedUtc");
        if (archived)
        {
            if (MessageBox.Show(this, "Aynı çalışma dönemi ve bakiye korunarak arşivleme geri alınacak. Yeni işe giriş için Çalışma dönemlerini yönet düğmesini kullanın.", "Arşivlemeyi geri al", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            if (await RunChangeOperationAsync(ct => People.RestoreArchivedAsync(group, person, version, ct), "Arşivleme geri alındı.")) await ReloadAsync();
        }
        else
        {
            using var date = new EmploymentDateDialog(false); if (date.ShowDialog(this) != DialogResult.OK) return; DateTime? end = date.SelectedDate;
            if (await RunChangeOperationAsync(ct => People.ArchiveAsync(group, tc, version, ct, end), "Personel arşivlendi; bakiye ve geçmiş korundu.")) await ReloadAsync();
        }
    }
    private async Task LeaveAsync()
    {
        if (header == null || !ConfirmDiscardChanges("Güncel dönem iznini düzenleme")) return;
        using var dialog = new PersonnelCardLeaveDialog(User, new(header.Field<long>("TC"), Version, header["CurrentLeaveVersion"] is byte[] v ? v.ToArray() : null), header["CurrentLeaveStart"] as DateTime?, header["CurrentLeaveEnd"] as DateTime?);
        dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this); if (!IsDisposed) await ReloadAsync();
    }
    private async Task ExportAsync()
    {
        var section = tabs.SelectedTab?.Tag as PersonnelCardSection?;
        DataGridView grid = section.HasValue ? sections[section.Value].Grid : jobs;
        if (grid.DataSource == null) return;
        using var data = ReportExport.Snapshot(grid);
        string periodScope = section is null or PersonnelCardSection.ArchiveEvents or PersonnelCardSection.Audit or PersonnelCardSection.GroupTransitions ? "Tüm dönemler" : SelectedPeriod?.ToString("D") ?? "Tüm dönemler";
        data.ExtendedProperties["Export.ReportTitle"] = "Personel kartı · " + tabs.SelectedTab?.Text;
        data.ExtendedProperties["Export.ReportScope"] = $"PersonId={person:D} · Dönem={periodScope} · Yalnız açık sayfa/görünüm";
        using var dialog = new SaveFileDialog { Filter = "CSV|*.csv", DefaultExt = "csv", FileName = "Personel-" + (section?.ToString() ?? "Donemler") + ".csv" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        await RunOperationAsync(ct => new PersonalDataExportService(AppDatabase.ConnectionString, User).ExportAsync(data, dialog.FileName, false, ct), "Açık görünüm dışa aktarıldı.");
    }
    private static void Format(DataGridView grid)
    {
        var captions = new Dictionary<string, string> { ["PersonnelGroup"] = "Grup (0 Genel / 1 Joker)", ["SourceGroup"] = "Önceki grup (0 Genel / 1 Joker)", ["TargetGroup"] = "Yeni grup (0 Genel / 1 Joker)", ["EffectiveDate"] = "Yeni gruba geçiş tarihi", ["IsLegacy"] = "Eski aktarım", ["IsClosed"] = "Kapalı", ["ProjectCode"] = "Proje kodu", ["ProjectName"] = "Proje", ["TeamName"] = "Ekip", ["StartDate"] = "Başlangıç", ["EndDate"] = "Bitiş", ["IZINBASLANGIC"] = "İzin başlangıcı", ["IZINBITIS"] = "İzin bitişi", ["IzinSuresi"] = "Gün", ["CreatedUtc"] = "Kayıt UTC", ["ChangedUtc"] = "Değişiklik UTC", ["Actor"] = "İşlemi yapan", ["CreatedBy"] = "Kaydeden", ["Amount"] = "Hakediş TL", ["Delta"] = "Hareket TL", ["RunningBalance"] = "Dönem içi net hareket TL", ["Reference"] = "Belge no", ["Description"] = "Açıklama", ["PaymentPurpose"] = "Ödeme türü", ["Kind"] = "Hareket türü", ["ReversedUtc"] = "İptal UTC", ["CancelledUtc"] = "İptal UTC", ["Reason"] = "Gerekçe", ["OldValues"] = "Eski değerler (maskeli)", ["NewValues"] = "Yeni değerler (maskeli)", ["ChangedFields"] = "Değişen alanlar", ["Balance"] = "Olay anındaki bakiye TL" };
        foreach (DataGridViewColumn c in grid.Columns) { if (captions.TryGetValue(c.Name, out string? title)) c.HeaderText = title; if (c.ValueType == typeof(decimal)) c.DefaultCellStyle.Format = "N2"; if (c.ValueType == typeof(Guid) || c.ValueType == typeof(byte[])) c.Visible = false; }
    }
    private void ShowAudit(string text)
    {
        using var dialog = new Form { Text = "İşlem geçmişi değeri", ClientSize = new(800, 550), MinimumSize = new(520, 360), StartPosition = FormStartPosition.CenterParent };
        try { using var json = System.Text.Json.JsonDocument.Parse(text); text = System.Text.Json.JsonSerializer.Serialize(json.RootElement, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }); } catch (System.Text.Json.JsonException) { }
        var field = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ShortcutsEnabled = false, ContextMenuStrip = new ContextMenuStrip(), ScrollBars = ScrollBars.Both, Text = text, WordWrap = false }; dialog.Controls.Add(field); AppTheme.Apply(dialog); dialog.ShowDialog(this);
    }
}

internal sealed class PersonnelCardLeaveDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    internal PersonnelCardLeaveDialog(UserSession user, LeaveTarget target, DateTime? from, DateTime? until)
    {
        Session = user; Text = "Muhasebe — Güncel çalışma dönemi izni"; ClientSize = new(560, 240);
        var start = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = from ?? DateTime.Today }; var end = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = until ?? DateTime.Today };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), AutoScroll = true, WrapContents = true };
        var note = new Label { Text = "Yalnız kartın güncel dönemindeki izin değiştirilir. Önceki tarihler geçmişte saklanır.", AutoSize = true, MaximumSize = new(510, 0) };
        var save = new Button { Text = "İzni kaydet", AutoSize = true }; var cancel = new Button { Text = "Vazgeç", AutoSize = true }; cancel.Click += (_, _) => Close();
        layout.Controls.AddRange([note, start, end, save, cancel]); Controls.Add(layout); TrackChanges("İzin", start, end); EditControls.AddRange([start, end, save]);
        save.Click += async (_, _) => { var period = new LeavePeriod(start.Value.Date, end.Value.Date); if (await RunChangeOperationAsync(ct => People.SaveLeavesAsync([(target, period)], ct), "İzin kaydedildi.", "İzin")) { DialogResult = DialogResult.OK; Close(); } };
        PageRequested += (_, _) => Close(); SessionRevoked += (_, _) => Close();
    }
}
