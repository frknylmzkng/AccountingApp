using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AccrualCorrectionsForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project,calculation;
    private readonly DataGridView items=AccrualFoundationForm.Grid(),history=AccrualFoundationForm.Grid();
    private readonly Button create=new(){Text="Seçili kişi için düzeltme hazırla…",AutoSize=true},approve=new(){Text="Seçili düzeltmeye bağımsız onay…",AutoSize=true},post=new(){Text="Düzeltme aktarım önizlemesi…",AutoSize=true};
    private AccrualFoundationService Service=>new(AppDatabase.ConnectionString,User);
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView r?r.Row:null;
    internal AccrualCorrectionsForm(Guid project,Guid calculation,UserSession user)
    {
        this.project=project;this.calculation=calculation;Session=user;PersistGridLayout=false;Text="Muhasebe — Bağlı mali düzeltme ve ters hareket";ClientSize=new(1100,720);
        var tabs=new TabControl{Dock=DockStyle.Fill};var a=new TabPage("Aktarım satırları / güncel tutar");a.Controls.Add(items);var b=new TabPage("Değişmez düzeltme geçmişi");b.Controls.Add(history);tabs.TabPages.AddRange([a,b]);Controls.Add(tabs);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Yenile",AutoSize=true};bar.Controls.AddRange([create,approve,post,refresh]);Controls.Add(bar);
        var info=new Label{Dock=DockStyle.Bottom,AutoSize=true,Padding=new(8),Text=$"Hesap: {calculation:D}\nEski hesap ve belge korunur. Yeni toplam 0 TL ise tam ters hareket; diğer değerler fark düzeltmesidir. Onay bakiye değiştirmez. Aktarım tek seferliktir, banka ödemesi değildir. Negatif bakiye doğabilir."};Controls.Add(info);SizeChanged+=(_,_)=>info.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        create.Click+=async(_,_)=>{if(Row(items) is not{} r||r.IsNull("FinanceEntryId"))return;using var dialog=new AccrualCorrectionCreateDialog(project,calculation,User,r);Watch(dialog);dialog.ShowDialog(this);if(!IsDisposed){await ReloadAsync();tabs.SelectedTab=b;}};
        approve.Click+=async(_,_)=>{if(Row(history) is not{} r)return;using var dialog=new AccrualCorrectionApproveDialog(project,calculation,User,r);Watch(dialog);dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        post.Click+=async(_,_)=>{if(Row(history) is not{} r)return;Guid id=r.Field<Guid>("CorrectionId");Wire.AccrualPostingQuote? quote=null;if(!await RunOperationAsync(async ct=>quote=await Service.CorrectionQuoteAsync(project,calculation,id,ct))||IsDisposed||quote==null)return;using var dialog=new AccrualCorrectionPostDialog(project,calculation,id,User,quote,r);Watch(dialog);dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;items.SelectionChanged+=(_,_)=>RefreshEditState();history.SelectionChanged+=(_,_)=>RefreshEditState();Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    private void Watch(SessionForm dialog)=>dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(780,520));}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>{var i=await Service.PostingItemsAsync(project,calculation,ct);var h=await Service.CorrectionHistoryAsync(project,calculation,ct);if(IsDisposed)return;items.DataSource=i;history.DataSource=h;items.ClearSelection();history.ClearSelection();});
    protected override void RefreshEditState(){bool admin=Session?.Role==UserRole.Admin&&!IsOperationRunning;var r=Row(history);create.Enabled=admin&&Row(items) is{} i&&!i.IsNull("FinanceEntryId");approve.Enabled=admin&&r!=null&&r.IsNull("ApprovalId")&&r.Field<bool>("IsCurrent");post.Enabled=admin&&r!=null&&!r.IsNull("ApprovalId")&&r.IsNull("TransferId")&&r.Field<bool>("IsCurrent");}
}
internal sealed class AccrualCorrectionCreateDialog:CalendarEditDialog
{
    private readonly Wire.AccrualCorrectionCreate request;
    private readonly NumericUpDown amount=new(){DecimalPlaces=2,Minimum=0,Maximum=9999999999999999.99m,ThousandsSeparator=true,Width=280};
    internal AccrualCorrectionCreateDialog(Guid project,Guid calculation,UserSession user,DataRow row):base(user,"Muhasebe — Bağlı mali düzeltme hazırla","Mali düzeltme hazırlığı")
    {
        request=new(project,calculation,Guid.NewGuid(),row.Field<Guid>("ItemId"),row.Field<Guid?>("LastCorrectionId"),row.Field<decimal>("EffectiveAmount"),0,"");amount.Value=request.OldAmount;
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Kişi: {row["PersonCaption"]}\nKaynak belge: {row["FinanceReference"]}\nİlk aktarım: {row.Field<decimal>("Amount"):N2} TL · Güncel hakediş: {request.OldAmount:N2} TL."});Field("Bu mali satırın düzeltilmiş toplam tutarı (fark tutarı değil)",amount);
        var reverse=new Button{Text="Tam ters hareket: yeni toplamı 0 TL yap",AutoSize=true};reverse.Click+=(_,_)=>amount.Value=0;Fields.Controls.Add(reverse);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Gerekçede hatayı ve doğru tutarın dayanağını açıklayın. Eski kaynak/oran/tutar silinmez veya açılmaz. Bu açık tutarlı mali düzeltmedir; puantajı yeniden hesaplamaz. Hazırlama ve mali onay bakiye değiştirmez; ayrı aktarım gerekir. Ödeme yapılmışsa negatif bakiye/borç doğabilir; banka iadesi yapılmaz. Yeni çalışan dönemine hareket taşınmaz."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{NewAmount=amount.Value,Reason=reason};
}
internal sealed class AccrualCorrectionApproveDialog:CalendarEditDialog
{
    private readonly Wire.AccrualCorrectionApprove request;
    internal AccrualCorrectionApproveDialog(Guid project,Guid calculation,UserSession user,DataRow row):base(user,"Muhasebe — Düzeltme bağımsız mali onayı","Mali düzeltme onayı")
    {
        request=new(project,calculation,row.Field<Guid>("CorrectionId"),Guid.NewGuid(),((byte[])row["RecordVersion",DataRowVersion.Original]).ToArray(),"");
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Düzeltme: {request.CorrectionId:D}\nHazırlayan: {row["CreatedBy"]}\nEski: {row.Field<decimal>("OldAmount"):N2} TL → Yeni: {row.Field<decimal>("NewAmount"):N2} TL\nFark: {row.Field<decimal>("Delta"):N2} TL\nGerekçe: {row["Reason"]}\nDüzeltme/asıl hesap hazırlayan ve asıl hesabın hak sahibiyle ilişkili hesap onaylayamaz. Onay değişmezdir, bu adım bakiye değiştirmez."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
internal sealed class AccrualCorrectionPostDialog:CalendarEditDialog
{
    private readonly Wire.AccrualCorrectionPost request;private readonly bool allowed;
    protected override bool CanSubmit=>allowed;
    internal AccrualCorrectionPostDialog(Guid project,Guid calculation,Guid correction,UserSession user,Wire.AccrualPostingQuote quote,DataRow row):base(user,"Muhasebe — Mali düzeltmeyi deftere aktar","Mali düzeltme aktarımı")
    {
        request=new(project,calculation,correction,Guid.NewGuid(),quote.Fingerprint,"");allowed=quote.CanPost;
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Eski hakediş: {row.Field<decimal>("OldAmount"):N2} TL → Yeni: {row.Field<decimal>("NewAmount"):N2} TL · Fark: {row.Field<decimal>("Delta"):N2} TL\nGerekçe: {row["Reason"]}\n"+(allowed?"Güncel bakiye ve sonraki bakiyeyi kontrol edin. Negatif bakiye borç gösterir; banka iadesi/ödeme yapılmaz.":string.Join("\n",quote.Issues))});
        var grid=AccrualFoundationForm.Grid();grid.Dock=DockStyle.None;grid.Width=800;grid.Height=260;grid.DataSource=RemoteApi.Decode(quote.Targets);Fields.Controls.Add(grid);Fields.SizeChanged+=(_,_)=>grid.Width=Math.Max(1,Fields.ClientSize.Width-48);ClientSize=new(950,650);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Fark tek transaction içinde bakiye, bağlantılı Allocation defteri, aktarım makbuzu ve maliyet görünümüne yansır. Asıl belgeler ve kapanış snapshot'ları değişmez. Yanıt belirsizse açık pencerede aynı isteği yeniden gönderin; pencere kapandıysa geçmişi yenileyin. Aktarılmış düzeltmeyi değiştirmek için güncel satırdan yeni bağımsız onaylı düzeltme hazırlayın."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>allowed?request with{Reason=reason}:throw new InvalidOperationException("Yeni aktarım önizlemesi gerekiyor.");
}
