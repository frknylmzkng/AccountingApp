using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class ReportCenterForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private sealed record Choice(string Key,string Caption){public override string ToString()=>Caption;}
    private sealed record Team(Guid? Id,string Caption){public override string ToString()=>Caption;}
    private sealed record Preset(Wire.ReportPreset Value){public override string ToString()=>Value.Name;}
    private readonly Guid project;private readonly bool canExport,canCosts,canPayments;
    private readonly ComboBox report=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=280},team=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=220},presets=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=240};
    private readonly DateTimePicker from=new(){Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false,Width=130},to=new(){Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false,Width=130};
    private readonly TextBox person=new(){Width=260,MaxLength=100,PlaceholderText="Kaynak adı veya kalıcı kişi kimliği"},name=new(){Width=220,MaxLength=80,PlaceholderText="Özel görünüm adı"};
    private readonly CheckBox pending=new(){Text="Yalnız bekleyen işlemler",AutoSize=true};
    private readonly DataGridView grid=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false};
    private readonly Button export=new(){Text="Tüm sonucu parçalı CSV / ZIP kaydet…",AutoSize=true},save=new(){Text="Yeni görünüm kaydet…",AutoSize=true},remove=new(){Text="Görünümü sil…",AutoSize=true};
    private readonly Label information=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)},note=new(){Dock=DockStyle.Bottom,AutoSize=true,Padding=new(8)};
    private readonly FlowLayoutPanel filters,views;private readonly ListPager pager;
    private Wire.ReportCenterScope? applied;private bool initialized;
    private ReportCenterService Service=>new(AppDatabase.ConnectionString,User);
    internal ReportCenterForm(Guid project,UserSession user,bool canExport,bool canCosts,bool canPayments)
    {
        this.project=project;this.canExport=canExport;this.canCosts=canCosts&&user.CanViewSalary;this.canPayments=canPayments&&this.canCosts;Session=user;PersistGridLayout=false;Text="Muhasebe — Proje rapor merkezi";ClientSize=new(1180,780);
        foreach(var pair in ReportCenterService.Reports)if(pair.Key=="Assignments"||this.canCosts)report.Items.Add(new Choice(pair.Key,pair.Value));report.SelectedIndex=0;
        report.AccessibleName = "Rapor türü";
        from.AccessibleName = "Başlangıç tarihi";
        to.AccessibleName = "Bitiş tarihi";
        team.AccessibleName = "Ekip filtresi";
        person.AccessibleName = "Kişi filtresi";
        presets.AccessibleName = "Kayıtlı kişisel görünüm";
        name.AccessibleName = "Yeni kişisel görünüm adı";
        grid.AccessibleName = "Rapor sonuçları";
        grid.AccessibleDescription = "Tablo seçili sayfayı gösterir. ZIP aktarımı filtreye uyan tüm sonucu kapsar.";
        grid.TabIndex = 4;
        Controls.Add(grid);pager=ListPager.Attach(grid,this,ReloadAsync,"satır");Controls.Add(information);
        var pick=new Button{Text="Kişi seç…",AutoSize=true};pick.Click+=(_,_)=>{if(IsOperationRunning)return;using var d=new CalendarPersonPicker(project,User,"Rapor için proje kişisi seç");d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(d.SelectedPerson is{} selected)person.Text=selected.Id.ToString("D");};
        filters=Bar(new Label{Text="Kayıt tarihi (İstanbul)",AutoSize=true,Padding=new(0,7,0,0)},from,to,team,person,pick,pending);
        var apply=new Button{Text="Raporu oluştur / yenile",AutoSize=true};var reset=new Button{Text="Filtreleri temizle",AutoSize=true};
        var links=Bar();
        void Link(string caption,Func<SessionForm> factory,bool enabled){var b=new Button{Text=caption,AutoSize=true,Enabled=enabled};links.Controls.Add(b);b.Click+=(_,_)=>{if(IsOperationRunning)return;using var d=factory();d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};}
        Link("Kişi-saat karşılaştırması…",()=>new ProjectWorkHoursForm(project,User,canExport),true);
        Link("Bütçe / maliyet sapması…",()=>new ProjectBudgetForm(project,User,canExport),this.canCosts);
        Link("Ödeme / tahsis mutabakatı…",()=>new ProjectReconciliationForm(project,User,canExport),this.canPayments);
        Link("Değişmez dönem kayıtları…",()=>new FiscalPeriodForm(project,User,canExport),this.canPayments);
        var commands = Bar(report, apply, reset, export);
        commands.TabIndex = 2;
        filters.TabIndex = 1;
        links.TabIndex = 3;
        Controls.Add(links);Controls.Add(commands);Controls.Add(filters);
        var load=new Button{Text="Görünümü uygula",AutoSize=true};views=Bar(presets,load,name,save,remove);views.TabIndex=0;Controls.Add(views);Controls.Add(note);
        note.Text="Görev listesi ücret/gider taleplerindeki bekleyen adımları gösterir; bir işin görünmesi onu onaylama yetkisi vermez. Kural onayı için kural raporunda yalnız bekleyen filtresini kullanın. Ekip: talep dönemiyle kesişen, iptal edilmemiş atama; geçmişte dondurulmuş ekip bilgisi değildir. Ekip maliyet paylaşımı yapılmaz. Proje giderleri kişi/ekip filtresinde dışarıda kalır. ZIP: en fazla 200.000 satır, 10.000 satırlık CSV parçaları; 32 MB arşiv. Diğer raporlar kendi kapsam ve sınırlarını kullanır.";
        report.SelectedIndexChanged+=(_,_)=>{pending.Checked=Value.Report=="Tasks";Changed();};team.SelectedIndexChanged+=(_,_)=>Changed();person.TextChanged+=(_,_)=>Changed();pending.CheckedChanged+=(_,_)=>Changed();from.ValueChanged+=(_,_)=>Changed();to.ValueChanged+=(_,_)=>Changed();from.MouseUp+=(_,_)=>Changed();to.MouseUp+=(_,_)=>Changed();from.KeyUp+=(_,_)=>Changed();to.KeyUp+=(_,_)=>Changed();presets.SelectedIndexChanged+=(_,_)=>RefreshEditState();name.TextChanged+=(_,_)=>RefreshEditState();
        apply.Click+=async(_,_)=>await ApplyAsync();reset.Click+=async(_,_)=>{if(IsOperationRunning)return;from.Checked=to.Checked=false;person.Clear();team.SelectedIndex=0;pending.Checked=false;await ApplyAsync();};
        load.Click+=async(_,_)=>{if(IsOperationRunning||presets.SelectedItem is not Preset p)return;var s=p.Value.Scope;var choice=report.Items.Cast<Choice>().FirstOrDefault(x=>x.Key==s.Report);if(choice==null){MessageBox.Show(this,"Bu görünüm için gereken ücret yetkisi yok.");return;}var selectedTeam=team.Items.Cast<Team>().FirstOrDefault(t=>t.Id==s.TeamId);if(selectedTeam==null){MessageBox.Show(this,"Görünümün ekibi artık bu projede bulunamıyor.");return;}report.SelectedItem=choice;team.SelectedItem=selectedTeam;from.Value=s.From??DateTime.Today;from.Checked=s.From.HasValue;to.Value=s.To??DateTime.Today;to.Checked=s.To.HasValue;person.Text=s.Person;pending.Checked=s.PendingOnly;await ApplyAsync();};
        save.Click+=async(_,_)=>{if(IsOperationRunning)return;var request=new Wire.ReportPreset(Guid.NewGuid(),name.Text,Value);pending.Enabled=false;await RunChangeOperationAsync(ct=>Service.SavePresetAsync(request,ct),"Özel görünüm kaydedildi.","Kişisel rapor görünümü kaydetme");await OptionsAsync();};
        remove.Click+=async(_,_)=>{if(IsOperationRunning||presets.SelectedItem is not Preset p)return;var id=p.Value.PresetId;pending.Enabled=false;await RunChangeOperationAsync(ct=>Service.DeletePresetAsync(new(project,id),ct),"Özel görünüm silindi.","Kişisel rapor görünümü silme");await OptionsAsync();};
        export.Click+=async(_,_)=>{if(IsOperationRunning||applied==null||applied!=Value)return;var scope=applied;using var d=new SaveFileDialog{Filter="ZIP arşivi|*.zip",FileName="proje-raporu-"+scope.Report+".zip",AddExtension=true,DefaultExt="zip"};if(d.ShowDialog(this)!=DialogResult.OK)return;pending.Enabled=false;await RunChangeOperationAsync(ct=>Service.SaveZipAsync(scope,d.FileName,ct),"Tüm rapor CSV parçalarıyla ZIP olarak kaydedildi.","Proje raporunu dışa aktarma");RefreshEditState();};
        Load+=async(_,_)=>{await OptionsAsync();initialized=true;await ApplyAsync();};RefreshAction=ReloadAsync;SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();SizeChanged+=(_,_)=>{information.MaximumSize=note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);};
        grid.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in grid.Columns){c.HeaderText=Title(c.Name);if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[]))c.Visible=false;if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N4";if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name.EndsWith("Utc",StringComparison.Ordinal)?"dd.MM.yyyy HH:mm:ss":"dd.MM.yyyy";}};
        grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&e.Value is string text){string key=grid.Columns[e.ColumnIndex].Name;if(key is "WorkflowState" or "Kind" or "ClaimKind" or "DecisionKind"){e.Value=State(text);e.FormattingApplied=true;}}};
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(800,600));}
    private static FlowLayoutPanel Bar(params Control[] c){var p=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};p.Controls.AddRange(c);return p;}
    private Wire.ReportCenterScope Value=>new(project,(report.SelectedItem as Choice)?.Key??"Assignments",from.Checked?from.Value.Date:null,to.Checked?to.Value.Date:null,(team.SelectedItem as Team)?.Id,person.Text.Trim(),pending.Checked);
    private void Changed(){if(initialized){applied=null;grid.DataSource=null;information.Text="Filtre değişti; raporu yeniden oluşturun.";pager.Apply(new(new DataTable(),0,0,pager.SizeLimit));}RefreshEditState();}
    protected override void RefreshEditState()
    {
        if(filters==null||views==null||pager==null)return;filters.Enabled=views.Enabled=report.Enabled=!IsOperationRunning;pending.Enabled=!IsOperationRunning&&(Value.Report is "Claims" or "Policies");
        export.Enabled=canExport&&!IsOperationRunning&&applied!=null&&applied==Value;save.Enabled=!IsOperationRunning&&name.Text.Trim().Length>0;remove.Enabled=!IsOperationRunning&&presets.SelectedItem is Preset;pager.UpdateState();
    }
    private async Task OptionsAsync()=>await RunOperationAsync(async ct=>{var options=await Service.OptionsAsync(project,ct);Guid? old=(team.SelectedItem as Team)?.Id;team.Items.Clear();team.Items.Add(new Team(null,"Tüm ekipler"));using var table=RemoteApi.Decode(options.Teams);foreach(DataRow row in table.Rows)team.Items.Add(new Team(row.Field<Guid>("TeamId"),row.Field<string>("Name")!));team.SelectedItem=team.Items.Cast<Team>().FirstOrDefault(x=>x.Id==old)??team.Items[0];presets.Items.Clear();foreach(var p in options.Presets)presets.Items.Add(new Preset(p));});
    private async Task ApplyAsync(){if(IsOperationRunning)return;applied=Value;pager.Reset();grid.DataSource=null;await ReloadAsync();}
    private async Task ReloadAsync()
    {
        if(IsOperationRunning)return;var scope=applied??Value;applied=null;grid.DataSource=null;information.Text="Rapor hazırlanıyor…";
        pending.Enabled=false;await RunOperationAsync(async ct=>{var result=await Service.BrowseAsync(new(scope,pager.Index,pager.SizeLimit),ct);if(IsDisposed)return;var data=RemoteApi.Decode(result.Page.Data);grid.DataSource=data;grid.ClearSelection();pager.Apply(new(data,result.Page.Total,result.Page.Index,result.Page.Size));information.Text=$"{result.Scope}\nVeri zamanı UTC: {result.ReadUtc:dd.MM.yyyy HH:mm:ss} · {result.Page.Total:N0} satır · ZIP sayfadaki satırlarla sınırlı değildir.";applied=scope;});
    }
    private static string Title(string n)=>n switch{"PersonCaption"=>"Kaynak adı","TeamName"=>"Ekip","RuleCode"=>"Kural kodu","RuleName"=>"Kural adı","Revision"=>"Sürüm","Kind"=>"Tür","ClaimKind"=>"Talep türü","FromDay"=>"Dönem başlangıcı","ToDay"=>"Dönem sonu","DocumentKey"=>"Belge no","Units"=>"Birim","UnitAmount"=>"Birim tutar TL","PremiumFactor"=>"Katsayı","Divisor"=>"Bölen","Amount"=>"Tutar TL","WorkflowState"=>"İşlem durumu","NextStep"=>"Bekleyen adım","DecisionKind"=>"Karar","DecidedBy"=>"Karar veren","DecidedUtc"=>"Karar UTC",_=>ProjectReportService.ReconciliationTitle(n)};
    private static string State(string n)=>n switch{"OvertimePremium"=>"Fazla mesai farkı","DailyWage"=>"Günlük ücret","MonthlyWage"=>"Aylık ücret","ExtraPay"=>"Ek ödeme","ProjectExpense"=>"Proje gideri","Prepared"=>"Hazırlandı","Approved"=>"Onaylı / aktarılmamış","Posted"=>"Deftere aktarıldı","Rejected"=>"Reddedildi","Cancelled"=>"İptal","Reversed"=>"Ters aktarıldı","ReverseRequested"=>"Ters onayı bekliyor","ReverseApproved"=>"Ters aktarım bekliyor","Post"=>"Deftere aktarım","Reverse"=>"Ters aktarım","Approve"=>"Onay","Reject"=>"Ret","Cancel"=>"İptal",_=>n};
}
