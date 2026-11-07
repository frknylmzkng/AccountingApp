using System.Data;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AccrualFoundationForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;
    private readonly DateTimePicker from=new(){Format=DateTimePickerFormat.Short,Width=130},to=new(){Format=DateTimePickerFormat.Short,Width=130};
    private readonly DataGridView rates=Grid(),preview=Grid(),history=Grid(),lines=Grid();
    private readonly Button add=Button("Yeni saatlik tarife…"),revoke=Button("Seçili tarifeyi kaldır…"),calculate=Button("Hesabı değişmez kaydet…");
    private readonly Label info=new(){AutoSize=true,Padding=new(8)};
    private readonly Button approve=Button("Seçili hesaba mali onay…"),postToLedger=Button("Aktarım önizle / deftere işle…"),postingItems=Button("Seçili aktarım satırları");
    private readonly Button corrections=Button("Seçili hesabın mali düzeltmeleri…");
    private readonly Button supplemental=Button("Seçili asıl hesap için ek önizleme");
    private Guid? preparedParent;
    private Wire.AccrualPreview? prepared;private DateTime preparedFrom,preparedTo;
    private AccrualFoundationService Service=>new(AppDatabase.ConnectionString,User);
    private static Button Button(string text)=>new(){Text=text,AutoSize=true};
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView r?r.Row:null;
    private static DateTime Date(DateTimePicker p)=>DateTime.SpecifyKind(p.Value.Date,DateTimeKind.Unspecified);
    internal static DataGridView Grid()
    {
        var g=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};RecordSelection.Configure(g);
        g.DataBindingComplete+=(_,_)=>{var names=new Dictionary<string,string>{{"AllocatedAmount","Aktif tahsis toplamı TL"},{"PaymentLedgerId","Ödeme defter no"},{"PaymentReference","Ödeme işlem no"},{"PaymentPurpose","Ödeme türü"},{"PaymentAmount","Ödeme TL"},{"AccrualAmount","Düzeltilmiş hakediş TL"},{"PaymentAvailable","Tahsis edilmemiş ödeme TL"},{"AccrualAvailable","Tahsis edilmemiş hakediş TL"},{"ReleaseReason","Serbest bırakma gerekçesi"},{"ReleasedBy","Serbest bırakan"},{"ReleasedUtc","Serbest bırakma UTC"},{"OriginalAmount","İlk hakediş TL"},{"OriginalLedgerId","İlk aktarım defter no"},{"EffectivePostedTotal","Düzeltme sonrası aktarım TL"},{"EffectiveAmount","Düzeltilmiş toplam TL"},{"AdjustmentAmount","Düzeltme toplamı TL"},{"OldAmount","Önceki hakediş TL"},{"NewAmount","Yeni hakediş TL"},{"Delta","Mali fark TL"},{"Reason","Düzeltme gerekçesi"},{"IsCurrent","Güncel tutara dayanıyor"},{"CalculationType","Hesap türü"},{"BaseCalculation","Asıl hesap no"},{"WorkflowState","İşlem durumu"},{"CanApprove","Bağımsız onay verilebilir"},{"ApprovedBy","Mali onaylayan"},{"ApprovedUtc","Mali onay UTC"},{"PostedBy","Aktaran"},{"PostedUtc","Aktarım UTC"},{"Balance","Güncel bakiye TL"},{"BeforeBalance","Önceki bakiye TL"},{"AfterBalance","Sonraki bakiye TL"},{"LedgerId","Defter no"},{"FinanceReference","Belge no"},{"PersonnelGroup","Personel grubu"},{"WorkStart","Kaynak ilk iş günü"},{"WorkEnd","Kaynak son iş günü"},{"PersonCaption","Kayıttaki kişi adı"},{"AD","Ad"},{"SOYAD","Soyad"},{"ValidFrom","Tarife başlangıcı"},{"ValidTo","Tarife sonu (dahil)"},{"HourlyAmount","Saatlik TL"},{"Currency","Para birimi"},{"RevokedUtc","Kaldırma UTC"},{"RevokedBy","Kaldıran"},{"CreatedBy","Hazırlayan"},{"CreatedUtc","Kayıt UTC"},{"EmploymentStart","Dönem işe giriş"},{"EmploymentEnd","Dönem ayrılış"},{"SourceKind","Kaynak türü"},{"WorkDate","İş günü"},{"PaidMinutes","Ücret dk"},{"RateCount","Uygun tarife sayısı"},{"Amount","Hesap TL"},{"BaseWageReserved","Günlük/aylık temel kapsamında"},{"AlreadyCalculated","Daha önce hesaplandı"},{"Issue","Engel"},{"PeriodStart","Dönem başlangıcı"},{"PeriodEnd","Dönem bitişi"},{"TotalAmount","Toplam TL"},{"LineCount","Satır"},{"IsSealed","Değişmez kayıt"},{"Policy","Hesap politikası"}};foreach(DataGridViewColumn c in g.Columns){if(names.TryGetValue(c.Name,out var title))c.HeaderText=title;if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[]))c.Visible=false;if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format="dd.MM.yyyy HH:mm";if(c.ValueType==typeof(decimal))c.DefaultCellStyle.Format="N2";}};
        g.CellFormatting+=(_,e)=>{if(e.ColumnIndex>=0&&g.Columns[e.ColumnIndex].Name=="PaymentPurpose"){e.Value=e.Value?.ToString() switch{"Advance"=>"Avans","Settlement"=>"Hakediş ödemesi",var x=>x};e.FormattingApplied=true;}if(e.ColumnIndex>=0&&g.Columns[e.ColumnIndex].Name=="WorkflowState"){e.Value=e.Value?.ToString() switch{"Prepared"=>"Hazırlanmış","Approved"=>"Mali onaylı","Posted"=>"Deftere aktarılmış",var x=>x};e.FormattingApplied=true;}if(e.ColumnIndex>=0&&g.Columns[e.ColumnIndex].Name=="SourceKind"){e.Value=e.Value?.ToString()=="Whole"?"Ana puantaj":"Proje parçası";e.FormattingApplied=true;}};return g;
    }
    private static FlowLayoutPanel Bar(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};p.Controls.AddRange(controls);return p;}
    internal AccrualFoundationForm(Guid project,UserSession user)
    {
        this.project=project;Session=user;PersistGridLayout=false;Text="Muhasebe — Tarihli tarife / dönem hakedişi hesabı";ClientSize=new(1150,760);
        var today=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,DailyClosingService.BusinessZone).Date;from.Value=new(today.Year,today.Month,1);to.Value=from.Value.AddMonths(1).AddDays(-1);
        var tabs=new TabControl{Dock=DockStyle.Fill};var a=new TabPage("Saatlik TL tarifeleri");a.Controls.Add(rates);a.Controls.Add(Bar(add,revoke));var b=new TabPage("Dönem önizlemesi");b.Controls.Add(preview);b.Controls.Add(Bar(calculate));var c=new TabPage("Değişmez hesap geçmişi");c.Controls.Add(history);c.Controls.Add(Bar(supplemental,approve,postToLedger,postingItems,corrections));var d=new TabPage("Seçili hesabın satırları");d.Controls.Add(lines);tabs.TabPages.AddRange([a,b,c,d]);Controls.Add(tabs);Controls.Add(info);info.Dock=DockStyle.Bottom;
        Button load=Button("Önizlemeyi hesapla"),refresh=Button("Tarife / geçmişi yenile"),details=Button("Seçili hesabın satırları"),close=Button("Kapat");Controls.Add(Bar(new Label{Text="Başlangıç",AutoSize=true},from,new Label{Text="Bitiş (dahil)",AutoSize=true},to,load,refresh,details,close));
        info.Text="Saatlik TL × onaylı ücret dakikası / 60; iş günü tarifesi kullanılır. Satır başına 2 ondalık, yarım değer sıfırdan uzağa yuvarlanır. Hesap hazırlama ve mali onay bakiye değiştirmez; bağımsız onay sonrası aktarım ayrıca yapılır. Ek hesap, aktarılmış asıl hesaba bağlı yeni kaynakları alır.";
        from.ValueChanged+=(_,_)=>{prepared=null;calculate.Enabled=false;};to.ValueChanged+=(_,_)=>{prepared=null;calculate.Enabled=false;};rates.SelectionChanged+=(_,_)=>RefreshEditState();history.SelectionChanged+=(_,_)=>RefreshEditState();
        async Task LoadPreviewAsync(Guid? parent,DateTime first,DateTime last)
        {
            prepared=null;preparedParent=null;
            await RunOperationAsync(async ct=>{var p=await Service.PreviewAsync(project,first,last,ct,parent);if(IsDisposed)return;prepared=p;preparedParent=parent;preparedFrom=first;preparedTo=last;var data=RemoteApi.Decode(p.Lines);preview.DataSource=data;preview.ClearSelection();info.Text=(parent.HasValue?"EK HESAP · ":"ASIL HESAP · ")+$"{data.Rows.Count} yeni onaylı kaynak · Toplam {data.AsEnumerable().Where(r=>!r.IsNull("Amount")).Sum(r=>r.Field<decimal>("Amount")):N2} TL. "+(p.CanRecord?"Hesap hazırlanabilir; bakiye/ödeme oluşmaz.":string.Join(" · ",p.Issues));tabs.SelectedTab=b;});
        }
        load.Click+=async(_,_)=>await LoadPreviewAsync(null,Date(from),Date(to));
        supplemental.Click+=async(_,_)=>{if(Row(history) is not{} row||!row.IsNull("ParentCalculationId")||row.IsNull("PostingId"))return;Guid parent=row.Field<Guid>("CalculationId");var first=row.Field<DateTime>("PeriodStart");var last=row.Field<DateTime>("PeriodEnd");from.Value=first;to.Value=last;await LoadPreviewAsync(parent,first,last);};
        refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;
        add.Click+=async(_,_)=>{DataTable? targets=null;if(!await RunOperationAsync(async ct=>targets=await Service.TargetsAsync(project,ct))||IsDisposed||targets==null)return;using var dialog=new HourlyRateDialog(project,User,targets);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        revoke.Click+=async(_,_)=>{if(Row(rates) is not{} r)return;var id=r.Field<Guid>("RateId");var v=((byte[])r["RecordVersion",DataRowVersion.Original]).ToArray();if(MessageBox.Show(this,"Tarife yeni hesaplamalardan kaldırılsın mı? Önceki hesapların oran/tutarı değişmez. Yeni tarihli tarife ayrıca oluşturulur.","Tarifeyi kaldır",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;if(await RunChangeOperationAsync(ct=>Service.RevokeRateAsync(new(project,id,v,RemoteApi.Reason),ct))&&!IsDisposed)await ReloadAsync();};
        calculate.Click+=async(_,_)=>{if(prepared==null||!prepared.CanRecord||Date(from)!=preparedFrom||Date(to)!=preparedTo)return;using var dialog=new AccrualCalculationDialog(project,User,preparedFrom,preparedTo,prepared,preparedParent);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        details.Click+=async(_,_)=>{if(Row(history) is not{} r){MessageBox.Show(this,"Hesap geçmişinden bir satır seçin.");return;}Guid id=r.Field<Guid>("CalculationId");await RunOperationAsync(async ct=>{var data=await Service.LinesAsync(project,id,ct);if(IsDisposed)return;lines.DataSource=data;lines.ClearSelection();tabs.SelectedTab=d;});};
        approve.Click+=async(_,_)=>{if(Row(history) is not{} row)return;using var dialog=new AccrualApprovalDialog(project,User,row);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        postToLedger.Click+=async(_,_)=>{if(Row(history) is not{} row)return;Guid id=row.Field<Guid>("CalculationId");Wire.AccrualPostingQuote? quote=null;if(!await RunOperationAsync(async ct=>quote=await Service.PostingQuoteAsync(project,id,ct))||IsDisposed||quote==null)return;using var dialog=new AccrualPostingDialog(project,id,User,quote);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        corrections.Click+=async(_,_)=>{if(Row(history) is not{} r)return;using var dialog=new AccrualCorrectionsForm(project,r.Field<Guid>("CalculationId"),User);dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();};
        postingItems.Click+=async(_,_)=>{if(Row(history) is not{} row)return;Guid id=row.Field<Guid>("CalculationId");await RunOperationAsync(async ct=>{var data=await Service.PostingItemsAsync(project,id,ct);if(IsDisposed)return;lines.DataSource=data;lines.ClearSelection();tabs.SelectedTab=d;});};
        close.Click+=(_,_)=>Close();Load+=async(_,_)=>await ReloadAsync();PageRequested+=(_,_)=>Close();SessionRevoked+=(_,_)=>Close();SizeChanged+=(_,_)=>info.MaximumSize=new(Math.Max(1,ClientSize.Width-30),0);
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(780,540));}
    protected override void RefreshEditState(){bool admin=Session?.Role==UserRole.Admin&&!IsOperationRunning;var selected=Row(history);corrections.Enabled=!IsOperationRunning&&selected!=null&&!selected.IsNull("PostingId");supplemental.Enabled=admin&&selected!=null&&selected.IsNull("ParentCalculationId")&&!selected.IsNull("PostingId");approve.Enabled=admin&&selected?.Field<bool>("CanApprove")==true;postToLedger.Enabled=admin&&selected!=null&&!selected.IsNull("ApprovalId")&&selected.IsNull("PostingId");postingItems.Enabled=!IsOperationRunning&&selected!=null&&!selected.IsNull("PostingId");add.Enabled=admin;revoke.Enabled=admin&&Row(rates) is{} r&&r.IsNull("RevokedUtc");calculate.Enabled=admin&&prepared?.CanRecord==true&&Date(from)==preparedFrom&&Date(to)==preparedTo;}
    private async Task ReloadAsync(){prepared=null;preparedParent=null;await RunOperationAsync(async ct=>{var r=await Service.RatesAsync(project,ct);var h=await Service.HistoryAsync(project,Date(from),Date(to),ct);if(IsDisposed)return;rates.DataSource=r;history.DataSource=h;rates.ClearSelection();history.ClearSelection();preview.DataSource=null;lines.DataSource=null;info.Text="Tarife ve hesap geçmişi yenilendi. Önizleme yeniden oluşturulmalı. Hazırlama ve mali onay bakiye değiştirmez; bağımsız onay sonrası aktarım ayrı yapılır.";});}
}

internal sealed class HourlyRateDialog:CalendarEditDialog
{
    private sealed record Target(DataRow Row){public override string ToString()=>$"{Row["AD"]} {Row["SOYAD"]} · {(Row.IsNull("StartDate")?"Eski başlangıç bilinmiyor":Row.Field<DateTime>("StartDate").ToString("dd.MM.yyyy"))} – {(Row.IsNull("EndDate")?"Açık dönem":Row.Field<DateTime>("EndDate").ToString("dd.MM.yyyy"))}";}
    private readonly Guid project,id=Guid.NewGuid();
    private readonly ComboBox person=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=570};
    private readonly DateTimePicker from=new(){Format=DateTimePickerFormat.Short,Width=180},to=new(){Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false,Width=180};
    private readonly NumericUpDown hourly=new(){DecimalPlaces=2,Minimum=0,Maximum=1000000,ThousandsSeparator=true,Width=200};
    internal HourlyRateDialog(Guid project,UserSession user,DataTable targets):base(user,"Muhasebe — Tarihli saatlik TL tarifesi","Saatlik ücret tarifesi")
    {
        this.project=project;foreach(DataRow r in targets.Rows)person.Items.Add(new Target(r));
        person.SelectedIndexChanged+=(_,_)=>{if(person.SelectedItem is not Target t)return;var r=t.Row;var first=r.IsNull("StartDate")?DateTime.Today:r.Field<DateTime>("StartDate");from.Value=first;to.Checked=!r.IsNull("EndDate");if(to.Checked)to.Value=r.Field<DateTime>("EndDate");};if(person.Items.Count>0)person.SelectedIndex=0;
        Field("Personel / değişmeyen çalışma dönemi",person);Field("Başlangıç (dahil)",from);Field("Bitiş (dahil; açık uç için işareti kaldırın)",to);Field("Saatlik TL (yalnız saatlik birim; pozitif tutarı açıkça girin)",hourly);
        Fields.Controls.Add(new Label{AutoSize=true,Text="Tarife proje ve çalışma dönemine bağlıdır. Aynı gün için etkin tarifeler çakışamaz. Oran/gövde sonradan düzenlenmez; yanlış tarife kaldırılıp yeni kimlikle oluşturulur. İş günü tarihteki oranı seçer; gece yarısında otomatik tarife bölünmesi yapılmaz. Günlük/aylık ücret, vergi veya fazla mesai katsayısı türetilmez."});Fields.SizeChanged+=(_,_)=>person.Width=Math.Max(1,Math.Min(570,Fields.ClientSize.Width-48));AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)
    {var t=person.SelectedItem as Target??throw new ArgumentException("Çalışma dönemi seçin.");return new Wire.HourlyRateCreate(project,id,t.Row.Field<Guid>("PersonId"),t.Row.Field<Guid>("EmploymentId"),((byte[])t.Row["EmploymentVersion",DataRowVersion.Original]).ToArray(),DateTime.SpecifyKind(from.Value.Date,DateTimeKind.Unspecified),to.Checked?DateTime.SpecifyKind(to.Value.Date,DateTimeKind.Unspecified):null,hourly.Value,reason);}
}

internal sealed class AccrualCalculationDialog:CalendarEditDialog
{
    private readonly Wire.AccrualCalculate request;
    internal AccrualCalculationDialog(Guid project,UserSession user,DateTime from,DateTime to,Wire.AccrualPreview preview,Guid? parent=null):base(user,"Muhasebe — Değişmez dönem hesabını kaydet","Dönem hakedişi hesap dökümü")
    {
        request=new(project,Guid.NewGuid(),from,to,preview.Fingerprint,"",parent);Fields.Controls.Add(new Label{AutoSize=true,Text=parent.HasValue?$"EK HESAP · Asıl hesap: {parent:D}\nYalnız daha önce hesaplanmamış kaynaklar alınır. Asıl hesap ve önceki ekler değişmez.":"ASIL DÖNEM HESABI"});var data=RemoteApi.Decode(preview.Lines);decimal total=data.AsEnumerable().Sum(r=>r.Field<decimal>("Amount"));Fields.Controls.Add(new Label{AutoSize=true,Text=$"Dönem: {from:dd.MM.yyyy} – {to:dd.MM.yyyy}\nKaynak: {data.Rows.Count} satır · Hesap toplamı: {total:N2} TL.\nSaatlik TL × ücret dakikası / 60; satır başına 2 ondalık, yarım değer sıfırdan uzağa yuvarlanır."});
        Fields.Controls.Add(new Label{AutoSize=true,Text="Kaydet ve gerekçe ile hesap dökümü değişmez hale gelir; oran/dakika/kaynak sürümleri saklanır. Aynı kaynak yeniden hesaplanamaz. Kaynak puantajın tamamı ve bağlı proje/parça onayları kilitlenir; başka projenin parçası da bu ana kaydı iade edemez. Bu temel sürümde bağlantılı düzeltme/geri alma yoktur. Önizlemeyi doğruladığınızdan emin olun.\nBu işlem bakiye artırmaz, maliyet defterine işlemez, bordro veya ödeme oluşturmaz. Yanıt kaybolursa açık pencerede aynı isteği yeniden gönderin veya dönem geçmişini kontrol edin; aynı kaynak ikinci kez hesaplanamaz. Ek hesap da ayrıca bağımsız mali onay ve tek seferlik aktarım gerektirir."});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
