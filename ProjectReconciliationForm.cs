using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class ProjectReconciliationForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool canExport;
    private readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    private readonly Dictionary<string,DataGridView> grids=new();
    private readonly Label information=new(){AutoSize=true,Padding=new(8)},note=new(){AutoSize=true,Padding=new(8)};
    private readonly Button export=new(){Text="Seçili bölümü güncel CSV olarak kaydet…",AutoSize=true};
    private bool loaded;
    private readonly Dictionary<string,ListPager> pagers=new();
    private readonly FinancialFilterBar filters;
    private Wire.FinancialFilter applied=new();
    internal ProjectReconciliationForm(Guid project,UserSession user,bool canExport)
    {
        this.project=project;this.canExport=canExport;Session=user;PersistGridLayout=false;Text="Muhasebe — Proje hakediş / ödeme / tahsis mutabakatı";ClientSize=new(1200,800);
        foreach(var pair in ProjectReportService.ReconciliationSections){var page=new TabPage(pair.Value){Tag=pair.Key};var grid=AccrualFoundationForm.Grid();grids[pair.Key]=grid;page.Controls.Add(grid);tabs.TabPages.Add(page);if(pair.Key!="Summary"){var key=pair.Key;pagers[key]=ListPager.Attach(grid,this,ReloadAsync,"satır");}
            grid.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in grid.Columns){c.HeaderText=ProjectReportService.ReconciliationTitle(c.Name);if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2";}};
            grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name=="PersonnelGroup"&&e.Value is int group){e.Value=group==0?"Genel":"Joker";e.FormattingApplied=true;}};
        }
        Controls.Add(tabs);information.Dock=DockStyle.Top;Controls.Add(information);note.Dock=DockStyle.Bottom;Controls.Add(note);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Güncel raporu oluştur / yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([refresh,export,close]);Controls.Add(bar);filters=new(this,project,user,NotifySessionRevoked);Controls.Add(filters);filters.ApplyAsync=async()=>{applied=new FinancialQueryScope(filters.Value).Filter;foreach(var p in pagers.Values)p.Reset();foreach(var g in grids.Values)g.DataSource=null;loaded=false;information.Text="Yeni filtre kapsamı yükleniyor…";await ReloadAsync();};filters.Changed+=RefreshEditState;
        note.Text="Kapsam: uygulanan kaynak kayıt tarihi / kişi filtresi. Kapsam dışındaki kaynakla tahsisler ayrıca gösterilir. Özet iptal edilmemiş kaynakları içerir; ayrıntılarda iptaller ve serbest bırakılan tahsisler ayrıca görünür. Tahsis edilmemiş hakediş, ödenmemiş hakedişle aynı değildir. Hakediş eksi ödeme proje içi net konumdur; kişinin genel bakiyesi değildir. CSV seçili bölümün filtreye uyan tüm satırlarını yeni veri zamanıyla oluşturur; yalnız sayfa aktarımı değildir.";
        SizeChanged+=(_,_)=>{information.MaximumSize=note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);};
        refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;close.Click+=(_,_)=>Close();export.Click+=async(_,_)=>await ExportAsync();Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(800,580));}
    protected override void RefreshEditState(){export.Enabled=canExport&&loaded&&!IsOperationRunning&&filters!=null&&filters.Matches(applied);if(filters!=null)filters.Enabled=!IsOperationRunning;foreach(var pager in pagers.Values)pager.UpdateState();}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>
    {
        Wire.ProjectReconciliation r=await new ProjectReconciliationService(AppDatabase.ConnectionString,User).ReadAsync(project,ct,new(applied,pagers.ToDictionary(p=>p.Key,p=>new Wire.PageRequest(p.Value.Index,p.Value.SizeLimit))));if(IsDisposed)return;
        var sections=new Dictionary<string,Wire.Table>{{"Summary",r.Summary},{"People",r.People},{"Accruals",r.Accruals},{"Payments",r.Payments},{"Allocations",r.Allocations},{"Issues",r.Issues}};
        foreach(var pair in sections){grids[pair.Key].DataSource=RemoteApi.Decode(pair.Value);grids[pair.Key].ClearSelection();if(pagers.TryGetValue(pair.Key,out var pager)&&r.Pages?.TryGetValue(pair.Key,out var meta)==true)pager.Apply(new((DataTable)grids[pair.Key].DataSource!,meta.Total,meta.Index,meta.Size));}
        long issues=r.Pages?.GetValueOrDefault("Issues")?.Total??0;var summary=((DataTable)grids["Summary"].DataSource!).Rows[0];decimal difference=summary.Field<decimal>("UnexplainedDifference");
        information.Text=$"{r.ProjectCaption}\n{r.Scope}\nVeri zamanı UTC: {r.ReadUtc:dd.MM.yyyy HH:mm:ss} · Oluşturan: {r.ReadBy} · İstisna: {issues} · Açıklanamayan tahsis farkı: {difference:N2} TL\n"+(issues>0||difference!=0?"İstisnaları inceleyin; sıfır proje farkı tek başına kayıtların doğru eşlendiğini göstermez.":"Listelenen kaynak istisnası yok. Bu rapor banka gerçekleşmesini doğrulamaz.");loaded=true;
    });
    private async Task ExportAsync()
    {
        if(!loaded||tabs.SelectedTab?.Tag is not string section)return;using var dialog=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName="proje-mutabakat-"+section+".csv",DefaultExt="csv",AddExtension=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        await RunChangeOperationAsync(ct=>new ProjectReportService(AppDatabase.ConnectionString,User).SaveCsvAsync(project,"Reconciliation."+section,dialog.FileName,ct,applied),"Güncel bölüm CSV olarak kaydedildi.","Proje mutabakat raporunu dışa aktarma");
    }
}
