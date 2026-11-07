using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class TimesheetDraftsForm : SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;
    private readonly DataGridView grid=new(){Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
    private readonly Label detail=new(){AutoSize=true,Padding=new(10)};
    private readonly Dictionary<Guid,(string Path,TimesheetDraftRecord? Record)> entries=[];
    private TimesheetDraftStore? store;
    private Wire.TimesheetDraftContext? owner;
    private readonly Button inspect=new(){Text="Salt okunur incele…",AutoSize=true};
    private readonly Button retry=new(){Text="Aynı isteği yeniden gönder…",AutoSize=true},resolve=new(){Text="Sunucu sonucunu kontrol et",AutoSize=true},discard=new(){Text="Taslağı sil / istekten vazgeç…",AutoSize=true};
    private TimesheetService Service=>new(AppDatabase.ConnectionString,User);
    internal TimesheetDraftsForm(Guid project,UserSession user)
    {
        this.project=project;Session=user;PersistGridLayout=false;Text="Muhasebe — Kalıcı puantaj taslakları / kurtarma";ClientSize=new(1000,660);RecordSelection.Configure(grid);
        var note=new Label{Dock=DockStyle.Top,AutoSize=true,Padding=new(10),Text="Bu bilgisayardaki, bu hesap/veritabanı/projeye ait şifreli taslaklar. Gönderilmemiş içerik buradan otomatik kaydedilmez; özgün personel/atamayı açıp açıkça kurtarın. Eski sürüm/hesap nesli değişmişse alanlar uygulanmaz.\nBelirsiz istek yalnız özgün işlem kimliği ve içerikle gönderilir. Vazgeçme sunucuda kalıcı iptal izi bırakır; kaydedilmiş iş kaydını geri almaz."};
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(8)};var refresh=new Button{Text="Yenile",AutoSize=true};var close=new Button{Text="Kapat",AutoSize=true};bar.Controls.AddRange([inspect,resolve,retry,discard,refresh,close]);
        Controls.Add(grid);Controls.Add(detail);detail.Dock=DockStyle.Bottom;Controls.Add(bar);Controls.Add(note);
        grid.DataBindingComplete+=(_,_)=>{if(grid.Columns["Id"] is{} c)c.Visible=false;};grid.SelectionChanged+=(_,_)=>{var selected=Selected();detail.Text=selected?.Record?.Preview??"Taslağı seçin. Okunamayan dosyalar değiştirilmez.";RefreshEditState();};
        void Fit(){note.MaximumSize=detail.MaximumSize=new(Math.Max(1,ClientSize.Width-36),0);}
        SizeChanged+=(_,_)=>Fit();Fit();
        inspect.Click+=async(_,_)=>await InspectAsync();
        refresh.Click+=async(_,_)=>await ReloadAsync();close.Click+=(_,_)=>Close();resolve.Click+=async(_,_)=>await HandleAsync(false,false);retry.Click+=async(_,_)=>await HandleAsync(true,false);discard.Click+=async(_,_)=>await HandleAsync(false,true);
        Load+=async(_,_)=>await ReloadAsync();RefreshAction=ReloadAsync;PageRequested+=(_,_)=>Close();SessionRevoked+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(700,500));}
    private (string Path,TimesheetDraftRecord? Record)? Selected()=>grid.SelectedRows.Count==1&&grid.SelectedRows[0].DataBoundItem is DataRowView r&&entries.TryGetValue(r.Row.Field<Guid>("Id"),out var item)?item:null;
    protected override void RefreshEditState()
    {
        var r=Selected()?.Record;bool ready=!IsOperationRunning&&r!=null;
        inspect.Enabled=discard.Enabled=ready;resolve.Enabled=retry.Enabled=ready&&r?.Intent!=null;
    }
    private async Task ReloadAsync()=>await RunOperationAsync(async ct=>
    {
        owner=await Service.DraftContextAsync(project,ct);if(IsDisposed)return;
        store=new(AppDatabase.ConnectionString,owner,project);entries.Clear();
        var data=new DataTable();data.Columns.Add("Id",typeof(Guid));foreach(var name in new[]{"Tür","Kayıt","Kaydedilen yerel zaman","Durum"})data.Columns.Add(name);
        var paths=Directory.Exists(store.DirectoryPath)?Directory.EnumerateFiles(store.DirectoryPath,"*.draft").Take(201).ToArray():[];
        if(paths.Length>200)throw new InvalidOperationException("Taslak sayısı 200 sınırını aşıyor; yerel taslak klasörünü yöneticiyle inceleyin.");
        foreach(var path in paths)
        {
            Guid id=Guid.NewGuid();try
            {
                var r=store.Read(path);entries[id]=(path,r);data.Rows.Add(id,r.Kind switch{"Planned"=>"Vardiyalı","Unplanned"=>"Vardiyasız","Csv"=>"CSV","ManualBatch"=>"Toplu manuel",_=>"Proje/mola dağılımı"},r.Caption,r.SavedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"),r.Intent==null?(r.Epoch==owner.AccountEpoch?"Gönderilmedi":"Hesap nesli değişmiş; salt okunur"):"Sonucu çözülmeli");
            }
            catch(Exception ex){AppLog.Failure(ex,"TimesheetDraft.Read");entries[id]=(path,null);data.Rows.Add(id,"Okunamadı","Dosya korundu","","Şifre/biçim/kapsam hatası");}
        }
        grid.DataSource=data;grid.ClearSelection();detail.Text="Gönderilmemiş taslağı kurtarmak için özgün kayıt/atama ekranını yeniden açın; CSV için aynı haftayı yükleyin.";
    });
    private async Task InspectAsync()
    {
        if(Selected()?.Record is not{} record)return;
        string? previewText=null;
        bool ready=await RunOperationAsync(async ct=>
        {
            var extra=TimesheetDraftExtra.Read(record.Extra);
            var projects=extra.Pieces?.Select(x=>x.ProjectId).Distinct().ToArray()??[];
            await Service.CheckDraftPreviewAsync(new(project,projects),ct);if(IsDisposed)return;
            var text=new System.Text.StringBuilder(record.Caption+"\r\n"+record.Preview+"\r\n\r\n");
            foreach(var r in extra.Rows??[])text.AppendLine($"{r.SourceKey} · {r.Start:dd.MM.yyyy HH:mm} → {r.End:dd.MM.yyyy HH:mm} · Mola {r.BreakMinutes} dk · Ücret {r.PayableMinutes?.ToString()??"Belirtilmedi"} dk");
            for(int i=0;i<(extra.Pieces?.Length??0);i++){var r=extra.Pieces![i];text.AppendLine($"Parça {i+1} · {(extra.Labels?.ElementAtOrDefault(i)??"Hedef proje")} · {r.Start:dd.MM.yyyy HH:mm} → {r.End:dd.MM.yyyy HH:mm} · Mola {r.BreakMinutes} dk · Ücret {r.PayableMinutes} dk");}
            foreach(var e in extra.ManualRows??[])
            {var p=e.Planned;var u=e.Unplanned;text.AppendLine($"{e.Caption} · {e.WorkDate:dd.MM.yyyy} · {(p!=null?"Vardiyalı":"Vardiyasız")} · {p?.Start??u?.Start:dd.MM.yyyy HH:mm} → {p?.End??u?.End:dd.MM.yyyy HH:mm} · Mola {p?.BreakMinutes??u?.BreakMinutes} dk · Ücret {p?.PayableMinutes??u?.PayableMinutes} dk · Gözden geçirildi: {e.Reviewed}");}
            previewText=text.ToString();
        });
        if(ready&&!IsDisposed&&previewText!=null)
        {
            using var preview=new TimesheetDraftPreviewForm(User,previewText);preview.SessionRevoked+=(_,_)=>NotifySessionRevoked();preview.ShowDialog(this);
        }
    }

    private async Task HandleAsync(bool send,bool cancel)
    {
        if(Selected() is not{} selected||selected.Record is not{} record||store==null)return;
        bool confirmed=cancel?MessageBox.Show(this,record.Intent==null?"Gönderilmemiş yerel taslak silinsin mi?":"İstek sunucuda kaydedilmemişse kalıcı olarak iptal edilsin mi? Kaydedilmişse yalnız yerel kurtarma dosyası temizlenir; iş kaydı geri alınmaz.","Taslağı sonuçlandır",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes:
            !send||MessageBox.Show(this,"Özgün işlem kimliği, gerekçe, hedef ve sürümlerle aynı istek gönderilsin mi? Yeni seçim veya onay üretilmez.","Aynı isteği gönder",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
        if(!confirmed)return;
        async Task Execute(CancellationToken ct)
        {
            var current=await Service.DraftContextAsync(project,ct);
            if(current.AccountId!=owner?.AccountId||current.DatabaseScope!=owner.DatabaseScope)throw new InvalidOperationException("Taslak kapsamı değişmiş.");
            using var lease=store.Acquire(selected.Path);var original=lease.Read()??throw new InvalidOperationException("Taslak başka pencerede sonuçlandırılmış; yenileyin.");
            if(System.Text.Json.JsonSerializer.Serialize(original)!=System.Text.Json.JsonSerializer.Serialize(record))throw new InvalidOperationException("Taslak seçimden sonra değişmiş; yenileyin.");
            if(record.Intent==null){if(cancel)lease.Clear();return;}
            var status=await Service.ResolveDraftAsync(new(project,record.Kind,record.Intent.Value,cancel,cancel?RemoteApi.Reason:""),ct);
            if(status.State is "Recorded" or "Canceled"){lease.Clear();MessageBox.Show(this,status.State=="Recorded"?"Sunucu işlemi zaten kaydetmiş. Yerel kurtarma dosyası temizlendi; iş kaydı geri alınmadı.":"İstek kalıcı olarak iptal edildi. Gecikmiş aynı istek uygulanamaz; yerel taslak temizlendi.","Taslak sonucu");return;}
            if(status.State!="NotRecorded")throw new InvalidOperationException("Taslak sunucu sonucu anlaşılamadı; dosya korundu.");
            if(!send){MessageBox.Show(this,"Sunucuda makbuz bulunmadı. İstek henüz kaydedilmemiş olabilir; aynı isteği yeniden gönderin veya kalıcı iptal kullanın. Dosya korundu.","Taslak sonucu");return;}
            if(record.Epoch!=current.AccountEpoch)throw new InvalidOperationException("Hesap nesli değişmiş; eski istek yeni nesilde uygulanmadı. Sonucu kontrol edin veya kalıcı olarak iptal edin.");
            object request=TimesheetDraftCommands.Parse(record.Kind,record.Intent.Value);var identity=TimesheetDraftCommands.Identity(request);
            using var reason=AuditContext.UseReason(identity.Reason);await CalendarCommands.RunAsync(request,User,ct);lease.Clear();
        }
        bool done=cancel&&record.Intent!=null?await RunChangeOperationAsync(Execute,"Taslak sonuçlandırıldı.","Puantaj isteğinden vazgeçme"):await RunOperationAsync(Execute);
        if(done&&!IsDisposed)await ReloadAsync();
    }
}

internal sealed class TimesheetDraftPreviewForm:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    internal TimesheetDraftPreviewForm(UserSession user,string text)
    {
        Session=user;PersistGridLayout=false;Text="Muhasebe — Taslak salt okunur inceleme";ClientSize=new(900,580);
        var grid=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells};RecordSelection.Configure(grid);
        var data=new DataTable();data.Columns.Add("Taslak ayrıntısı");foreach(string line in text.Split('\n'))data.Rows.Add(line.TrimEnd('\r'));grid.DataSource=data;grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        Controls.Add(grid);var close=new Button{Dock=DockStyle.Bottom,Text="Kapat",Height=32};Controls.Add(close);close.Click+=(_,_)=>Close();Load+=(_,_)=>{MinimumSize=LogicalToDeviceUnits(new Size(600,400));grid.ClearSelection();};SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
}
