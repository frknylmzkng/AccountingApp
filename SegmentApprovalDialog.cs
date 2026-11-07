using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class SegmentApprovalDialog : CalendarEditDialog
{
    private readonly Wire.SegmentDecision request;
    internal SegmentApprovalDialog(Guid project,UserSession user,DataRow row,string state):base(user,state=="Approved"?"Muhasebe — Projeye ait parçayı onayla":"Muhasebe — Projeye ait parçayı iade et","Proje parça kararı")
    {
        request=new(project,Guid.NewGuid(),row.Field<Guid>("SegmentId"),row.Field<Guid?>("RoundId"),row["ApprovalVersion"] is byte[] v?v.ToArray():null,((byte[])row["ParentVersion",DataRowVersion.Original]).ToArray(),((byte[])row["SetVersion",DataRowVersion.Original]).ToArray(),state,"");
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"{row["AD"]} {row["SOYAD"]} · İş günü {row.Field<DateTime>("WorkDate"):dd.MM.yyyy}\nYalnız açık olan projeye ait parça: {row.Field<DateTime>("LocalStart"):dd.MM.yyyy HH:mm} → {row.Field<DateTime>("LocalEnd"):dd.MM.yyyy HH:mm}\nMola: {row["BreakMinutes"]} dk · Net: {row["NetMinutes"]} dk · Ücretlendirilecek: {row["PayableMinutes"]} dk.\nMevcut durum: {row["EffectiveState"]}."});
        Fields.Controls.Add(new Label{AutoSize=true,Text=state=="Approved"?"Kaydet ve gerekçe yalnız bu parçayı onaylar; başka projedeki parçaya onay verilmez. Hazırlayan, düzenleyen, gönderen ve bağlı ham olay katılımcıları kendi çalışmasını onaylayamaz. Ana puantaj son parça da onaylandığında tamamlanır. Parasal hakediş/ödeme üretilmez.":"İade yalnız bu parçanın kararıdır; açık turun tamamı kapatılır ve ana puantaj iade durumuna döner. Diğer amirlerin kararları değiştirilmez, tarihsel olarak korunur; kapanan turdaki süreler etkin onaylı toplamdan çıkar. Yeni gönderim bütün parçalar için yeni karar ister. Eski toplu onay/gönderimde de yeniden açma bu yolla yapılabilir."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
internal sealed class SegmentDecisionHistoryForm : SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project,segment;private readonly DataGridView history=new(){Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
    internal SegmentDecisionHistoryForm(Guid project,Guid segment,UserSession user)
    {
        this.project=project;this.segment=segment;Session=user;PersistGridLayout=false;Text="Muhasebe — Bu proje parçasının karar geçmişi";ClientSize=new(1000,620);RecordSelection.Configure(history);Controls.Add(history);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true};var refresh=new Button{Text="Yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([new Label{AutoSize=true,Text="Yalnız bu projeye ait parçanın değişmez karar olayları. Kapalı turdaki onay tarihsel kayıttır; etkin süre değildir."},refresh,close]);Controls.Add(bar);close.Click+=(_,_)=>Close();refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
        history.DataBindingComplete+=(_,_)=>{var titles=new Dictionary<string,string>{{"State","Karar"},{"LegacyReturn","Eski toplu kayıt iadesi"},{"DecidedBy","Karar veren"},{"DecidedUtc","Karar UTC"},{"DecisionReason","Gerekçe"},{"SubmittedUtc","Tur gönderim UTC"},{"IsReady","Tur hazır"},{"RoundClosedUtc","Tur kapanış UTC"}};foreach(DataGridViewColumn c in history.Columns){if(titles.TryGetValue(c.Name,out var title))c.HeaderText=title;if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[]))c.Visible=false;if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format="dd.MM.yyyy HH:mm:ss";}};
        history.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0||history.Columns[e.ColumnIndex].Name!="State")return;e.Value=e.Value?.ToString()=="Approved"?"Onay":"İade";e.FormattingApplied=true;};
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(760,440));}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>{var rows=await new TimesheetService(AppDatabase.ConnectionString,User).SegmentHistoryAsync(project,segment,ct);if(IsDisposed)return;history.DataSource=rows;history.ClearSelection();});
}
