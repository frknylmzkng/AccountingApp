using System.Data;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class WorkAllocationEditDialog : CalendarEditDialog
{
    private sealed record Target(DataRow Row):ITimesheetDraftChoice{public string DraftKey=>TimesheetDraftStore.RowKey(Row,"RosterAssignmentId","RosterVersion","ProjectId","ProjectVersion");public override string ToString()=>$"{Row["ProjectCode"]} · {Row["ProjectName"]} · {Row["TeamName"]} · {Row.Field<DateTime>("StartDate"):dd.MM.yyyy}–{(Row.IsNull("EndDate")?"Açık":Row.Field<DateTime>("EndDate").ToString("dd.MM.yyyy"))} · {(Row.Field<bool>("IsSupplementary")?"Ek":"Asıl")}";}
    private readonly Guid project;private Guid set=Guid.NewGuid(),command=Guid.NewGuid();private readonly DataRow parent;private readonly List<Wire.WorkSegment> pieces=[];
    private readonly ComboBox target=new(){Width=570,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly DateTimePicker start=Moment(),end=Moment();private readonly NumericUpDown pause=new(){Maximum=1440,Width=150},paid=new(){Maximum=1440,Width=150};
    private readonly DataGridView preview=new(){Width=570,Height=240,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};private readonly Label totals=new(){AutoSize=true};private readonly TextBox token=new(){Visible=false};
    private readonly DataTable targets;
    private static DateTimePicker Moment()=>new(){Width=280,Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm"};
    private static DateTime Read(DateTimePicker d)=>DateTime.SpecifyKind(new DateTime(d.Value.Year,d.Value.Month,d.Value.Day,d.Value.Hour,d.Value.Minute,0),DateTimeKind.Unspecified);
    internal WorkAllocationEditDialog(Guid project,UserSession user,DataRow parent,DataTable targets):base(user,"Muhasebe — Gün içi proje / mola dağılımı","Proje süre dağılımı")
    {
        this.project=project;this.parent=parent;this.targets=targets;ClientSize=new(800,850);RecordSelection.Configure(preview);
        Fields.Controls.Add(new Label{Text=$"{parent["AD"]} {parent["SOYAD"]} · {parent.Field<DateTime>("WorkDate"):dd.MM.yyyy}\nKaynak: {parent.Field<DateTime>("LocalStart"):dd.MM HH:mm} → {parent.Field<DateTime>("LocalEnd"):dd.MM HH:mm} · Mola {parent["BreakMinutes"]} dk · Ücretlendirilecek {parent["PayableMinutes"]} dk.\nHer parça için projeyi, tarihli atamayı, aralığı, mola ve ücret dakikasını açıkça seçin. Mola otomatik oranlanmaz. Kaydet bütün listeyi tek işlemde saklar; onay vermez.",AutoSize=true});
        foreach(DataRow t in targets.Rows)target.Items.Add(new Target(t));start.Value=parent.Field<DateTime>("LocalStart");end.Value=parent.Field<DateTime>("LocalEnd");
        Field("Hedef proje / ekip ve tarihli atama",target);Field("Parça başlangıcı (dahil)",start);Field("Parça bitişi (hariç)",end);Field("Bu parçaya dağıtılan mola (dakika)",pause);Field("Bu parçanın ücretlendirilecek dakikası",paid);
        var add=new Button{Text="Parçayı önizlemeye ekle",AutoSize=true};var remove=new Button{Text="Önizlemedeki seçili parçayı çıkar",AutoSize=true};Field("Önizleme düzenleme",add);Field("",remove);Fields.Controls.Add(preview);Fields.Controls.Add(totals);Fields.Controls.Add(token);TrackChanges("Proje süre dağılımı",token);
        Fields.Controls.Add(new Label{Text="Parçalar tam kaynak aralığını boşluksuz/çakışmasız kapsamalı. Mola toplamı ana mola, ücret dakikası toplamı ana ücret dakikasıyla aynı olmalı. Bir molayı iki projeye yazmayın. En fazla 50 parça. Eksik hedef ataması yönetici tarafından gün içi ek atama olarak hazırlanabilir.",AutoSize=true});
        add.Click+=(_,_)=>{try{Add();}catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){MessageBox.Show(this,ex.Message,"Parça eklenemedi");}};
        remove.Click+=(_,_)=>{if(preview.SelectedRows.Count!=1||preview.SelectedRows[0].DataBoundItem is not DataRowView r)return;pieces.RemoveAll(p=>p.SegmentId==r.Row.Field<Guid>("SegmentId"));Changed();};
        Fields.SizeChanged+=(_,_)=>target.Width=preview.Width=Math.Max(1,Fields.ClientSize.Width-48);UpdatePreview();AcceptTrackedChanges();
        PersistentDraft(project,"Allocation",parent.Field<Guid>("TimesheetId").ToString("D"),TimesheetDraftStore.RowKey(parent,"TimesheetId","RecordVersion","PersonVersion","RosterVersion"),parent["AD"]+" "+parent["SOYAD"]+" · Proje/mola dağılımı",
            ()=>JsonSerializer.SerializeToElement(new TimesheetDraftExtra(set,command,Pieces:pieces.ToArray(),Labels:pieces.Select(p=>{var t=targets.AsEnumerable().First(t=>t.Field<Guid>("RosterAssignmentId")==p.RosterAssignmentId);return t["ProjectCode"]+" · "+t["ProjectName"]+" · "+t["TeamName"];}).ToArray())),extra=>
            {
                var x=TimesheetDraftExtra.Read(extra);var restored=x.Pieces??[];if(restored.Length>50)throw new InvalidOperationException("Parça taslağı sınırı aşıldı.");
                foreach(var part in restored)
                {var t=targets.AsEnumerable().SingleOrDefault(t=>t.Field<Guid>("RosterAssignmentId")==part.RosterAssignmentId&&t.Field<Guid>("ProjectId")==part.ProjectId);if(t==null||!((byte[])t["RosterVersion"]).SequenceEqual(part.RosterVersion)||!((byte[])t["ProjectVersion"]).SequenceEqual(part.ProjectVersion))throw new InvalidOperationException("Taslağın hedef proje/atama sürümü değişmiş.");SchedulingRules.UtcRange(part.Start,part.End);}
                return ()=>{set=x.Id;command=x.Command;pieces.Clear();pieces.AddRange(restored);UpdatePreview();};
            },()=>"Parça sayısı: "+pieces.Count+" · Mola: "+pieces.Sum(p=>p.BreakMinutes)+" dk · Ücret: "+pieces.Sum(p=>p.PayableMinutes)+" dk. Tamamlanmamış önizleme de saklanır.");
    }
    private void Add()
    {
        var t=(target.SelectedItem as Target)?.Row??throw new ArgumentException("Hedef proje/atama seçin.");if(pieces.Count>=50)throw new ArgumentException("En fazla 50 parça.");
        var a=Read(start);var b=Read(end);var utc=SchedulingRules.UtcRange(a,b);int gross=(int)(utc.End-utc.Start).TotalMinutes;
        if(a<parent.Field<DateTime>("LocalStart")||b>parent.Field<DateTime>("LocalEnd")||pause.Value>gross||paid.Value>gross-pause.Value)throw new ArgumentException("Aralık kaynak içinde olmalı; mola ve ücret dakikası net süreyi aşamaz.");
        pieces.Add(new(Guid.NewGuid(),t.Field<Guid>("ProjectId"),((byte[])t["ProjectVersion"]).ToArray(),t.Field<Guid>("RosterAssignmentId"),((byte[])t["RosterVersion"]).ToArray(),a,b,(int)pause.Value,(int)paid.Value));
        if(b<parent.Field<DateTime>("LocalEnd")){start.Value=b;end.Value=parent.Field<DateTime>("LocalEnd");}pause.Value=paid.Value=0;Changed();
    }
    private void Changed(){token.Text=Guid.NewGuid().ToString("N");MarkTrackedChanges();UpdatePreview();}
    private void UpdatePreview()
    {
        var rows=new DataTable();rows.Columns.Add("SegmentId",typeof(Guid));rows.Columns.Add("Proje / ekip");rows.Columns.Add("Başlangıç",typeof(DateTime));rows.Columns.Add("Bitiş",typeof(DateTime));rows.Columns.Add("Brüt dk",typeof(int));rows.Columns.Add("Mola dk",typeof(int));rows.Columns.Add("Net dk",typeof(int));rows.Columns.Add("Ücret dk",typeof(int));
        int gross=0;foreach(var p in pieces.OrderBy(p=>p.Start)){var t=targets.AsEnumerable().First(r=>r.Field<Guid>("RosterAssignmentId")==p.RosterAssignmentId);var utc=SchedulingRules.UtcRange(p.Start,p.End);int g=(int)(utc.End-utc.Start).TotalMinutes;gross+=g;rows.Rows.Add(p.SegmentId,t["ProjectCode"]+" · "+t["TeamName"],p.Start,p.End,g,p.BreakMinutes,g-p.BreakMinutes,p.PayableMinutes);}
        preview.DataSource=rows;preview.Columns["SegmentId"]!.Visible=false;preview.Columns["Başlangıç"]!.DefaultCellStyle.Format=preview.Columns["Bitiş"]!.DefaultCellStyle.Format="dd.MM.yyyy HH:mm";preview.ClearSelection();
        totals.Text=$"Önizleme: {pieces.Count} parça · Brüt {gross} dk · Mola {pieces.Sum(p=>p.BreakMinutes)}/{parent["BreakMinutes"]} dk · Ücret {pieces.Sum(p=>p.PayableMinutes)}/{parent["PayableMinutes"]} dk. Toplamların eşitliği tek başına yetmez; aralıklar da boşluksuz olmalı.";
    }
    protected override object MakeRequest(string reason)
    {
        if(pieces.Count==0)throw new ArgumentException("Önce zaman parçalarını ekleyin.");
        var next=parent.Field<DateTime>("LocalStart");foreach(var p in pieces.OrderBy(p=>p.Start)){if(p.Start!=next)throw new ArgumentException("Önizlemede boşluk veya çakışma var; parçaları düzeltin.");next=p.End;}
        if(next!=parent.Field<DateTime>("LocalEnd")||pieces.Sum(p=>p.BreakMinutes)!=parent.Field<int>("BreakMinutes")||pieces.Sum(p=>p.PayableMinutes)!=parent.Field<int>("PayableMinutes"))throw new ArgumentException("Önizleme bitişi, mola ve ücret dakikası toplamları ana kayıtla eşleşmiyor.");
        return new Wire.WorkAllocation(project,command,set,parent.Field<Guid>("TimesheetId"),((byte[])parent["RecordVersion",DataRowVersion.Original]).ToArray(),((byte[])parent["PersonVersion"]).ToArray(),((byte[])parent["RosterVersion"]).ToArray(),pieces.OrderBy(p=>p.Start).ToArray(),reason);
    }
}
internal sealed class WorkAllocationReleaseDialog : CalendarEditDialog
{
    private readonly Wire.WorkAllocationRelease request;
    internal WorkAllocationReleaseDialog(Guid project,UserSession user,DataRow row):base(user,"Muhasebe — Süre dağılımını kaldır","Süre dağılımını kaldırma")
    {request=new(project,Guid.NewGuid(),row.Field<Guid>("SetId"),((byte[])row["RecordVersion",DataRowVersion.Original]).ToArray(),((byte[])row["TimesheetVersion",DataRowVersion.Original]).ToArray(),"");Fields.Controls.Add(new Label{AutoSize=true,Text="Dağılım ve parçaları silinmez; kaldırma gerekçesi/aktörü/zamanı geçmişte kalır. Ana puantajın saatleri, mola ve ücret dakikası değişmez. Yeni dağılım açılabilir veya kaynak saat/olay düzenlemesine geçilebilir. Onaylı/gönderilmiş kayıt önce ilgili projenin parçasından yetkili amir tarafından iade edilmeli/yeniden açılmalı. Kaldırma bütün hedeflerde puantaj giriş hakkı ister."});AcceptTrackedChanges();}
    protected override object MakeRequest(string reason)=>request with{Reason=reason};
}
internal sealed class WorkAllocationForm : SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly DataRow parent;private readonly bool enter;private bool changed,loading;
    private readonly DataGridView sets=Grid(),segments=Grid();private readonly Button create=new(){Text="Yeni dağılım / önizle…",AutoSize=true},release=new(){Text="Seçili dağılımı kaldır…",AutoSize=true};
    private TimesheetService Service=>new(AppDatabase.ConnectionString,User);
    private static FlowLayoutPanel Bar(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};p.Controls.AddRange(controls);return p;}
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView r?r.Row:null;
    private static DataGridView Grid()
    {
        var g=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};RecordSelection.Configure(g);g.DataBindingComplete+=(_,_)=>{var titles=new Dictionary<string,string>{{"ProjectCode","Proje kodu"},{"ProjectName","Proje"},{"TeamName","Ekip"},{"SequenceNo","Sıra"},{"LocalStart","Başlangıç"},{"LocalEnd","Bitiş"},{"GrossMinutes","Brüt dk"},{"NetMinutes","Net dk"},{"BreakMinutes","Mola dk"},{"PayableMinutes","Ücret dk"},{"IsSealed","Dağılım tamamlandı"},{"CreatedBy","Hazırlayan"},{"CreatedUtc","Oluşturma UTC"},{"ReleasedUtc","Kaldırma UTC"},{"ReleasedBy","Kaldıran"},{"PolicyVersion","Politika"},{"IsSupplementary","Ek atama"},{"TimesheetStatus","Güncel puantaj durumu"}};foreach(DataGridViewColumn c in g.Columns){if(titles.TryGetValue(c.Name,out var t))c.HeaderText=t;if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[])||c.Name is "StartUtc" or "EndUtc")c.Visible=false;if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format="dd.MM.yyyy HH:mm";}};return g;
    }
    internal WorkAllocationForm(Guid project,UserSession user,DataRow parent,bool enter)
    {
        this.project=project;this.enter=enter;var copy=parent.Table.Clone();copy.ImportRow(parent);this.parent=copy.Rows[0];Session=user;PersistGridLayout=false;Text="Muhasebe — Gün içi proje / mola dağılımı geçmişi";ClientSize=new(1120,740);
        var tabs=new TabControl{Dock=DockStyle.Fill};var a=new TabPage("Dağılım sürümleri");a.Controls.Add(sets);var b=new TabPage("Seçili sürümün parçaları");b.Controls.Add(segments);tabs.TabPages.AddRange([a,b]);Controls.Add(tabs);
        Controls.Add(new Label{Dock=DockStyle.Top,AutoSize=true,Padding=new(8),Text=$"{parent["AD"]} {parent["SOYAD"]} · {parent.Field<DateTime>("WorkDate"):dd.MM.yyyy}\nDağılım hazırlama/kaldırma bütün hedeflerde giriş hakkı; gönderim bütün hedeflerde giriş hakkı ister. Amir onayı/iadesi her projenin kendi parçasında bağımsızdır. Detay görmek için geçmiş hedef projelerinde de okuma hakkı gerekir."});
        var refresh=new Button{Text="Yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};Controls.Add(Bar(create,release,refresh,close));close.Click+=(_,_)=>Close();refresh.Click+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;create.Click+=async(_,_)=>await CreateAsync();release.Click+=async(_,_)=>await ReleaseAsync();sets.SelectionChanged+=(_,_)=>{FilterSegments();RefreshEditState();};Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,520));}
    protected override void RefreshEditState(){bool ready=!IsOperationRunning&&!loading;create.Enabled=ready&&enter&&!changed&&parent.IsNull("ActiveAllocationId")&&parent.Field<string>("Status") is "Draft" or "Returned";release.Enabled=ready&&enter&&Row(sets) is{} r&&r.IsNull("ReleasedUtc")&&r.Field<string>("TimesheetStatus") is "Draft" or "Returned";}
    private async Task ReloadAsync(){if(loading)return;loading=true;try{await RunOperationAsync(async ct=>{var d=await Service.AllocationsAsync(project,parent.Field<Guid>("TimesheetId"),ct);if(IsDisposed)return;sets.DataSource=d.Sets;segments.DataSource=d.Segments;sets.ClearSelection();segments.ClearSelection();FilterSegments();});}finally{loading=false;if(!IsDisposed)RefreshEditState();}}
    private void FilterSegments(){if(segments.DataSource is DataTable data){var selected=Row(sets);data.DefaultView.RowFilter=selected==null?"1=0":"Convert(SetId, 'System.String') = '"+selected.Field<Guid>("SetId").ToString("D")+"'";segments.ClearSelection();}}
    private async Task CreateAsync()
    {
        DataTable? targets=null;bool ok=await RunOperationAsync(async ct=>{if(parent.IsNull("LocalStart")||parent.IsNull("LocalEnd")||parent.IsNull("PayableMinutes"))throw new InvalidOperationException("Önce ana puantajın saat, mola ve ücret dakikasını tamamlayın.");targets=await Service.AllocationTargetsAsync(project,parent.Field<Guid>("TimesheetId"),ct);});if(!ok||IsDisposed||targets==null)return;
        using var d=new WorkAllocationEditDialog(project,User,parent,targets);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(d.Saved)changed=true;if(!IsDisposed)await ReloadAsync();
    }
    private async Task ReleaseAsync(){if(Row(sets) is not{} r)return;using var d=new WorkAllocationReleaseDialog(project,User,r);d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);if(d.Saved)changed=true;if(!IsDisposed)await ReloadAsync();}
}
