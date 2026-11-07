using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class ProjectWorkHoursForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool canExport;
    private readonly TabControl tabs=new(){Dock=DockStyle.Fill};private readonly Dictionary<string,DataGridView> grids=new();private readonly Dictionary<string,ListPager> pagers=new();
    private readonly FinancialFilterBar filters;private Wire.FinancialFilter applied=new();private bool loaded;
    private readonly Label information=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)},note=new(){Dock=DockStyle.Bottom,AutoSize=true,Padding=new(8)};
    private readonly Button export=new(){Text="Seçili bölümün tüm filtre sonuçlarını CSV kaydet…",AutoSize=true};
    internal ProjectWorkHoursForm(Guid project,UserSession user,bool canExport)
    {
        this.project=project;this.canExport=canExport;Session=user;PersistGridLayout=false;Text="Muhasebe — Planlanan / gerçekleşen proje kişi-saat";ClientSize=new(1220,820);
        foreach(var pair in ProjectReportService.WorkforceSections)
        {
            var page=new TabPage(pair.Value){Tag=pair.Key};var grid=AccrualFoundationForm.Grid();grids[pair.Key]=grid;page.Controls.Add(grid);tabs.TabPages.Add(page);if(pair.Key!="Summary")pagers[pair.Key]=ListPager.Attach(grid,this,ReloadAsync,"satır");
            grid.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in grid.Columns){c.HeaderText=ProjectReportService.WorkforceTitle(c.Name);if(c.Name=="WorkDate")c.DefaultCellStyle.Format="dd.MM.yyyy";if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2";}};
            grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex<0)return;string name=grid.Columns[e.ColumnIndex].Name;if(name=="PersonnelGroup"&&e.Value is int g){e.Value=g==0?"Genel":"Joker";e.FormattingApplied=true;}if(name is "Status" or "SourceKind" or "ParentState" or "EffectiveState"){e.Value=ProjectReportService.WorkforceState(e.Value);e.FormattingApplied=true;}};
        }
        Controls.Add(tabs);Controls.Add(information);Controls.Add(note);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Güncel karşılaştırmayı oluştur / yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([refresh,export,close]);Controls.Add(bar);
        filters=new(this,project,user,NotifySessionRevoked,"İş günü / plan başlangıç günü:","Ad / kalıcı kişi no");Controls.Add(filters);filters.ApplyAsync=async()=>{applied=new FinancialQueryScope(filters.Value).Filter;loaded=false;foreach(var p in pagers.Values)p.Reset();foreach(var g in grids.Values)g.DataSource=null;information.Text="Yeni iş günü kapsamı yükleniyor…";await ReloadAsync();};filters.Changed+=RefreshEditState;
        note.Text="Plan kişi-saat = yayımlanmış aktif kişi atamalarının yerel net dakikası / 60. Gerçekleşme = bu projenin onaylı net UTC dakikası / 60; taslak ve bekleyen onay ayrı. Saat farkı = onaylı gerçekleşme − atanmış plan. Kişi filtresinde genel kadro ihtiyacı boş gösterilir. Gece yarısında çalışma başka güne bölünmez. Ücret dakikası net çalışma süresinden ayrıdır; parasal hakediş oluşturulmaz.";
        SizeChanged+=(_,_)=>information.MaximumSize=note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;close.Click+=(_,_)=>Close();export.Click+=async(_,_)=>await ExportAsync();Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,600));}
    protected override void RefreshEditState(){export.Enabled=canExport&&loaded&&!IsOperationRunning&&filters!=null&&filters.Matches(applied);if(filters!=null)filters.Enabled=!IsOperationRunning;foreach(var p in pagers.Values)p.UpdateState();}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>
    {
        var request=new Wire.ReconciliationBrowse(project,applied,pagers.ToDictionary(p=>p.Key,p=>new Wire.PageRequest(p.Value.Index,p.Value.SizeLimit)));var report=await new ProjectWorkHoursService(AppDatabase.ConnectionString,User).ReadAsync(request,ct);if(IsDisposed)return;
        grids["Summary"].DataSource=RemoteApi.Decode(report.Summary);grids["Summary"].ClearSelection();foreach(var pair in report.Sections){var data=RemoteApi.Decode(pair.Value.Data);grids[pair.Key].DataSource=data;grids[pair.Key].ClearSelection();pagers[pair.Key].Apply(new(data,pair.Value.Total,pair.Value.Index,pair.Value.Size));}
        long issues=report.Sections["Issues"].Total;information.Text=$"{report.ProjectCaption}\n{report.Scope}\nVeri zamanı UTC: {report.ReadUtc:dd.MM.yyyy HH:mm:ss} · Oluşturan: {report.ReadBy} · Eksik kayıt / istisna: {issues:N0}\nProje parçasının bağımsız onayı ile ana puantajın tam onayı ayrı gösterilir. CSV seçili bölümün tüm filtre sonuçlarını güncel veriden yeniden oluşturur.";loaded=true;
    });
    private async Task ExportAsync()
    {
        if(!loaded||!filters.Matches(applied)||tabs.SelectedTab?.Tag is not string section)return;using var dialog=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName="proje-kisi-saat-"+section+".csv",DefaultExt="csv",AddExtension=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        await RunChangeOperationAsync(ct=>new ProjectReportService(AppDatabase.ConnectionString,User).SaveCsvAsync(project,"Workforce."+section,dialog.FileName,ct,applied),"Güncel kişi-saat bölümü CSV olarak kaydedildi.","Proje kişi-saat raporunu dışa aktarma");
    }
}
