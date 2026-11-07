using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AccrualApprovalDialog:CalendarEditDialog
{
    private readonly Wire.AccrualApprove request;
    internal AccrualApprovalDialog(Guid project,UserSession user,DataRow row):base(user,"Muhasebe — Bağımsız mali onay","Dönem hesabı mali onayı")
    {
        request=new(project,row.Field<Guid>("CalculationId"),((byte[])row["RecordVersion",DataRowVersion.Original]).ToArray(),Guid.NewGuid(),"");
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"Dönem: {row.Field<DateTime>("PeriodStart"):dd.MM.yyyy} – {row.Field<DateTime>("PeriodEnd"):dd.MM.yyyy}\nHazırlayan: {row["CreatedBy"]} · {row["LineCount"]} satır · {row.Field<decimal>("TotalAmount"):N2} TL."});
        Fields.Controls.Add(new Label{AutoSize=true,Text="Kaynak satırlarını, ücret oranlarını ve toplamı hesap geçmişinden inceleyin. Kaydet ve gerekçe, bu mühürlü hesaba mali onay verir. Hazırlayan veya hak sahibiyle aynı kişi, başka hesabını kullansa da onaylayamaz.\nMali onay değişmez kayıttır; bu adımda bakiye ve defter değişmez. Aktarım ayrıca yapılır. Onay geri alınmaz; aktarım sonrası kişi satırına bağlı ayrı, bağımsız onaylı mali düzeltme hazırlanabilir."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}

internal sealed class AccrualPostingDialog:CalendarEditDialog
{
    private readonly Wire.AccrualPost request;
    private readonly bool allowed;
    protected override bool CanSubmit=>allowed;
    internal AccrualPostingDialog(Guid project,Guid calculation,UserSession user,Wire.AccrualPostingQuote quote):base(user,"Muhasebe — Onaylı hakedişi deftere ve bakiyeye aktar","Onaylı dönem hesabı aktarımı")
    {
        request=new(project,calculation,Guid.NewGuid(),quote.Fingerprint,"");allowed=quote.CanPost;
        Fields.Controls.Add(new Label{AutoSize=true,Text=allowed?"Onaylı hesap kişi/çalışma dönemine göre toplanır. Aşağıdaki önceki ve sonraki bakiyeleri kontrol edin. Kaydet ve gerekçe ile bütün kişilere tek transaction içinde aktarılır.":string.Join("\n",quote.Issues)});
        var grid=AccrualFoundationForm.Grid();grid.Dock=DockStyle.None;grid.Width=850;grid.Height=300;grid.DataSource=RemoteApi.Decode(quote.Targets);Fields.Controls.Add(grid);Fields.SizeChanged+=(_,_)=>{grid.Width=Math.Max(1,Fields.ClientSize.Width-48);grid.Height=Math.Max(220,Fields.ClientSize.Height-230);};ClientSize=new(1050,720);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Pozitif kişi toplamı için bir hakediş belgesi ve bir Allocation defter satırı oluşturulur; bakiye aynı tutarda artar. Sıfır toplam açık aktarım satırıyla izlenir; sıfır belge/defter veya bakiye hareketi üretilmez. Bir hedef reddedilirse tamamı geri alınır. Banka transferi veya ödeme yapılmaz.\nSonuç belirsizse aynı açık pencerede Aynı isteği yeniden gönder'i kullanın; uygulama kapanırsa hesap/aktarım geçmişini kontrol edin. Aynı hesap ikinci kez aktarılamaz. Bu belgeler tek başına iptal edilemez; seçili hesabın mali düzeltmeleri ekranından bağımsız onaylı fark/ters hareket hazırlanabilir."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>allowed?request with{Reason=reason}:throw new InvalidOperationException("Aktarım engellerini giderip yeni önizleme oluşturun.");
}
