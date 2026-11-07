using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class PaymentAllocationsForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool canWrite;private bool binding;private long? selectedPayment;
    private readonly DataGridView payments=AccrualFoundationForm.Grid(),accruals=AccrualFoundationForm.Grid(),history=AccrualFoundationForm.Grid();
    private readonly Button allocate=new(){Text="Seçili ödeme ve hakedişi tahsis et…",AutoSize=true},release=new(){Text="Seçili tahsisi serbest bırak…",AutoSize=true};
    private readonly FinancialFilterBar filters;private Wire.FinancialFilter applied=new();
    private readonly ListPager paymentPager,accrualPager,historyPager;
    private readonly Label note=new(){Dock=DockStyle.Bottom,AutoSize=true,Padding=new(8)};
    private PaymentAllocationService Service=>new(AppDatabase.ConnectionString,User);
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView v?v.Row:null;
    internal PaymentAllocationsForm(Guid project,UserSession user,bool canWrite)
    {
        this.project=project;this.canWrite=canWrite;Session=user;PersistGridLayout=false;Text="Muhasebe — Ödeme ve avans tahsisi";ClientSize=new(1150,820);
        var tabs=new TabControl{Dock=DockStyle.Fill};var matching=new TabPage("Ödeme → hakediş eşleştirme");var past=new TabPage("Tahsis / serbest bırakma geçmişi");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new(8)};layout.RowStyles.Add(new(SizeType.Percent,50));layout.RowStyles.Add(new(SizeType.Percent,50));
        var pg=new GroupBox{Text="1. Ödeme / avans seçin",Dock=DockStyle.Fill,Padding=new(8)};pg.Controls.Add(payments);var fg=new GroupBox{Text="2. Aynı kişi ve çalışma döneminin hakedişini seçin",Dock=DockStyle.Fill,Padding=new(8)};fg.Controls.Add(accruals);layout.Controls.Add(pg,0,0);layout.Controls.Add(fg,0,1);matching.Controls.Add(layout);past.Controls.Add(history);tabs.TabPages.AddRange([matching,past]);Controls.Add(tabs);
        paymentPager=ListPager.Attach(payments,this,ReloadPaymentsAsync,"ödeme");accrualPager=ListPager.Attach(accruals,this,ReloadAccrualsAsync,"hakediş");historyPager=ListPager.Attach(history,this,ReloadHistoryAsync,"tahsis");
        var refresh=new Button{Text="Yenile",AutoSize=true};var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};bar.Controls.AddRange([allocate,release,refresh]);Controls.Add(bar);
        filters=new(this,project,user,NotifySessionRevoked);Controls.Add(filters);filters.ApplyAsync=async()=>{applied=new FinancialQueryScope(filters.Value).Filter;paymentPager.Reset();historyPager.Reset();accrualPager.Reset();payments.DataSource=history.DataSource=accruals.DataSource=null;note.Text="Yeni filtre kapsamı yükleniyor…";await ReloadAsync();};filters.Changed+=RefreshEditState;
        Controls.Add(note);SizeChanged+=(_,_)=>note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        payments.SelectionChanged+=async(_,_)=>{if(binding||IsOperationRunning)return;selectedPayment=filters.Matches(applied)?Row(payments)?.Field<long>("PaymentLedgerId"):null;accrualPager.Reset();accruals.DataSource=null;if(selectedPayment.HasValue)await ReloadAccrualsAsync();RefreshEditState();};accruals.SelectionChanged+=(_,_)=>RefreshEditState();history.SelectionChanged+=(_,_)=>RefreshEditState();
        allocate.Click+=async(_,_)=>{if(!filters.Matches(applied)||Row(payments) is not{} p||Row(accruals) is not{} f)return;long payment=p.Field<long>("PaymentLedgerId");Guid finance=f.Field<Guid>("FinanceEntryId");Wire.AccrualPostingQuote? quote=null;if(!await RunOperationAsync(async ct=>quote=await Service.QuoteAsync(project,payment,finance,ct))||IsDisposed||quote==null)return;using var d=new PaymentAllocationDialog(project,payment,finance,User,quote);Watch(d);d.ShowDialog(this);if(!IsDisposed){await ReloadAsync();tabs.SelectedTab=past;}};
        release.Click+=async(_,_)=>{if(!filters.Matches(applied)||Row(history) is not{} row||!row.IsNull("ReleaseId"))return;using var d=new PaymentAllocationReleaseDialog(project,User,row);Watch(d);d.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    private void Watch(SessionForm f)=>f.SessionRevoked+=(_,_)=>NotifySessionRevoked();
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,640));}
    private Wire.AllocationBrowse Request(string section,ListPager pager)=>new(project,section,applied,pager.Index,pager.SizeLimit,selectedPayment);
    private void Bind(DataGridView grid,ListPager pager,Wire.Page result){grid.DataSource=RemoteApi.Decode(result.Data);grid.ClearSelection();pager.Apply(new((DataTable)grid.DataSource,result.Total,result.Index,result.Size));}
    private async Task ReloadPaymentsAsync()=>await RunOperationAsync(async ct=>{selectedPayment=null;payments.ClearSelection();accruals.DataSource=null;accrualPager.Reset();var r=await Service.PageAsync(Request("Payments",paymentPager),ct);if(IsDisposed)return;binding=true;try{Bind(payments,paymentPager,r);}finally{binding=false;}});
    private async Task ReloadAccrualsAsync()=>await RunOperationAsync(async ct=>{long? expected=selectedPayment;accruals.ClearSelection();var r=await Service.PageAsync(Request("Accruals",accrualPager),ct);if(!IsDisposed&&expected==Row(payments)?.Field<long>("PaymentLedgerId"))Bind(accruals,accrualPager,r);});
    private async Task ReloadHistoryAsync()=>await RunOperationAsync(async ct=>{history.ClearSelection();var r=await Service.PageAsync(Request("Allocations",historyPager),ct);if(!IsDisposed)Bind(history,historyPager,r);});
    private async Task ReloadAsync()
    {
        selectedPayment=null;accruals.DataSource=null;accrualPager.Reset();
        await RunOperationAsync(async ct=>{var p=await Service.PageAsync(Request("Payments",paymentPager),ct);var h=await Service.PageAsync(Request("Allocations",historyPager),ct);if(IsDisposed)return;binding=true;try{Bind(payments,paymentPager,p);Bind(history,historyPager,h);}finally{binding=false;}note.Text=new FinancialQueryScope(applied).Description+"\nTahsis para hareketi değildir. Ödeme/hakediş sınırları tüm aktif tahsisleri içerir; sayfa veya filtre sınırları parasal kapasiteyi değiştirmez. Filtre değişikliklerinden sonra Uygula'ya basın.";});
    }
    protected override void RefreshEditState(){bool ready=canWrite&&!IsOperationRunning&&filters!=null&&filters.Matches(applied);allocate.Enabled=ready&&Row(payments) is{} p&&p.Field<decimal>("PaymentAvailable")>0&&Row(accruals) is{} f&&f.Field<decimal>("AccrualAvailable")>0;release.Enabled=ready&&Row(history) is{} r&&r.IsNull("ReleaseId");if(filters!=null)filters.Enabled=!IsOperationRunning;paymentPager?.UpdateState();accrualPager?.UpdateState();historyPager?.UpdateState();}
}
internal sealed class PaymentAllocationDialog:CalendarEditDialog
{
    private readonly Wire.PaymentAllocationCreate request;private readonly bool allowed;
    private readonly NumericUpDown amount=new(){DecimalPlaces=2,Minimum=0,Maximum=9999999999999999.99m,ThousandsSeparator=true,Width=280};
    protected override bool CanSubmit=>allowed;
    internal PaymentAllocationDialog(Guid project,long payment,Guid finance,UserSession user,Wire.AccrualPostingQuote quote):base(user,"Muhasebe — Ödeme/avans tahsisi","Ödeme/avans tahsisi")
    {
        request=new(project,Guid.NewGuid(),payment,finance,0,quote.Fingerprint,"");allowed=quote.CanPost;var data=RemoteApi.Decode(quote.Targets);var r=data.Rows[0];decimal available=Math.Max(0,Math.Min(r.Field<decimal>("PaymentAvailable"),r.Field<decimal>("AccrualAvailable")));amount.Maximum=available;
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Ödeme defter no: {payment} · Tür: {(r["PaymentPurpose"].ToString()=="Advance"?"Avans":"Hakediş ödemesi")}\nHakediş belge no: {r["FinanceReference"]}\nÖdeme: {r.Field<decimal>("PaymentAmount"):N2} TL · Tahsis edilmemiş: {r.Field<decimal>("PaymentAvailable"):N2} TL\nDüzeltilmiş hakediş: {r.Field<decimal>("AccrualAmount"):N2} TL · Tahsis edilmemiş: {r.Field<decimal>("AccrualAvailable"):N2} TL\n"+(allowed?"Tahsis edilecek pozitif tutarı girin.":string.Join("\n",quote.Issues))});Field("Tahsis tutarı TL",amount);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Bu kayıt mevcut ödemeyi bu hakedişe bağlar. Yeni banka işlemi, ödeme, defter hareketi veya bakiye düşümü oluşturmaz. Yanıt belirsizse açık pencerede aynı isteği yeniden gönderin; pencere kapandıysa önce geçmişi yenileyin."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>allowed?request with{Amount=amount.Value,Reason=reason}:throw new InvalidOperationException("Yeni tahsis önizlemesi gerekiyor.");
}
internal sealed class PaymentAllocationReleaseDialog:CalendarEditDialog
{
    private readonly Wire.PaymentAllocationRelease request;
    internal PaymentAllocationReleaseDialog(Guid project,UserSession user,DataRow row):base(user,"Muhasebe — Tahsisi serbest bırak","Tahsis serbest bırakma")
    {
        request=new(project,row.Field<Guid>("AllocationId"),Guid.NewGuid(),"");Fields.Controls.Add(new Label{AutoSize=true,Text=$"Ödeme defter no: {row["PaymentLedgerId"]}\nHakediş belge no: {row["FinanceReference"]}\nTahsis: {row.Field<decimal>("Amount"):N2} TL\nİlk gerekçe: {row["Reason"]}\nGerekçeyle serbest bırakıldığında ödeme ve hakedişin tahsis edilebilir kısmı açılır. Eski bağlantı ve serbest bırakma geçmişi korunur. Ödeme, bakiye, maliyet ve banka hareketi değişmez; yeni eşleştirme ayrıca yapılır."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
