using System.Data;

namespace WinFormsApp2;

internal sealed class ProjectFinanceForm : SessionForm
{
    private sealed record ProjectChoice(Guid Id, string Caption) { public override string ToString() => Caption; }
    private sealed record PersonChoice(Guid Id, PersonnelGroup Group, long Tc, byte[] Version, string Caption) { public override string ToString() => Caption; }
    private readonly ComboBox project = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly ComboBox person = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 360 };
    private readonly ComboBox kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210 };
    private readonly TextBox code = new() { MaxLength = 30, Width = 140 }, projectName = new() { MaxLength = 100, Width = 270 };
    private readonly TextBox reference = new() { MaxLength = 50, Width = 220 }, description = new() { MaxLength = 200, Width = 360 };
    private readonly TextBox amount = new() { Width = 150 }, advanceAmount = new() { Width = 150 };
    private readonly DateTimePicker start = new() { Format = DateTimePickerFormat.Short, Width = 125 }, end = new() { Format = DateTimePickerFormat.Short, Width = 125 };
    private readonly DataGridView costs = Grid("ProjectCosts"), entries = Grid("FinanceEntries");
    private readonly Button create = new() { Text = "Proje oluştur", AutoSize = true }, post = new() { Text = "Belgeyi onayla ve kaydet", AutoSize = true },
        reverse = new() { Text = "Seçili belgeyi iptal et", AutoSize = true }, advance = new() { Text = "Avans ödemesi kaydet", AutoSize = true };
    private readonly Button budget=new(){Text="Proje bütçesi / maliyet sapması…",AutoSize=true};
    private readonly Button workHours=new(){Text="Plan / gerçekleşen kişi-saat…",AutoSize=true};
    private readonly Button reconciliation=new(){Text="Proje mutabakat raporu…",AutoSize=true};
    private readonly Button allocations=new(){Text="Ödeme / avans tahsisi…",AutoSize=true};
    private readonly Button accrualFoundation=new(){Text="Tarihli tarife / dönem hesabı…",AutoSize=true};
    private bool loading;
    private Guid? priorProject;
    private ProjectFinanceService Finance => new(AppDatabase.ConnectionString, User);
    private static DataGridView Grid(string name)
    {
        var grid = new DataGridView { Name = name, Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        RecordSelection.Configure(grid); return grid;
    }
    private static FlowLayoutPanel Flow(params Control[] controls)
    { var panel = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(8) }; panel.Controls.AddRange(controls); return panel; }
    private static Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new(6, 7, 6, 0) };
    private static void AddPage(TabControl tabs, string title, Control body, params Control[] header)
    { var page = new TabPage(title); page.Controls.Add(body); page.Controls.Add(Flow(header)); tabs.TabPages.Add(page); }
    internal ProjectFinanceForm()
    {
        Text = "Muhasebe — Hakediş, avans ve proje maliyeti"; ClientSize = new(1140, 760);
        kind.Items.AddRange(["Hakediş (personel alacağı)", "Diğer proje gideri"]); kind.SelectedIndex = 0;
        TrackChanges("Yeni proje", code, projectName);
        TrackChanges("Finans belgesi", kind, reference, start, end, amount, description);
        TrackChanges("Avans tutarı", advanceAmount);
        var reload = new Button { Text = "Yenile", AutoSize = true };
        var paymentPage = new Button { Text = "Ödeme sonucunu kontrol et / onayla", AutoSize = true };
        var tabs = new TabControl { Dock = DockStyle.Fill };
        AddPage(tabs, "Proje maliyetleri", costs, Label("Kod"), code, Label("Proje adı"), projectName, create);
        AddPage(tabs, "Hakediş ve gider belgeleri", entries, Label("Tür"), kind, Label("Belge no"), reference,
            Label("Başlangıç"), start, Label("Bitiş"), end, Label("Tutar TL"), amount, Label("Açıklama"), description, post, reverse);
        var advanceNote = new Label { Dock = DockStyle.Fill, Padding = new(16), Text =
            "Avans para çıkışıdır; yeni hakediş veya proje gideri değildir.\n\nAvans bakiyeyi azaltır; bakiye negatifse henüz hak edilmemiş ödeme vardır. Sonraki hakediş bu tutarı yalnızca bir kez karşılar.\n\nBanka transferi yapılmaz ve banka gerçekleşmesi doğrulanmaz. Kaydedilen veya sonucu belirsiz işlemi Ödemeler ekranından aynı işlem kimliğiyle kontrol edin. Yeni ödeme için önce mevcut sonucu onaylayın." };
        AddPage(tabs, "Avans", advanceNote, Label("Avans TL"), advanceAmount, advance, paymentPage);
        Controls.Add(tabs);
        var pick = new Button { Text = "Personel seç / ara", AutoSize = true };
        var wageRules=new Button{Text="Ek ücret / gider kuralları…",AutoSize=true};
        Load+=(_,_)=>wageRules.Visible=Session?.Role==UserRole.Admin;
        wageRules.Click+=(_,_)=>{if(IsOperationRunning||project.SelectedItem is not ProjectChoice selected||!ConfirmDiscardChanges("Ücret/gider kurallarını açma","Yeni proje"))return;using var d=new WageRulesForm(selected.Id,User);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        var fiscal=new Button{Text="Kalıcı mali dönem kapanışı…",AutoSize=true};
        Load+=(_,_)=>fiscal.Visible=Session?.Role==UserRole.Admin;
        fiscal.Click+=(_,_)=>{if(IsOperationRunning||project.SelectedItem is not ProjectChoice selected||!ConfirmDiscardChanges("Mali dönem kapanışını açma","Yeni proje"))return;using var d=new FiscalPeriodForm(selected.Id,User,User.CanExportPersonalData);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        var bank=new Button{Text="Banka CSV / ödeme eşleştirme…",AutoSize=true};
        Load+=(_,_)=>bank.Visible=Session?.Role==UserRole.Admin;
        bank.Click+=(_,_)=>{if(IsOperationRunning||Session?.Role!=UserRole.Admin||!ConfirmDiscardChanges("Banka eşleştirmesini açma","Yeni proje"))return;using var d=new BankReconciliationForm(User);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        var reportCenter=new Button{Text="Rapor merkezi…",AutoSize=true};
        reportCenter.Click+=(_,_)=>{if(IsOperationRunning||project.SelectedItem is not ProjectChoice selected||!ConfirmDiscardChanges("Rapor merkezini açma","Yeni proje"))return;using var d=new ReportCenterForm(selected.Id,User,User.CanExportPersonalData,User.CanViewSalary,User.Role==UserRole.Admin);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        var header = Flow(Label("Proje"), project, Label("Personel"), person, pick, reload,reportCenter, accrualFoundation,allocations,reconciliation,workHours,budget,bank,fiscal,wageRules); Controls.Add(header);
        budget.Click+=(_,_)=>{if(project.SelectedItem is not ProjectChoice chosen||!ConfirmDiscardChanges("Proje bütçesini açma","Yeni proje"))return;using var d=new ProjectBudgetForm(chosen.Id,User,User.CanExportPersonalData);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        workHours.Click+=(_,_)=>{if(project.SelectedItem is not ProjectChoice chosen||!ConfirmDiscardChanges("Proje kişi-saat karşılaştırmasını açma","Yeni proje"))return;using var d=new ProjectWorkHoursForm(chosen.Id,User,User.CanExportPersonalData);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        reconciliation.Click+=(_,_)=>{if(project.SelectedItem is not ProjectChoice chosen||!ConfirmDiscardChanges("Proje mutabakat raporunu açma","Yeni proje"))return;using var d=new ProjectReconciliationForm(chosen.Id,User,User.CanExportPersonalData);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};
        allocations.Click+=async(_,_)=>{if(project.SelectedItem is not ProjectChoice chosen||!ConfirmDiscardChanges("Tahsis ekranını açma","Yeni proje"))return;using var d=new PaymentAllocationsForm(chosen.Id,User,Session?.Role==UserRole.Admin);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await ReloadDataAsync();};
        accrualFoundation.Click+=async(_,_)=>{if(project.SelectedItem is not ProjectChoice chosen||!ConfirmDiscardChanges("Dönem hesabını açma","Yeni proje"))return;using var d=new AccrualFoundationForm(chosen.Id,User);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await ReloadDataAsync();};
        pick.Click += (_, _) =>
        {
            if (IsOperationRunning) return;
            if (!ConfirmDiscardChanges("Personel seçimini değiştirme", "Yeni proje")) return;
            using var dialog = new PersonnelPickerDialog(User);
            var result = dialog.ShowDialog(this);
            if (dialog.SessionInvalidated) { NotifySessionRevoked(); return; }
            if (result == DialogResult.OK && dialog.Selected is { } selected)
            {
                ClearFinanceDrafts();
                person.Items.Clear(); person.Items.Add(new PersonChoice(selected.Id, selected.Group, selected.Tc, selected.Version.ToArray(), selected.Caption));
                person.SelectedIndex = 0; RefreshEditState();
            }
        };
        var note = new Label { Dock = DockStyle.Bottom, Height = 44, Padding = new(8), Text =
            "Proje maliyeti = iptal edilmemiş hakediş + diğer gider. Avans ve ödeme bu maliyete yeniden eklenmez. Mevcut bakiye geçmiş hakediş sayılmaz. Belgeler: seçili projede en yeni 500 kayıt." };
        Controls.Add(note);
        reload.Click += async (_, _) => await ReloadAsync(); RefreshAction = ReloadDataAsync;
        project.SelectedIndexChanged += async (_, _) =>
        {
            if (loading) return;
            if (!ConfirmDiscardChanges("Proje seçimini değiştirme", "Yeni proje"))
            {
                loading = true;
                try { project.SelectedItem = project.Items.Cast<ProjectChoice>().FirstOrDefault(p => p.Id == priorProject); }
                finally { loading = false; }
                return;
            }
            ClearFinanceDrafts(); priorProject = (project.SelectedItem as ProjectChoice)?.Id; await LoadEntriesAsync();
        };
        kind.SelectedIndexChanged += (_, _) => RefreshEditState();
        entries.SelectionChanged += (_, _) => RefreshEditState();
        create.Click += CreateProject; post.Click += Post; reverse.Click += Reverse; advance.Click += Advance;
        paymentPage.Click += (_, _) => RequestPage(AppPage.Payments);
        Load += async (_, _) => { MinimumSize = LogicalToDeviceUnits(new Size(800, 600)); if (!PreviewOnly) await ReloadDataAsync(); RefreshEditState(); };
    }
    protected override void RefreshEditState()
    {
        bool admin = Session?.Role == UserRole.Admin && !IsOperationRunning;
        code.ReadOnly = projectName.ReadOnly = reference.ReadOnly = description.ReadOnly = amount.ReadOnly = !admin;
        kind.Enabled = start.Enabled = end.Enabled = admin;
        advanceAmount.ReadOnly = Session?.CanPay != true || IsOperationRunning;
        accrualFoundation.Enabled=!IsOperationRunning&&Session?.CanViewSalary==true&&project.SelectedItem!=null;
        budget.Enabled=admin&&project.SelectedItem!=null;
        workHours.Enabled=admin&&project.SelectedItem!=null;
        reconciliation.Enabled=admin&&project.SelectedItem!=null;
        allocations.Enabled=admin&&project.SelectedItem!=null;
        create.Enabled = admin; post.Enabled = admin && project.SelectedItem != null;
        reverse.Enabled = admin && entries.SelectedRows.Count == 1 && entries.SelectedRows[0].DataBoundItem is DataRowView selectedEntry && selectedEntry.Row.IsNull("AccrualCalculationId");
        advance.Enabled = Session?.CanPay == true && !IsOperationRunning && project.SelectedItem != null && person.SelectedItem != null;
        person.Enabled = !IsOperationRunning; // Also used by the separate advance tab.
    }
    private async Task ReloadAsync() { if (ConfirmDiscardChanges("Finans listelerini yenileme", "Yeni proje")) await ReloadDataAsync(); }
    private void ClearFinanceDrafts()
    {
        reference.Clear(); description.Clear(); amount.Clear(); advanceAmount.Clear(); start.Value = end.Value = DateTime.Today; kind.SelectedIndex = 0;
        AcceptTrackedChanges("Finans belgesi"); AcceptTrackedChanges("Avans tutarı");
    }
    private async Task ReloadDataAsync() => await RunOperationAsync(async token =>
    {
        Guid? projectId = (project.SelectedItem as ProjectChoice)?.Id;
        var selectedPerson = person.SelectedItem as PersonChoice;
        var projects = await Finance.ProjectsAsync(token);
        var people = selectedPerson == null ? null : (await People.PageAsync(null, 0, 1, token, tc: selectedPerson.Tc.ToString(System.Globalization.CultureInfo.InvariantCulture), includeLeaves: false)).Rows;
        if (IsDisposed || token.IsCancellationRequested) return;
        loading = true;
        try
        {
            costs.DataSource = projects; costs.ClearSelection();
            var headings = new Dictionary<string, string> { ["Code"] = "Proje kodu", ["Name"] = "Proje adı", ["LaborCost"] = "Hakediş maliyeti TL",
                ["OtherCost"] = "Diğer gider TL", ["TotalCost"] = "Toplam maliyet TL", ["AdvanceCash"] = "Avans çıkışı TL", ["SettlementCash"] = "Projeye bağlı ödeme TL" };
            costs.Columns["ProjectId"]!.Visible = false;
            foreach (var h in headings) { costs.Columns[h.Key]!.HeaderText = h.Value; if (h.Key.EndsWith("Cost") || h.Key.EndsWith("Cash")) costs.Columns[h.Key]!.DefaultCellStyle.Format = "N2"; }
            project.Items.Clear(); person.Items.Clear();
            foreach (DataRow row in projects.Rows) if (row.Field<bool>("IsActive")) project.Items.Add(new ProjectChoice(row.Field<Guid>("ProjectId"), row["Code"] + " · " + row["Name"]));
            foreach (var row in (people?.AsEnumerable() ?? Enumerable.Empty<DataRow>()).Where(r => r.Field<Guid>("PersonId") == selectedPerson?.Id))
                person.Items.Add(new PersonChoice(row.Field<Guid>("PersonId"), row["GRUP"].ToString() == "Joker" ? PersonnelGroup.Joker : PersonnelGroup.General,
                    row.Field<long>("TC"), ((byte[])row["RecordVersion"]).ToArray(), row["AD"] + " " + row["SOYAD"] + " · " + row["GRUP"]));
            project.SelectedItem = project.Items.Cast<ProjectChoice>().FirstOrDefault(p => p.Id == projectId);
            if (person.Items.Count == 1) person.SelectedIndex = 0;
            entries.DataSource = null;
            if (project.SelectedItem is ProjectChoice selected) entries.DataSource = await Finance.EntriesAsync(selected.Id, token);
            priorProject = (project.SelectedItem as ProjectChoice)?.Id; ClearFinanceDrafts();
        }
        finally { loading = false; }
    });
    private async Task LoadEntriesAsync() => await RunOperationAsync(async token =>
    {
        var choice = project.SelectedItem as ProjectChoice;
        var rows = choice == null ? new DataTable() : await Finance.EntriesAsync(choice.Id, token);
        if (!IsDisposed && !token.IsCancellationRequested) { entries.DataSource = rows; entries.ClearSelection(); }
    });
    private async void CreateProject(object? sender, EventArgs e)
    {
        string selectedCode = code.Text, selectedName = projectName.Text;
        if (await RunChangeOperationAsync(token => Finance.CreateProjectAsync(selectedCode, selectedName, token), "Proje kaydedildi.", "Yeni proje")) await ReloadAsync();
    }
    private async void Post(object? sender, EventArgs e)
    {
        if (project.SelectedItem is not ProjectChoice selected) return;
        if (!MoneyInput.TryParse(amount.Text, out decimal value)) { MessageBox.Show(this, MoneyInput.ValidationMessage); return; }
        var selectedPerson = kind.SelectedIndex == 0 ? person.SelectedItem as PersonChoice : null;
        FinanceDocument document;
        try { document = ProjectFinanceService.Normalize(new(selected.Id, kind.SelectedIndex == 0 ? "Accrual" : "Expense", reference.Text,
            selectedPerson?.Id, selectedPerson?.Group, selectedPerson?.Tc, selectedPerson?.Version, start.Value, end.Value, value, description.Text)); }
        catch (ArgumentException ex) { MessageBox.Show(this, ex.Message); return; }
        if (await RunChangeOperationAsync(token => Finance.PostAsync(document, token), "Belge kaydedildi; aynı proje/tür/belge numarası yeniden eklenmez.", "Finans belgesi")) await ReloadAsync();
    }
    private async void Reverse(object? sender, EventArgs e)
    {
        if (entries.SelectedRows.Count != 1 || entries.SelectedRows[0].DataBoundItem is not DataRowView row) return;
        Guid id = row.Row.Field<Guid>("EntryId", DataRowVersion.Original);
        byte[] version = ((byte[])row.Row["RecordVersion", DataRowVersion.Original]).ToArray();
        if (MessageBox.Show(this, "Belge iptal edilecek. Hakedişse bakiye aynı tutarda azalacak; negatif bakiye oluşabilir. Devam edilsin mi?", "Belge iptali", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        if (await RunChangeOperationAsync(token => Finance.ReverseAsync(id, version, token), "Belge iptal edildi; geçmiş korundu.")) await ReloadAsync();
    }
    private async void Advance(object? sender, EventArgs e)
    {
        if (project.SelectedItem is not ProjectChoice selected || person.SelectedItem is not PersonChoice selectedPerson) return;
        if (!MoneyInput.TryParse(advanceAmount.Text, out decimal value) || value <= 0) { MessageBox.Show(this, "Pozitif avans tutarı girin."); return; }
        byte[] version = selectedPerson.Version.ToArray();
        if (MessageBox.Show(this, selectedPerson.Caption + " için " + value.ToString("N2") + " TL avans kaydedilsin mi? Bakiye negatif olabilir.", "Avans onayı", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        if (await RunChangeOperationAsync(async token =>
            await new PaymentWorkflow(AppDatabase.ConnectionString, User).BeginAsync(selectedPerson.Group, selectedPerson.Tc, value, version, token, PaymentPurpose.Advance, selected.Id), savedGroup: "Avans tutarı"))
            RequestPage(AppPage.Payments);
    }
}
