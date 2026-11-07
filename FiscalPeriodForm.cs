using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class FiscalPeriodForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool canExport;
    private readonly DataGridView history=AccrualFoundationForm.Grid();private readonly ListPager pager;
    private readonly DateTimePicker from=new(){Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy",Width=130,MinDate=new(1900,1,1),MaxDate=new(2199,12,31)},to=new(){Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy",Width=130,MinDate=new(1900,1,1),MaxDate=new(2199,12,31)};
    private readonly Button preview=new(){Text="Dönemi önizle / kesinleştir…",AutoSize=true},open=new(){Text="Seçili kesin kapanışı aç…",AutoSize=true};
    private readonly Label info=new(){AutoSize=true,Dock=DockStyle.Top,Padding=new(8)},note=new(){AutoSize=true,Dock=DockStyle.Bottom,Padding=new(8)};private bool hasPrevious;
    private FiscalPeriodService Service=>new(AppDatabase.ConnectionString,User);
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView v?v.Row:null;
    internal FiscalPeriodForm(Guid project,UserSession user,bool canExport)
    {
        this.project=project;this.canExport=canExport;Session=user;PersistGridLayout=false;Text="Muhasebe — Kalıcı mali dönem kapanışı";ClientSize=new(1160,760);Controls.Add(history);pager=ListPager.Attach(history,this,async()=>await ReloadAsync(false),"kesin kapanış");Controls.Add(info);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Güncel kapanış zincirini yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([new Label{Text="İstanbul kayıt dönemi: başlangıç",AutoSize=true},from,new Label{Text="Bitiş (dahil)",AutoSize=true},to,preview,open,refresh,close]);Controls.Add(bar);
        note.Text="Kapanış proje bazlı mali kayıt olaylarını dondurur; günlük nakit kapanışından ayrıdır. İlk dönemde eski olaylar açılışa alınır. Sonraki dönem boşluksuz ilerler; eski kayda bağlı iptal/düzeltmeler yeni dönemde bağlantılı görünür. Kesin kapanış değişmez ve yeniden açılamaz. Yeni ödeme veya bakiye hareketi oluşmaz. Projesiz eski hareketler ve personelin tüm bakiyesi dahil değildir.";Controls.Add(note);SizeChanged+=(_,_)=>info.MaximumSize=note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        FiscalPresentation.Format(history);history.SelectionChanged+=(_,_)=>RefreshEditState();preview.Click+=async(_,_)=>await PreviewAsync();open.Click+=(_,_)=>{if(Row(history) is not{} row)return;using var d=new FiscalSnapshotForm(project,row.Field<Guid>("ClosingId"),User,canExport);Watch(d);d.ShowDialog(this);};refresh.Click+=async(_,_)=>await ReloadAsync(true);close.Click+=(_,_)=>Close();RefreshAction=async()=>await ReloadAsync(true);Load+=async(_,_)=>await ReloadAsync(true);SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(800,560));}
    private void Watch(SessionForm f)=>f.SessionRevoked+=(_,_)=>NotifySessionRevoked();
    protected override void RefreshEditState(){bool admin=!IsOperationRunning&&Session?.Role==UserRole.Admin;preview.Enabled=admin;from.Enabled=admin&&!hasPrevious;to.Enabled=admin;open.Enabled=!IsOperationRunning&&Row(history)!=null;pager?.UpdateState();}
    private async Task ReloadAsync(bool dates)=>await RunOperationAsync(async ct=>
    {
        var result=await Service.HistoryAsync(new(project,pager.Index,pager.SizeLimit),ct);if(IsDisposed)return;var context=RemoteApi.Decode(result.Context).Rows[0];hasPrevious=!context.IsNull("LatestClosingId");var rows=RemoteApi.Decode(result.Closings.Data);history.DataSource=rows;history.ClearSelection();pager.Apply(new(rows,result.Closings.Total,result.Closings.Index,result.Closings.Size));
        if(dates){DateTime today=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,DailyClosingService.BusinessZone).Date;DateTime first=new(today.Year,today.Month,1);var start=hasPrevious?context.Field<DateTime>("LatestToDay").AddDays(1):first.AddMonths(-1);if(start>from.MaxDate){preview.Enabled=false;info.Text="Takvim üst sınırına ulaşıldı.";return;}from.Value=start;DateTime end=new(start.Year,start.Month,DateTime.DaysInMonth(start.Year,start.Month));to.Value=end<today?end:(today.AddDays(-1)>=start?today.AddDays(-1):start);}
        info.Text=$"{context["Code"]} · {context["Name"]}\n"+(hasPrevious?$"Son kapanış: {context["LatestClosingId"]} · Yeni başlangıç: {context.Field<DateTime>("LatestToDay").AddDays(1):dd.MM.yyyy}":"İlk dönem: başlangıçtan eski mali olaylar ayrı açılış satırları olarak korunur.");
    });
    private async Task PreviewAsync()
    {
        var request=new Wire.FiscalPeriodScope(project,DateTime.SpecifyKind(from.Value.Date,DateTimeKind.Unspecified),DateTime.SpecifyKind(to.Value.Date,DateTimeKind.Unspecified));Wire.FiscalPeriodQuote? result=null;if(!await RunOperationAsync(async ct=>result=await Service.QuoteAsync(request,ct))||IsDisposed||result==null)return;using var d=new FiscalCloseDialog(User,request,result);Watch(d);d.ShowDialog(this);if(!IsDisposed&&d.Saved)await ReloadAsync(true);
    }
}
internal sealed class FiscalCloseDialog:CalendarEditDialog
{
    private readonly Wire.FiscalPeriodFinalize request;
    private readonly CheckBox confirm=new(){AutoSize=false,Height=50,Width=700,Text="Dönem, açılış, toplamlar ve geç yansımaları inceledim; değişmez olarak kesinleştirmeyi onaylıyorum."};
    protected override bool CanSubmit=>confirm.Checked;
    internal FiscalCloseDialog(UserSession user,Wire.FiscalPeriodScope scope,Wire.FiscalPeriodQuote quote):base(user,"Mali dönem — Değişmez kapanışı kesinleştir","Mali dönem kesinleştirme")
    {
        ClientSize=new(1060,850);request=new(scope.ProjectId,Guid.NewGuid(),quote.PreviousClosingId,scope.From,scope.To,quote.Fingerprint,"");Fields.Controls.Add(new Label{AutoSize=true,Text=$"Dönem: {scope.From:dd.MM.yyyy} – {scope.To:dd.MM.yyyy} (İstanbul, bitiş dahil)\nÖnceki kapanış: {quote.PreviousClosingId?.ToString()??"İlk kayıt"}\nToplam yeni kaynak olayı: {quote.TotalLines:N0}\n\n"+string.Join("\n\n",quote.Notices)});
        var summary=AccrualFoundationForm.Grid();summary.Dock=DockStyle.None;summary.Height=155;summary.DataSource=RemoteApi.Decode(quote.Summary);var lines=AccrualFoundationForm.Grid();lines.Dock=DockStyle.None;lines.Height=270;lines.DataSource=RemoteApi.Decode(quote.Lines);FiscalPresentation.Format(summary);FiscalPresentation.Format(lines);Fields.Controls.Add(summary);Fields.Controls.Add(lines);Field("Kesinleştirme onayı",confirm);confirm.CheckedChanged+=(_,_)=>RefreshEditState();Fields.SizeChanged+=(_,_)=>{int width=Math.Max(1,Fields.ClientSize.Width-48);summary.Width=lines.Width=confirm.Width=width;};AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>confirm.Checked?request with{Reason=reason}:throw new ArgumentException("Değişmez kapanış için açık onay gerekiyor.");
}
internal sealed class FiscalSnapshotForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project,closing;private readonly bool canExport;private readonly DataGridView summary=AccrualFoundationForm.Grid(),lines=AccrualFoundationForm.Grid();private readonly ListPager pager;private readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    private readonly Label info=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)};private readonly Button export=new(){AutoSize=true,Text="Seçili kesin bölümün tamamını CSV kaydet…"},related=new(){AutoSize=true,Text="Satırın bağlı olduğu eski kapanışı aç…"};private bool loaded;
    internal FiscalSnapshotForm(Guid project,Guid closing,UserSession user,bool canExport,int? relatedLine=null)
    {
        this.project=project;this.closing=closing;this.canExport=canExport;Session=user;PersistGridLayout=false;Text="Muhasebe — Kesin mali dönem kaydı";ClientSize=new(1200,800);foreach(var item in new[]{("Summary","Kesin açılış / dönem / kapanış",summary),("Lines","Kaynak olayları ve önceki kapanış bağlantıları",lines)}){var tab=new TabPage(item.Item2){Tag=item.Item1};tab.Controls.Add(item.Item3);tabs.TabPages.Add(tab);}Controls.Add(tabs);pager=ListPager.Attach(lines,this,ReloadAsync,"kesin kaynak olayı");Controls.Add(info);var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([export,related,close]);Controls.Add(bar);FiscalPresentation.Format(summary);FiscalPresentation.Format(lines);SizeChanged+=(_,_)=>info.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        close.Click+=(_,_)=>Close();export.Click+=async(_,_)=>await ExportAsync();lines.SelectionChanged+=(_,_)=>RefreshEditState();related.Click+=(_,_)=>{if(lines.SelectedRows.Count!=1||lines.SelectedRows[0].DataBoundItem is not DataRowView v||v.Row.IsNull("RelatedClosingId"))return;using var d=new FiscalSnapshotForm(project,v.Row.Field<Guid>("RelatedClosingId"),User,canExport,v.Row.Field<int?>("RelatedLineNo"));d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);};Load+=async(_,_)=>{if(relatedLine.HasValue){pager.Apply(new(new DataTable(),relatedLine.Value,(relatedLine.Value-1)/50,50));tabs.SelectedIndex=1;}await ReloadAsync();};RefreshAction=ReloadAsync;SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,580));}
    protected override void RefreshEditState(){export.Enabled=!IsOperationRunning&&canExport&&loaded;related.Enabled=!IsOperationRunning&&lines.SelectedRows.Count==1&&lines.SelectedRows[0].DataBoundItem is DataRowView v&&!v.Row.IsNull("RelatedClosingId");pager?.UpdateState();}
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>{var result=await new FiscalPeriodService(AppDatabase.ConnectionString,User).ReadAsync(new(project,closing,pager.Index,pager.SizeLimit),ct);if(IsDisposed)return;var h=RemoteApi.Decode(result.Header).Rows[0];info.Text=$"{h["ProjectCaption"]}\nKesin dönem: {h["FromDay"]} – {h["ToDay"]} · Kapanış: {h["ClosingId"]}\nKesinleştiren: {h["ClosedBy"]} · UTC: {h["ClosedUtc"]}\nGerekçe: {h["Reason"]}\nSHA-256: {h["SnapshotHash"]}\nBu içerik kapanışta saklanmıştır; canlı veriden yeniden hesaplanmaz.";summary.DataSource=RemoteApi.Decode(result.Summary);summary.ClearSelection();var data=RemoteApi.Decode(result.Lines.Data);lines.DataSource=data;lines.ClearSelection();pager.Apply(new(data,result.Lines.Total,result.Lines.Index,result.Lines.Size));loaded=true;});
    private async Task ExportAsync(){if(tabs.SelectedTab?.Tag is not string section)return;using var d=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName=$"kesin-mali-donem-{closing:D}-{section}.csv",DefaultExt="csv",AddExtension=true};if(d.ShowDialog(this)!=DialogResult.OK)return;await RunChangeOperationAsync(ct=>new ProjectReportService(AppDatabase.ConnectionString,User).SaveCsvAsync(project,$"Fiscal.{closing:D}.{section}",d.FileName,ct),"Kesin kapanış bölümü CSV olarak kaydedildi.","Kesin mali dönem kaydını dışa aktarma");}
}
internal static class FiscalPresentation
{
    internal static readonly Dictionary<string,string> Titles=new(){["Scope"]="Kapsam",["Labor"]="İşçilik / hakediş TL",["OtherCost"]="Diğer gider TL",["CashOut"]="Ödeme çıkışı TL",["CashReturn"]="Ödeme iptal tutarı TL",["AdvanceOut"]="Avans çıkışı TL",["AdvanceReturn"]="Avans iptali TL",["Allocated"]="Net tahsis TL",["BankMatched"]="Net banka bağlantısı TL",["TotalCost"]="Toplam maliyet TL",["NetCash"]="Net ödeme TL",["NetAdvance"]="Net avans TL",["ProjectPosition"]="Projeye bağlı hakediş − net ödeme TL",["ClosingId"]="Kesin kapanış kimliği",["PreviousClosingId"]="Önceki kapanış",["FromDay"]="Başlangıç",["ToDay"]="Bitiş",["ClosedUtc"]="Kesinleştirme UTC",["ClosedBy"]="Kesinleştiren",["Reason"]="Gerekçe",["LineCount"]="Kaynak olay sayısı",["LineNo"]="Satır",["SourceKind"]="Kaynak olay türü",["SourceKey"]="Kaynak olay kimliği",["OccurredUtc"]="Kaynak kayıt UTC",["PersonId"]="Kalıcı personel kimliği",["Reference"]="Kaynak referansı",["OriginKind"]="Asıl olay türü",["OriginKey"]="Asıl olay kimliği",["ServiceDay"]="Belge hizmet bitişi",["Bucket"]="Döneme alınış",["LinkKind"]="Önceki kapanış ilişkisi",["RelatedClosingId"]="İlgili eski kapanış",["RelatedLineNo"]="Eski kapanış satırı"};
    internal static string Value(string value)=>value switch{"Opening"=>"İlk açılış","Period"=>"Bu dönem kaydı","Carry"=>"Önceki dönemden geç yansıma","ClosedSource"=>"Kapalı kaynağın sonraki işlemi","LateBooking"=>"Geç görünen eski kayıt","PriorService"=>"Önceki hizmet döneminin geç mali kaydı","FinancePosted"=>"Hakediş / gider kaydı","FinanceReversed"=>"Belge iptali","AccrualCorrection"=>"Onaylı mali düzeltme aktarımı","Payment"=>"Ödeme","PaymentReversal"=>"Ödeme iptali","Allocation"=>"Tahsis","AllocationRelease"=>"Tahsis kaldırma","BankMatch"=>"Banka bağlantısı","BankMatchRelease"=>"Banka bağlantısı kaldırma",_=>value};
    internal static void Format(DataGridView grid)
    {
        void Columns(){foreach(DataGridViewColumn c in grid.Columns){c.HeaderText=Titles.GetValueOrDefault(c.Name,c.Name);if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2";if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name.EndsWith("Utc",StringComparison.Ordinal)?"dd.MM.yyyy HH:mm:ss":"dd.MM.yyyy";}}
        grid.DataBindingComplete+=(_,_)=>Columns();Columns();grid.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&grid.Columns[e.ColumnIndex].Name is "SourceKind" or "OriginKind" or "Bucket" or "LinkKind"&&e.Value is string s){e.Value=Value(s);e.FormattingApplied=true;}};
    }
}
