using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class ProjectPaymentsForm : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly Guid project; private readonly bool canPrepare, canApprove;
    private readonly DataGridView people = Grid(), proposals = Grid();
    private readonly ListPager peoplePager, proposalPager;
    private readonly Button prepare = Button("Seçili personel için talep hazırla…"), approve = Button("Seçili talebi onayla…"), reject = Button("Seçili talebi reddet…");
    private static DataGridView Grid() { var g = new DataGridView { Dock = DockStyle.Fill, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells }; RecordSelection.Configure(g); g.DataBindingComplete += (_, _) =>
        {
            var titles = new Dictionary<string,string> { ["AD"]="Ad",["SOYAD"]="Soyad",["Amount"]="Tutar TL",["Purpose"]="Ödeme türü",["Status"]="Durum",["PreparedBy"]="Hazırlayan",["PreparedUtc"]="Hazırlama UTC",["PrepareReason"]="Hazırlama gerekçesi",["DecidedBy"]="Karar veren",["DecidedUtc"]="Karar UTC",["DecisionReason"]="Karar gerekçesi",["LedgerId"]="Defter no",["ReversedUtc"]="Ödeme iptal UTC",["PersonnelGroup"]="Grup (0 Genel / 1 Joker)" };
            foreach(DataGridViewColumn c in g.Columns) { if(titles.TryGetValue(c.Name,out var title)) c.HeaderText=title; if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[])) c.Visible=false; if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2"; }
        };
        g.CellFormatting += (_,e) => { if(e.RowIndex<0||e.ColumnIndex<0)return; var name=g.Columns[e.ColumnIndex].Name; if(name=="Status") { e.Value=e.Value?.ToString() switch { "Pending"=>"Onay bekliyor","Approved"=>"Onaylandı / kaydedildi","Rejected"=>"Reddedildi",var v=>v }; e.FormattingApplied=true; } if(name=="Purpose") { e.Value=e.Value?.ToString()=="Advance"?"Avans":"Bakiye ödemesi";e.FormattingApplied=true; } }; return g; }
    private static Button Button(string text) => new() { Text = text, AutoSize = true };
    private static DataRow? Row(DataGridView g) => g.SelectedRows.Count == 1 && g.SelectedRows[0].DataBoundItem is DataRowView r ? r.Row : null;
    internal ProjectPaymentsForm(Guid project, UserSession user, bool canPrepare, bool canApprove)
    {
        this.project = project; this.canPrepare = canPrepare; this.canApprove = canApprove; Session = user; PersistGridLayout = false; Text = "Muhasebe — Proje ödeme hazırlama ve onay"; ClientSize = new(1120, 740);
        var tabs = new TabControl { Dock = DockStyle.Fill }; TabPage pool = new TabPage("Proje personeli"), queue = new TabPage("Ödeme talepleri"); pool.Controls.Add(people); queue.Controls.Add(proposals); tabs.TabPages.AddRange([pool, queue]); Controls.Add(tabs);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(8) }; Button refresh = Button("Yenile"), close = Button("Kapat"); bar.Controls.AddRange([prepare, approve, reject, refresh, close]); Controls.Add(bar);
        var note = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new(8), Text = "Hazırlama bakiyeyi değiştirmez. Başka bir yetkili onayladığında ödeme kaydedilir. TC/IBAN ve genel bakiye gösterilmez. Avans negatif bakiye oluşturabilir. Sonuç belirsizse açık işlem penceresinde aynı isteği yeniden gönderin." }; Controls.Add(note); ClientSizeChanged += (_, _) => note.MaximumSize = new(Math.Max(1, ClientSize.Width - 24), 0);
        peoplePager = ListPager.Attach(people, this, LoadPeopleAsync); proposalPager = ListPager.Attach(proposals, this, LoadProposalsAsync, "talep");
        prepare.Click += async (_, _) => await ActAsync(null); approve.Click += async (_, _) => await ActAsync(true); reject.Click += async (_, _) => await ActAsync(false); refresh.Click += async (_, _) => await ReloadAsync(); close.Click += (_, _) => Close();
        people.SelectionChanged += (_, _) => RefreshEditState(); proposals.SelectionChanged += (_, _) => RefreshEditState(); Load += async (_, _) => await ReloadAsync(); RefreshAction = ReloadAsync; SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(740, 520)); }
    private async Task ReloadAsync() { await LoadPeopleAsync(); if (!IsDisposed) await LoadProposalsAsync(); }
    private async Task LoadPeopleAsync() => await RunOperationAsync(async ct => { var page = await new ProjectAccessService(AppDatabase.ConnectionString, User).PeopleAsync(project, peoplePager.Index, peoplePager.SizeLimit, ct); if (IsDisposed) return; people.DataSource = page.Rows; people.ClearSelection(); peoplePager.Apply(page); });
    private async Task LoadProposalsAsync() => await RunOperationAsync(async ct => { var page = await new ProjectPaymentService(AppDatabase.ConnectionString, User).PageAsync(project, proposalPager.Index, proposalPager.SizeLimit, ct); if (IsDisposed) return; proposals.DataSource = page.Rows; proposals.ClearSelection(); proposalPager.Apply(page); });
    protected override void RefreshEditState() { bool ready = !IsOperationRunning; prepare.Enabled = ready && canPrepare && Row(people) != null; var row = Row(proposals); approve.Enabled = reject.Enabled = ready && canApprove && row?.Field<string>("Status") == "Pending" && !string.Equals(row.Field<string>("PreparedBy"), User.Username, StringComparison.OrdinalIgnoreCase); peoplePager?.UpdateState(); proposalPager?.UpdateState(); }
    private async Task ActAsync(bool? decision)
    {
        var row = Row(decision.HasValue ? proposals : people); if (row == null) return;
        using var dialog = new ProjectPaymentIntentDialog(project, User, row, decision); dialog.SessionRevoked += (_, _) => NotifySessionRevoked(); dialog.ShowDialog(this); if (!IsDisposed) await ReloadAsync();
    }
}
internal sealed class ProjectPaymentIntentDialog : SessionForm
{
    protected override bool ShowNavigationTools => false;
    private readonly Guid project, id, person; private readonly Wire.PersonnelGroup group; private readonly byte[] version; private readonly bool? decision;
    private readonly TextBox amount = new() { Width = 170 }; private readonly ComboBox purpose = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 }; private readonly Button send = new() { Text = "Kaydet", AutoSize = true };
    private Wire.ProjectPaymentPrepare? preparation; private Wire.ProjectPaymentDecide? choice;
    internal ProjectPaymentIntentDialog(Guid project, UserSession user, DataRow row, bool? decision)
    {
        this.project = project; this.decision = decision; Session = user; PersistGridLayout = false; Text = decision.HasValue ? (decision.Value ? "Proje ödemesini onayla" : "Proje ödemesini reddet") : "Proje ödemesi hazırla"; ClientSize = new(620, 320);
        person = row.Field<Guid>("PersonId", DataRowVersion.Original); group = (Wire.PersonnelGroup)row.Field<int>("PersonnelGroup", DataRowVersion.Original); version = ((byte[])row["RecordVersion", DataRowVersion.Original]).ToArray(); id = decision.HasValue ? row.Field<Guid>("RequestId", DataRowVersion.Original) : Guid.NewGuid();
        purpose.Items.AddRange(["Bakiye ödemesi", "Avans"]); purpose.SelectedIndex = decision.HasValue && row.Field<string>("Purpose") == "Advance" ? 1 : 0; if (decision.HasValue) amount.Text = MoneyInput.Format(row.Field<decimal>("Amount", DataRowVersion.Original));
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new(16), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true }; var close = new Button { Text = "Kapat", AutoSize = true }; close.Click += (_, _) => Close();
        panel.Controls.AddRange([new Label { Text = $"{row["AD"]} {row["SOYAD"]}\nİlk gönderimden sonra tutar/tür/gerekçe sabitlenir.\nAvans negatif bakiye oluşturabilir. Onay gerçek banka transferi yapmaz; uygulama defterine kayıt yapar.", AutoSize = true, MaximumSize = new(560, 0) }, purpose, amount, send, close]); Controls.Add(panel); TrackChanges("Ödeme talebi", purpose, amount); send.Click += async (_, _) => await SendAsync(); SessionRevoked += (_, _) => Close(); PageRequested += (_, _) => Close();
    }
    protected override void OnLoad(EventArgs e) { base.OnLoad(e); MinimumSize = LogicalToDeviceUnits(new Size(540, 320)); }
    protected override void RefreshEditState() { send.Enabled = !IsOperationRunning; amount.ReadOnly = decision.HasValue || preparation != null; purpose.Enabled = !IsOperationRunning && !decision.HasValue && preparation == null; send.Text = preparation != null || choice != null ? "Aynı isteği yeniden gönder" : "Kaydet"; }
    private async Task SendAsync()
    {
        decimal value = 0; if (!decision.HasValue && (!MoneyInput.TryParse(amount.Text, out value) || value <= 0)) { MessageBox.Show(this, "Pozitif iki ondalıklı tutar girin."); return; }
        bool done;
        async Task Execute(CancellationToken ct) { var service = new ProjectPaymentService(AppDatabase.ConnectionString, User); if (decision.HasValue) { choice ??= new(project, id, version.ToArray(), decision.Value, RemoteApi.Reason); await service.DecideAsync(choice, ct); } else { preparation ??= new(id, project, person, group, version.ToArray(), value, purpose.SelectedIndex == 1 ? Wire.PaymentPurpose.Advance : Wire.PaymentPurpose.Settlement, RemoteApi.Reason); await service.PrepareAsync(preparation, ct); } }
        if (preparation != null || choice != null) { using var reason = AuditContext.UseReason(preparation?.Reason ?? choice!.Reason); done = await RunOperationAsync(Execute, "İşlem sonucu alındı."); } else done = await RunChangeOperationAsync(Execute, decision == true ? "Onay ve ödeme kaydedildi." : decision == false ? "Talep reddedildi; ödeme yapılmadı." : "Talep hazırlandı; bakiye değişmedi.", "Ödeme talebi");
        if (done) { AcceptTrackedChanges(); Close(); }
    }
}
