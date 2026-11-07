using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal abstract partial class CalendarEditDialog
{
    private Guid draftProject;
    private string? draftKind,draftTarget,draftBaseline,draftCaption;
    private Func<JsonElement>? captureExtra;
    private Func<JsonElement,Action>? prepareExtra;
    private Func<string>? draftPreview;
    private Func<JsonElement,CancellationToken,Task>? validateDraftExtra;
    private TimesheetDraftStore.Lease? draftLease;
    private TimesheetDraftRecord? heldDraft;
    private Wire.TimesheetDraftContext? draftOwner;
    private bool draftReady,draftBlocked,draftRestoring,draftDirty;
    private readonly System.Windows.Forms.Timer draftTimer=new(){Interval=1200};
    private readonly Label draftStatus=new(){AutoSize=true,Text="Kalıcı taslak hazırlığı…"};
    private readonly Button recoverDraft=new(){Text="Taslağı açıkça kurtar",AutoSize=true},discardDraft=new(){Text="Gönderilmemiş taslağı sil",AutoSize=true},saveDraft=new(){Text="Taslağı şimdi sakla",AutoSize=true};
    private bool DraftAllowsSave=>draftKind==null||draftReady&&!draftBlocked;
    protected void PersistentDraft(Guid project,string kind,string target,string baseline,string caption,Func<JsonElement> extra,Func<JsonElement,Action> restore,Func<string> preview,Func<JsonElement,CancellationToken,Task>? validateExtra=null)
    {
        draftProject=project;draftKind=kind;draftTarget=target;draftBaseline=baseline;draftCaption=caption;captureExtra=extra;prepareExtra=restore;draftPreview=preview;validateDraftExtra=validateExtra;
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(10)};
        bar.Controls.AddRange([draftStatus,recoverDraft,discardDraft,saveDraft]);Controls.Add(bar);
        recoverDraft.Click+=async(_,_)=>await RecoverDraftAsync();discardDraft.Click+=async(_,_)=>await DiscardDraftAsync();saveDraft.Click+=(_,_)=>SaveDraftNow();
        Load+=async(_,_)=>await InitializeDraftAsync();
        TimesheetDraftFields.Watch(Fields,(_,_)=>{if(draftReady&&!draftBlocked&&!draftRestoring&&intent==null){draftDirty=true;draftTimer.Stop();draftTimer.Start();}});
        draftTimer.Tick+=(_,_)=>{draftTimer.Stop();SaveDraftNow();};
        FormClosing+=(_,_)=>{draftTimer.Stop();if(!Saved&&draftDirty)SaveDraftNow();};
        FormClosed+=(_,_)=>{draftTimer.Dispose();draftLease?.Dispose();draftLease=null;};
        // Also release a lease if construction/showing is interrupted before FormClosed.
        Disposed+=(_,_)=>{draftTimer.Dispose();draftLease?.Dispose();draftLease=null;};
    }
    private async Task InitializeDraftAsync()
    {
        await RunOperationAsync(async ct=>
        {
            draftOwner=await new TimesheetService(AppDatabase.ConnectionString,User).DraftContextAsync(draftProject,ct);
            if(IsDisposed)return;
            var store=new TimesheetDraftStore(AppDatabase.ConnectionString,draftOwner,draftProject);
            draftLease=store.Acquire(store.PathFor(draftKind!,draftTarget!));heldDraft=draftLease.Read();draftBlocked=heldDraft!=null;draftReady=true;
            draftStatus.Text=heldDraft==null?"Alan değişiklikleri 1,2 sn sonra şifreli saklanır. Sunucuya gönderilmez.":$"{heldDraft.SavedUtc.ToLocalTime():dd.MM.yyyy HH:mm} tarihli taslak var; önce kurtarın veya inceleyin.";
        });
        if(!IsDisposed)RefreshEditState();
    }
    private void RefreshDraftButtons()
    {
        recoverDraft.Enabled=draftReady&&draftBlocked&&heldDraft?.Intent==null&&!IsOperationRunning;
        discardDraft.Enabled=recoverDraft.Enabled;
        saveDraft.Enabled=draftReady&&!draftBlocked&&intent==null&&!IsOperationRunning;
        draftStatus.MaximumSize=new(Math.Max(1,ClientSize.Width-60),0);
        if(heldDraft?.Intent!=null&&draftBlocked)draftStatus.Text="Gönderilmiş isteğin sonucu belirsiz. Kapatıp Puantaj → Kalıcı taslaklar / kurtarma ekranından sonucu çözün; yeni istek üretilmedi.";
    }
    private TimesheetDraftRecord DraftRecord(JsonElement? pending=null)=>new(1,draftKind!,draftTarget!,draftBaseline!,draftOwner!.AccountEpoch,DateTime.UtcNow,draftCaption!,draftPreview!(),TimesheetDraftFields.Capture(Fields),captureExtra!(),pending);
    private void SaveDraftNow()
    {
        if(!draftReady||draftBlocked||intent!=null||draftRestoring||draftLease==null)return;
        try{draftLease.Write(DraftRecord());draftDirty=false;draftStatus.Text="Yerel şifreli taslak saklandı: "+DateTime.Now.ToString("HH:mm:ss");}
        catch(Exception ex){AppLog.Failure(ex,"TimesheetDraft.Save");draftStatus.Text="Taslak saklanamadı. Taslağı şimdi sakla ile yeniden deneyin; sunucu kaydı için kalıcı niyet saklanması zorunludur.";}
    }
    private async Task RecoverDraftAsync()
    {
        if(heldDraft==null||heldDraft.Intent!=null)return;
        await RunOperationAsync(async ct=>
        {
            var owner=await new TimesheetService(AppDatabase.ConnectionString,User).DraftContextAsync(draftProject,ct);
            if(IsDisposed)return;
            if(owner!=draftOwner||heldDraft.Epoch!=owner.AccountEpoch||heldDraft.Baseline!=draftBaseline)throw new InvalidOperationException("Hesap nesli veya özgün kayıt/atama sürümü değişmiş. Taslak yeni seçime uygulanmadı; Kalıcı taslaklar ekranından salt okunur inceleyin.");
            if(validateDraftExtra!=null)await validateDraftExtra(heldDraft.Extra,ct);
            if(IsDisposed)return;
            var restoreFields=TimesheetDraftFields.PrepareRestore(Fields,heldDraft.Controls);
            var restoreExtra=prepareExtra!(heldDraft.Extra);
            draftRestoring=true;
            try{restoreFields();restoreExtra();MarkTrackedChanges();draftBlocked=false;draftDirty=true;draftStatus.Text="Taslak alanları kurtarıldı; sunucuya yazılmadı. Kontrol edip Kaydet'i kullanın.";}
            finally{draftRestoring=false;}
        });
    }
    private async Task DiscardDraftAsync()
    {
        if(heldDraft==null||heldDraft.Intent!=null||MessageBox.Show(this,"Yalnız gönderilmemiş yerel taslak silinsin mi?","Taslağı sil",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
        await RunOperationAsync(async ct=>
        {
            var owner=await new TimesheetService(AppDatabase.ConnectionString,User).DraftContextAsync(draftProject,ct);
            if(owner.AccountId!=draftOwner?.AccountId||owner.DatabaseScope!=draftOwner.DatabaseScope)throw new InvalidOperationException("Taslak kapsamı değişmiş.");
            draftLease!.Clear();heldDraft=null;draftBlocked=false;draftStatus.Text="Gönderilmemiş taslak silindi. Güncel seçimle yeni giriş yapabilirsiniz.";
        });
    }
    private async Task BeforeDraftSendAsync(object request,CancellationToken ct)
    {
        if(draftKind==null)return;
        if(!DraftAllowsSave||draftLease==null)throw new InvalidOperationException("Kalıcı taslak hazırlığı tamamlanmadı veya eski taslak çözülmedi.");
        var current=await new TimesheetService(AppDatabase.ConnectionString,User).DraftContextAsync(draftProject,ct);
        if(current!=draftOwner)throw new InvalidOperationException("Taslak hesap/veritabanı nesli değişmiş; yeniden giriş yapın.");
        var identity=TimesheetDraftCommands.Identity(request);if(identity.Project!=draftProject)throw new InvalidOperationException("Taslak proje kapsamı uyuşmuyor.");
        var pending=DraftRecord(JsonSerializer.SerializeToElement(request,request.GetType()));
        draftLease.Write(pending);heldDraft=pending;draftDirty=false;draftTimer.Stop();
    }
    private void ClearCompletedDraft(){if(draftKind!=null){draftTimer.Stop();draftLease!.Clear();heldDraft=null;draftDirty=false;}}
}
