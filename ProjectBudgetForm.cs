using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed class ProjectBudgetForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool canExport;
    private readonly DataGridView summary=AccrualFoundationForm.Grid(),history=AccrualFoundationForm.Grid(),costs=AccrualFoundationForm.Grid();
    private readonly ListPager historyPager,costPager;private readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    private readonly Label info=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)},note=new(){Dock=DockStyle.Bottom,AutoSize=true,Padding=new(8)};
    private readonly Button revise=new(){Text="İlk bütçeyi / yeni revizyonu kaydet…",AutoSize=true},export=new(){Text="Seçili bölümün tamamını güncel CSV kaydet…",AutoSize=true};
    private DataRow? current;
    internal ProjectBudgetForm(Guid project,UserSession user,bool canExport)
    {
        this.project=project;this.canExport=canExport;Session=user;PersistGridLayout=false;Text="Muhasebe — Proje bütçesi ve maliyet sapması";ClientSize=new(1180,780);
        foreach(var pair in new[]{("Summary",summary),("History",history),("Costs",costs)}){var page=new TabPage(ProjectReportService.BudgetSections[pair.Item1]){Tag=pair.Item1};page.Controls.Add(pair.Item2);tabs.TabPages.Add(page);var grid=pair.Item2;grid.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in grid.Columns)c.HeaderText=ProjectReportService.BudgetTitle(c.Name);};grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name=="Kind"){e.Value=e.Value?.ToString()=="Accrual"?"İşçilik hakedişi":"Diğer gider";e.FormattingApplied=true;}};}
        historyPager=ListPager.Attach(history,this,ReloadAsync,"revizyon");costPager=ListPager.Attach(costs,this,ReloadAsync,"belge");Controls.Add(tabs);Controls.Add(info);Controls.Add(note);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Güncel bütçe / maliyeti yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([revise,refresh,export,close]);Controls.Add(bar);
        note.Text="Kapsam: tüm proje ömrü, TL. Gerçekleşen maliyet = iptal edilmemiş, düzeltilmiş işçilik hakedişi + diğer gider. Ödeme, avans ve tahsis maliyete tekrar eklenmez. Sapma = gerçekleşen − bütçe; pozitif değer aşımı gösterir. Bütçe tanımlı değilse sapma boş kalır; sıfır bütçede yüzde hesaplanmaz. Revizyon plan değişikliğidir; bakiyeye, hakedişe veya ödemeye dokunmaz.";
        SizeChanged+=(_,_)=>info.MaximumSize=note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        revise.Click+=async(_,_)=>{if(current==null)return;using var d=new ProjectBudgetDialog(project,User,current);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;export.Click+=async(_,_)=>await ExportAsync();close.Click+=(_,_)=>Close();Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,580));}
    protected override void RefreshEditState(){revise.Enabled=!IsOperationRunning&&Session?.Role==UserRole.Admin&&current?.Field<bool>("IsActive")==true;export.Enabled=!IsOperationRunning&&canExport&&current!=null;historyPager?.UpdateState();costPager?.UpdateState();}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>
    {
        var pages=new Dictionary<string,Wire.PageRequest>{{"History",new(historyPager.Index,historyPager.SizeLimit)},{"Costs",new(costPager.Index,costPager.SizeLimit)}};var r=await new ProjectBudgetService(AppDatabase.ConnectionString,User).ReadAsync(new(project,pages),ct);if(IsDisposed)return;
        var sums=RemoteApi.Decode(r.Summary);summary.DataSource=sums;summary.ClearSelection();current=sums.Rows[0];var h=RemoteApi.Decode(r.History.Data);history.DataSource=h;history.ClearSelection();historyPager.Apply(new(h,r.History.Total,r.History.Index,r.History.Size));var c=RemoteApi.Decode(r.Costs.Data);costs.DataSource=c;costs.ClearSelection();costPager.Apply(new(c,r.Costs.Total,r.Costs.Index,r.Costs.Size));
        string status=current.IsNull("BudgetId")?"Henüz bütçe yok":$"Güncel bütçe: {current["BudgetId"]}";if(!current.IsNull("CostVariance")&&current.Field<decimal>("CostVariance")>0)status+=" · BÜTÇE AŞILDI";info.Text=$"{r.ProjectCaption}\n{status}\nVeri zamanı UTC: {r.ReadUtc:dd.MM.yyyy HH:mm:ss} · Oluşturan: {r.ReadBy} · Revizyonlar değişmez geçmişte tutulur.";
    });
    private async Task ExportAsync(){if(current==null||tabs.SelectedTab?.Tag is not string section)return;using var d=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName="proje-butce-"+section+".csv",DefaultExt="csv",AddExtension=true};if(d.ShowDialog(this)!=DialogResult.OK)return;await RunChangeOperationAsync(ct=>new ProjectReportService(AppDatabase.ConnectionString,User).SaveCsvAsync(project,"Budget."+section,d.FileName,ct),"Güncel bütçe bölümü CSV kaydedildi.","Proje bütçesini dışa aktarma");}
}
internal sealed class ProjectBudgetDialog:CalendarEditDialog
{
    private readonly Wire.ProjectBudgetSave request;
    private readonly NumericUpDown labor=new(){DecimalPlaces=2,Minimum=0,Maximum=9999999999999999.99m,ThousandsSeparator=true,Width=300},other=new(){DecimalPlaces=2,Minimum=0,Maximum=9999999999999999.99m,ThousandsSeparator=true,Width=300};
    internal ProjectBudgetDialog(Guid project,UserSession user,DataRow current):base(user,"Muhasebe — Gerekçeli proje bütçesi revizyonu","Proje bütçesi revizyonu")
    {
        request=new(project,Guid.NewGuid(),current.Field<Guid?>("BudgetId"),((byte[])current["ProjectVersion",DataRowVersion.Original]).ToArray(),0,0,"");labor.Value=current.Field<decimal?>("LaborBudget")??0;other.Value=current.Field<decimal?>("OtherBudget")??0;
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Proje: {current["Code"]} · {current["Name"]}\nÖnceki bütçe: {(current.IsNull("BudgetId")?"Tanımlı değil":current["BudgetId"])}\nİşçilik gerçekleşeni: {current.Field<decimal>("ActualLabor"):N2} TL · Diğer gider gerçekleşeni: {current.Field<decimal>("ActualOther"):N2} TL"});Field("Yeni toplam işçilik bütçesi TL (fark değil)",labor);Field("Yeni toplam diğer gider bütçesi TL (fark değil)",other);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Tüm proje ömrü için açıkça yeni toplamları girin. Önceki bütçe silinmez/değişmez; gerekçeli yeni revizyon kaydedilir. Sıfır bütçe açık bir karardır; bütçe yok ile aynı değildir. Eşzamanlı revizyonda yeniden inceleme gerekir. Bu kayıt ödeme, bakiye veya gider üretmez; bütçe aşımı mali işlemleri otomatik engellemez. Yanıt belirsizse aynı açık pencereden aynı isteği yeniden gönderin; pencere kapandıysa geçmişi yenileyin."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{LaborBudget=labor.Value,OtherBudget=other.Value,Reason=reason};
}
