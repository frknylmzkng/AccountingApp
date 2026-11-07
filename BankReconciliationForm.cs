using System.Data;
using System.Text;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class BankReconciliationForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private sealed record Account(Guid Id,string Caption){public override string ToString()=>Caption;}
    private readonly ComboBox accounts=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=400};
    private readonly DataGridView movements=AccrualFoundationForm.Grid(),candidates=AccrualFoundationForm.Grid(),history=AccrualFoundationForm.Grid(),imports=AccrualFoundationForm.Grid();
    private readonly ListPager movementPager,candidatePager,historyPager,importPager;
    private readonly Button create=new(){Text="Banka hesabı tanımla…",AutoSize=true},import=new(){Text="CSV seç / önizle…",AutoSize=true},match=new(){Text="Seçili hareket ve ödemeyi kontrol et…",AutoSize=true},release=new(){Text="Seçili bağlantıyı kaldır…",AutoSize=true},refresh=new(){Text="Yenile",AutoSize=true},find=new(){Text="Defter no ile aday ara",AutoSize=true};
    private readonly TextBox ledgerSearch=new(){Width=120,PlaceholderText="İsteğe bağlı no"};
    private readonly Label note=new(){AutoSize=true,Dock=DockStyle.Bottom,Padding=new(8)};
    private bool binding;private Guid? transaction;private long? ledgerFilter;
    private BankReconciliationService Service=>new(AppDatabase.ConnectionString,User);
    private Guid? AccountId=>(accounts.SelectedItem as Account)?.Id;
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView v?v.Row:null;
    internal BankReconciliationForm(UserSession user)
    {
        Session=user;PersistGridLayout=false;Text="Muhasebe — Banka CSV ve ödeme eşleştirme";ClientSize=new(1220,820);
        var tabs=new TabControl{Dock=DockStyle.Fill};var matching=new TabPage("Hareket ve ödeme adayları");var past=new TabPage("Eşleştirme / kaldırma geçmişi");var files=new TabPage("CSV aktarım geçmişi");
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new(8)};layout.RowStyles.Add(new(SizeType.Percent,55));layout.RowStyles.Add(new(SizeType.Percent,45));
        var top=new GroupBox{Text="1. Banka hareketi seçin — eksi tutar çıkış, artı tutar giriş",Dock=DockStyle.Fill,Padding=new(8)};top.Controls.Add(movements);
        var bottom=new GroupBox{Text="2. Mevcut ödeme adayı seçin — hiçbir aday otomatik onaylanmaz",Dock=DockStyle.Fill,Padding=new(8)};bottom.Controls.Add(candidates);layout.Controls.Add(top,0,0);layout.Controls.Add(bottom,0,1);matching.Controls.Add(layout);past.Controls.Add(history);files.Controls.Add(imports);tabs.TabPages.AddRange([matching,past,files]);Controls.Add(tabs);
        movementPager=ListPager.Attach(movements,this,ReloadMovementsAsync,"banka hareketi");candidatePager=ListPager.Attach(candidates,this,ReloadCandidatesAsync,"ödeme adayı");historyPager=ListPager.Attach(history,this,ReloadHistoryAsync,"bağlantı");importPager=ListPager.Attach(imports,this,ReloadImportsAsync,"dosya");
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};bar.Controls.AddRange([new Label{Text="Kaynak banka hesabı",AutoSize=true,Margin=new(3,8,3,0)},accounts,create,import,refresh,match,release,new Label{Text="Ödeme defter no",AutoSize=true,Margin=new(3,8,3,0)},ledgerSearch,find]);Controls.Add(bar);
        note.Text="Yalnız merkez yöneticisi. CSV ham hareket kaydıdır; yeni ödeme, banka transferi veya bakiye değişimi oluşturmaz. Aday: tam tutar + uygulama referansı; referans yoksa ±7 kayıt günü. Daha eski ödeme için defter no arayın. Tarih/tutar benzerliği alıcı kanıtı değildir. Eşleşen ödeme iptalinden önce bağlantı gerekçeyle kaldırılmalıdır.";Controls.Add(note);SizeChanged+=(_,_)=>note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        accounts.SelectedIndexChanged+=async(_,_)=>{if(binding||IsOperationRunning)return;Reset();await ReloadAsync();};
        movements.SelectionChanged+=async(_,_)=>{if(binding||IsOperationRunning)return;transaction=Row(movements)?.Field<Guid>("TransactionId");ClearCandidates();ledgerFilter=null;ledgerSearch.Clear();if(transaction.HasValue)await ReloadCandidatesAsync();RefreshEditState();};candidates.SelectionChanged+=(_,_)=>RefreshEditState();history.SelectionChanged+=(_,_)=>RefreshEditState();
        create.Click+=async(_,_)=>{using var d=new BankAccountDialog(User);Watch(d);d.ShowDialog(this);if(!IsDisposed)await ReloadAccountsAsync();};import.Click+=async(_,_)=>await ImportAsync();refresh.Click+=async(_,_)=>await ReloadAsync();ledgerSearch.TextChanged+=(_,_)=>RefreshEditState();find.Click+=async(_,_)=>{if(!long.TryParse(ledgerSearch.Text.Trim(),out long id)&&ledgerSearch.Text.Trim().Length>0){MessageBox.Show(this,"Geçerli pozitif defter no girin veya alanı boş bırakın.","Defter no");return;}ledgerFilter=ledgerSearch.Text.Trim().Length==0?null:id;if(ledgerFilter<=0)return;candidatePager.Reset();await ReloadCandidatesAsync();};
        match.Click+=async(_,_)=>await MatchAsync();release.Click+=async(_,_)=>{if(AccountId is not{} account||Row(history) is not{} row||!row.IsNull("ReleaseId"))return;using var d=new BankReleaseDialog(account,User,row);Watch(d);d.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};RefreshAction=ReloadAsync;Load+=async(_,_)=>await ReloadAccountsAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
        foreach(var g in new[]{movements,candidates,history,imports})g.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in g.Columns){c.HeaderText=BankTitles.GetValueOrDefault(c.Name,c.Name);if(c.ValueType==typeof(byte[]))c.Visible=false;if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2";if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name=="BookingDay"?"dd.MM.yyyy":"dd.MM.yyyy HH:mm:ss";}};
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(850,650));}
    private void Watch(SessionForm f)=>f.SessionRevoked+=(_,_)=>NotifySessionRevoked();
    private void ClearCandidates(){candidates.DataSource=null;candidatePager.Apply(new(new DataTable(),0,0,candidatePager.SizeLimit));}
    private void Reset(){transaction=null;ledgerFilter=null;ledgerSearch.Clear();foreach(var g in new[]{movements,candidates,history,imports})g.DataSource=null;movementPager.Reset();ClearCandidates();historyPager.Reset();importPager.Reset();}
    private async Task ReloadAccountsAsync()
    {
        Guid? previous=AccountId;await RunOperationAsync(async ct=>{var data=await Service.AccountsAsync(ct);if(IsDisposed)return;binding=true;try{accounts.Items.Clear();foreach(DataRow r in data.Rows)accounts.Items.Add(new Account(r.Field<Guid>("AccountId"),r["Name"]+" · "+r["Iban"]));accounts.SelectedItem=accounts.Items.Cast<Account>().FirstOrDefault(x=>x.Id==previous);if(accounts.SelectedIndex<0&&accounts.Items.Count>0)accounts.SelectedIndex=0;Reset();}finally{binding=false;}});if(!IsDisposed&&AccountId.HasValue)await ReloadAsync();
    }
    private async Task PageAsync(string section,DataGridView grid,ListPager pager)
    {
        if(AccountId is not{} account)return;Guid? selected=transaction;long? filter=ledgerFilter;grid.ClearSelection();
        await RunOperationAsync(async ct=>{var result=await Service.BrowseAsync(new(account,section,pager.Index,pager.SizeLimit,selected,filter),ct);if(IsDisposed||AccountId!=account||(section=="Candidates"&&(transaction!=selected||ledgerFilter!=filter)))return;binding=true;try{var data=RemoteApi.Decode(result.Data);grid.DataSource=data;grid.ClearSelection();pager.Apply(new(data,result.Total,result.Index,result.Size));}finally{binding=false;}});
    }
    private async Task ReloadMovementsAsync(){transaction=null;ClearCandidates();await PageAsync("Transactions",movements,movementPager);}
    private Task ReloadCandidatesAsync()=>PageAsync("Candidates",candidates,candidatePager);
    private Task ReloadHistoryAsync()=>PageAsync("History",history,historyPager);
    private Task ReloadImportsAsync()=>PageAsync("Imports",imports,importPager);
    private async Task ReloadAsync(){await ReloadMovementsAsync();if(IsDisposed)return;await ReloadHistoryAsync();if(!IsDisposed)await ReloadImportsAsync();}
    private async Task ImportAsync()
    {
        if(AccountId is not{} account)return;using var pick=new OpenFileDialog{Filter="CSV (*.csv)|*.csv|Tüm dosyalar (*.*)|*.*",Title="Tek banka hesabına ait UTF-8 hesap hareketleri"};if(pick.ShowDialog(this)!=DialogResult.OK)return;string? csv=null;
        if(!await RunOperationAsync(async ct=>{if(new FileInfo(pick.FileName).Length>BankCsv.MaxBytes+3)throw new ArgumentException("Dosya 24.000 bayt sınırını aşıyor; 200 satırı aşmayan parçalara bölün.");var bytes=await File.ReadAllBytesAsync(pick.FileName,ct);if(bytes.Length>BankCsv.MaxBytes+3)throw new ArgumentException("Dosya sınırı aşıldı.");try{csv=new UTF8Encoding(false,true).GetString(bytes);}catch(DecoderFallbackException){throw new ArgumentException("Dosya geçerli UTF-8 değil. Banka dosyasının kopyasını UTF-8 CSV olarak kaydedip tekrar seçin.");}})||IsDisposed||csv==null||AccountId!=account)return;
        using var d=new BankImportDialog(account,accounts.Text,User,Path.GetFileName(pick.FileName),csv);Watch(d);d.ShowDialog(this);if(!IsDisposed)await ReloadAsync();
    }
    private bool SearchMatches=>ledgerSearch.Text.Trim().Length==0?ledgerFilter==null:long.TryParse(ledgerSearch.Text.Trim(),out long id)&&id==ledgerFilter;
    private async Task MatchAsync()
    {
        if(!SearchMatches||AccountId is not{} account||Row(movements) is not{} bank||Row(candidates) is not{} payment)return;Guid id=bank.Field<Guid>("TransactionId");long ledger=payment.Field<long>("LedgerId");Wire.BankMatchQuote? quote=null;
        if(!await RunOperationAsync(async ct=>quote=await Service.QuoteAsync(new(account,id,ledger),ct))||IsDisposed||quote==null)return;using var d=new BankMatchDialog(account,id,ledger,User,quote);Watch(d);d.ShowDialog(this);if(!IsDisposed)await ReloadAsync();
    }
    protected override void RefreshEditState()
    {
        bool ready=!IsOperationRunning&&Session?.Role==UserRole.Admin;accounts.Enabled=create.Enabled=refresh.Enabled=ready;import.Enabled=ready&&AccountId.HasValue;find.Enabled=ready&&transaction.HasValue;ledgerSearch.Enabled=ready;match.Enabled=ready&&SearchMatches&&Row(movements) is{} b&&b.Field<decimal>("SignedAmount")<0&&b.IsNull("MatchId")&&Row(candidates)!=null;release.Enabled=ready&&Row(history) is{} h&&h.IsNull("ReleaseId");movementPager?.UpdateState();candidatePager?.UpdateState();historyPager?.UpdateState();importPager?.UpdateState();
    }
    internal static readonly Dictionary<string,string> BankTitles=new(){["SourceLine"]="CSV satırı",["ExternalId"]="Banka işlem no",["BookingDay"]="Banka günü",["SignedAmount"]="Hareket TL (eksi çıkış)",["CounterpartyIban"]="Dekont alıcı/gönderen IBAN",["Description"]="Banka açıklaması",["PaymentReference"]="CSV uygulama ödeme referansı",["ImportStatus"]="Aktarım sonucu",["TransactionId"]="Ham hareket kimliği",["MatchId"]="Bağlantı kimliği",["LedgerId"]="Ödeme defter no",["RequestId"]="Uygulama ödeme referansı",["PersonId"]="Kalıcı personel kimliği",["PersonTc"]="Ödemedeki TC",["PersonnelGroup"]="Ödemedeki grup",["ProjectId"]="Ödemedeki proje kimliği",["PaymentUtc"]="Ödeme kayıt UTC",["PaymentAmount"]="Ödeme TL",["CandidateBasis"]="Adaylık gerekçesi",["BatchId"]="Aktarım kimliği",["FileName"]="Dosya adı",["AddedCount"]="Yeni hareket",["DuplicateCount"]="Atlanan tekrar",["CreatedBy"]="Kaydeden",["CreatedUtc"]="Kayıt UTC",["Reason"]="Kayıt gerekçesi",["ReleaseId"]="Kaldırma işlem kimliği",["ReleasedUtc"]="Kaldırma UTC",["ReleasedBy"]="Kaldıran",["ReleaseReason"]="Kaldırma gerekçesi",["Currency"]="Para birimi",["MatchStatus"]="Eşleştirme durumu"};
}

internal sealed class BankAccountDialog:CalendarEditDialog
{
    private readonly Guid id=Guid.NewGuid();private readonly TextBox name=new(){Width=400,MaxLength=100},iban=new(){Width=400,MaxLength=34};
    internal BankAccountDialog(UserSession user):base(user,"Banka — Kaynak hesap tanımla","Banka hesabı")
    {Field("Hesap adı",name);Field("Kaynak hesabın IBAN'ı (TRY)",iban);Fields.Controls.Add(new Label{AutoSize=true,Text="Aynı IBAN ikinci kez tanımlanamaz. CSV'deki kaynak IBAN bu hesapla eşleşmelidir. Hesap kimliği ve geçmişi değişmez; bu tanım bankaya bağlanmaz."});AcceptTrackedChanges();}
    protected override object MakeRequest(string reason)=>new Wire.BankAccountCreate(id,name.Text,iban.Text,reason);
}
internal sealed class BankImportDialog:CalendarEditDialog
{
    private readonly Guid account,batch=Guid.NewGuid();private readonly string csv,fileName;
    private readonly ComboBox separator=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=140},date=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=140},decimalMark=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=140};
    private readonly ComboBox[] columns=Enumerable.Range(0,8).Select(_=>new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=220}).ToArray();
    private readonly DataGridView rows=AccrualFoundationForm.Grid();private readonly Label status=new(){AutoSize=true};private Wire.BankImportPreview? preview;private Wire.BankCsvSource? source;private bool binding;
    protected override bool CanSubmit=>preview is{AlreadyImported:false}&&source!=null;
    internal BankImportDialog(Guid account,string caption,UserSession user,string fileName,string csv):base(user,"Banka — CSV önizlemesi ve sütun eşleme","Banka CSV aktarımı")
    {
        this.account=account;this.csv=csv;this.fileName=fileName;ClientSize=new(1020,850);Fields.Controls.Add(new Label{AutoSize=true,Text=caption+"\nDosya: "+fileName+"\n200 satır / 24.000 UTF-8 bayt. Çıkışlar eksi, girişler artı; binlik ayırıcı yok. Yalnız TRY. Ödeme GUID sütunu isteğe bağlıdır; bankanın açıklama/referans sütununu GUID alanına eşlemeyin."});
        separator.Items.AddRange([";",",","Sekme"]);date.Items.AddRange(["yyyy-MM-dd","dd.MM.yyyy"]);decimalMark.Items.AddRange([".",","]);separator.SelectedIndex=date.SelectedIndex=decimalMark.SelectedIndex=0;
        var formats=new FlowLayoutPanel{AutoSize=true,WrapContents=true};formats.Controls.AddRange([new Label{Text="Ayırıcı",AutoSize=true},separator,new Label{Text="Tarih biçimi",AutoSize=true},date,new Label{Text="Ondalık",AutoSize=true},decimalMark]);Fields.Controls.Add(formats);
        var mapping=new FlowLayoutPanel{AutoSize=true,WrapContents=true};Fields.Controls.Add(mapping);
        string[] labels=["Kaynak hesap IBAN *","Banka işlem kimliği *","Banka günü *","İşaretli tutar *","Para birimi *","Karşı taraf IBAN","Açıklama","Uygulama ödeme GUID"];
        for(int i=0;i<columns.Length;i++){var box=new FlowLayoutPanel{FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoSize=true,Margin=new(8)};box.Controls.Add(new Label{Text=labels[i],AutoSize=true});box.Controls.Add(columns[i]);mapping.Controls.Add(box);columns[i].SelectedIndexChanged+=(_,_)=>InvalidatePreview();TrackChanges("Banka CSV aktarımı",columns[i]);}
        var inspect=new Button{Text="Sunucuda önizlemeyi oluştur / tekrarları kontrol et",AutoSize=true};Fields.Controls.Add(inspect);Fields.Controls.Add(status);rows.Dock=DockStyle.None;rows.Height=280;Fields.Controls.Add(rows);
        Fields.SizeChanged+=(_,_)=>{int width=Math.Max(1,Fields.ClientSize.Width-48);formats.MaximumSize=mapping.MaximumSize=new(width,0);formats.Width=mapping.Width=rows.Width=width;};
        foreach(var c in new[]{separator,date,decimalMark})TrackChanges("Banka CSV aktarımı",c);separator.SelectedIndexChanged+=(_,_)=>{InvalidatePreview();MapHeaders();};date.SelectedIndexChanged+=(_,_)=>InvalidatePreview();decimalMark.SelectedIndexChanged+=(_,_)=>InvalidatePreview();
        inspect.Click+=async(_,_)=>await RunOperationAsync(async ct=>{preview=null;source=null;rows.DataSource=null;var selected=Source();var result=await new BankReconciliationService(AppDatabase.ConnectionString,User).PreviewAsync(selected,ct);if(IsDisposed)return;source=selected;preview=result;rows.DataSource=RemoteApi.Decode(result.Rows);rows.ClearSelection();foreach(DataGridViewColumn c in rows.Columns)c.HeaderText=BankReconciliationForm.BankTitles.GetValueOrDefault(c.Name,c.Name);status.Text=$"Yeni: {result.NewRows} · Atlanacak tekrar: {result.DuplicateRows} · "+(result.AlreadyImported?"Bu dosya/eşleme daha önce aktarıldı; tekrar kaydedilemez.":"Satırları kontrol edin. Kaydet, yalnız ham hareketleri saklar.");});MapHeaders();AcceptTrackedChanges();
    }
    private string Separator=>separator.SelectedIndex==2?"\t":separator.Text;
    private void InvalidatePreview(){if(binding)return;preview=null;source=null;rows.DataSource=null;status.Text="Biçim veya sütun seçimi değişti; yeni önizleme gerekiyor.";RefreshEditState();}
    private void MapHeaders()
    {
        binding=true;try{var header=BankCsv.Cells(csv,Separator)[0];string[] expected=["AccountIban","TransactionId","Date","Amount","Currency","CounterpartyIban","Description","PaymentReference"];
            for(int i=0;i<columns.Length;i++){columns[i].Items.Clear();columns[i].Items.Add("(Seçilmedi)");foreach(var h in header)columns[i].Items.Add(h);int found=Array.FindIndex(header,h=>string.Equals(h,expected[i],StringComparison.OrdinalIgnoreCase));columns[i].SelectedIndex=found+1;}status.Text="Zorunlu beş alanı ayrı sütunlara eşleyin ve önizleyin.";
        }catch(ArgumentException ex){foreach(var c in columns){c.Items.Clear();c.Items.Add("(Seçilmedi)");c.SelectedIndex=0;}status.Text=ex.Message;}finally{binding=false;}
    }
    private Wire.BankCsvSource Source()=>new(account,fileName,csv,Separator,date.Text,decimalMark.Text,columns.Select(c=>c.SelectedIndex-1).ToArray());
    protected override object MakeRequest(string reason)=>CanSubmit?new Wire.BankImport(batch,source!,preview!.Fingerprint,reason):throw new InvalidOperationException("Geçerli CSV önizlemesi gerekiyor.");
}
internal sealed class BankMatchDialog:CalendarEditDialog
{
    private readonly Wire.BankMatchCreate request;private readonly CheckBox confirmed=new(){Text="Dekontu, ödeme referansını ve alıcıyı kontrol ettim; aynı işlem olduğunu onaylıyorum.",AutoSize=false,Height=48,Width=550};
    protected override bool CanSubmit=>confirmed.Checked;
    internal BankMatchDialog(Guid account,Guid transaction,long ledger,UserSession user,Wire.BankMatchQuote quote):base(user,"Banka — Mevcut ödemeyle eşleştir","Banka ödeme eşleştirme")
    {
        request=new(account,Guid.NewGuid(),transaction,ledger,quote.Fingerprint,"");var r=RemoteApi.Decode(quote.Details).Rows[0];Fields.Controls.Add(new Label{AutoSize=true,Text=$"Banka işlem no: {r["ExternalId"]}\nBanka günü: {r.Field<DateTime>("BookingDay"):dd.MM.yyyy} · Hareket: {r.Field<decimal>("SignedAmount"):N2} TL\nDekont IBAN: {r["CounterpartyIban"]}\nBanka açıklaması: {r["Description"]}\nCSV ödeme GUID: {r["PaymentReference"]}\nÖdeme defter no: {ledger} · Referans: {r["RequestId"]}\nÖdeme kişisi TC: {r["PersonTc"]} · Kalıcı kimlik: {r["PersonId"]}\nProje: {r["ProjectId"]}\nÖdeme: {r.Field<decimal>("PaymentAmount"):N2} TL · Kayıt UTC: {r.Field<DateTime>("PaymentUtc"):dd.MM.yyyy HH:mm:ss}\n\n"+string.Join("\n\n",quote.Warnings)});Field("Açık kullanıcı onayı",confirmed);confirmed.CheckedChanged+=(_,_)=>RefreshEditState();Fields.SizeChanged+=(_,_)=>confirmed.Width=Math.Max(1,Fields.ClientSize.Width-48);AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>confirmed.Checked?request with{Reason=reason}:throw new ArgumentException("Dekont kontrolünü açıkça onaylayın.");
}
internal sealed class BankReleaseDialog:CalendarEditDialog
{
    private readonly Wire.BankMatchRelease request;
    internal BankReleaseDialog(Guid account,UserSession user,DataRow row):base(user,"Banka — Ödeme bağlantısını kaldır","Banka eşleştirmesini kaldırma")
    {request=new(account,row.Field<Guid>("MatchId"),Guid.NewGuid(),((byte[])row["RecordVersion"]).ToArray(),"");Fields.Controls.Add(new Label{AutoSize=true,Text=$"Banka işlem no: {row["ExternalId"]}\nÖdeme defter no: {row["LedgerId"]}\nTutar: {row.Field<decimal>("SignedAmount"):N2} TL\nİlk bağlantı gerekçesi: {row["Reason"]}\n\nYalnız bağlantı kaldırılır; banka hareketi ve eski eşleştirme geçmişi korunur. Ödeme iptal edilmez, para iadesi yapılmaz. Yeniden eşleştirme ayrı onay gerektirir."});AcceptTrackedChanges();}
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
