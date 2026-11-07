using System.Data;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AttendanceEventsForm : SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly DateTime first;private readonly bool enter;private readonly DataRow? target;private bool targetChanged,loading;
    private readonly DataGridView events=Grid("AttendanceEvents"),matches=Grid("AttendanceMatches");
    private readonly Label note=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)};
    private readonly Button single=Button("Tek ham olay…"),csv=Button("CSV ham olay / önizle…"),link=Button("Seçili puantaja olay eşle…"),ignore=Button("Seçili olayı yoksay…"),release=Button("Seçili eşlemeyi kaldır…");
    private DataTable eventData=new();private TimesheetService Service=>new(AppDatabase.ConnectionString,User);
    private static Button Button(string title)=>new(){Text=title,AutoSize=true};
    private static DataRow? Row(DataGridView g)=>g.SelectedRows.Count==1&&g.SelectedRows[0].DataBoundItem is DataRowView r?r.Row:null;
    private static FlowLayoutPanel Bar(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(6)};p.Controls.AddRange(controls);return p;}
    private static DataGridView Grid(string name)
    {
        var g=new DataGridView{Name=name,Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};RecordSelection.Configure(g);
        g.DataBindingComplete+=(_,_)=>
        {
            var titles=new Dictionary<string,string>{{"AD","Ad"},{"SOYAD","Soyad"},{"SourceSystem","Kaynak sistem"},{"ExternalId","Kaynak olay kimliği"},{"Direction","Yön"},{"OccurredLocal","Ham yerel zaman"},{"OccurredUtc","Ham UTC"},{"TimeZoneId","Saat dilimi"},{"ImportedBy","Olayı giren"},{"ImportedUtc","Alınma UTC"},{"IgnoredUtc","Yoksayma UTC"},{"IgnoredBy","Yoksayan"},{"EventState","Durum / bekleyen işlem"},{"MatchedBy","Eşleyen"},{"CreatedUtc","Eşleme UTC"},{"ReleasedUtc","Kaldırma UTC"},{"ReleasedBy","Kaldıran"},{"MatchPolicy","Eşleme politikası"},{"TimesheetStatus","Güncel puantaj durumu"},{"WorkDate","İş günü"},{"RawIn","Ham giriş"},{"RawOut","Ham çıkış"},{"MatchedStart","Eşlenen giriş"},{"MatchedEnd","Eşlenen çıkış"}};
            foreach(DataGridViewColumn c in g.Columns){if(titles.TryGetValue(c.Name,out var t))c.HeaderText=t;if(c.ValueType==typeof(Guid)||c.ValueType==typeof(byte[]))c.Visible=false;if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name=="WorkDate"?"dd.MM.yyyy":c.Name is "MatchedStart" or "MatchedEnd"?"dd.MM.yyyy HH:mm":"dd.MM.yyyy HH:mm:ss.fffffff";}
        };
        g.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0)return;string name=g.Columns[e.ColumnIndex].Name;if(name=="Direction"){e.Value=e.Value?.ToString()=="In"?"Giriş":"Çıkış";e.FormattingApplied=true;}if(name=="TimesheetStatus"){e.Value=e.Value?.ToString() switch{"Draft"=>"Taslak","Submitted"=>"Onay bekliyor","Approved"=>"Onaylı","Returned"=>"İade / yeniden açıldı","Cancelled"=>"İptal",var v=>v};e.FormattingApplied=true;}};return g;
    }
    internal AttendanceEventsForm(Guid project,UserSession user,DateTime first,bool enter,DataRow? target)
    {
        this.project=project;this.first=first;this.enter=enter;Session=user;PersistGridLayout=false;Text="Muhasebe — Ham giriş / çıkış ve kontrollü eşleme";ClientSize=new(1240,820);
        if(target!=null){var copy=target.Table.Clone();copy.ImportRow(target);this.target=copy.Rows[0];}
        var tabs=new TabControl{Dock=DockStyle.Fill};var raw=new TabPage("Ham olaylar / bekleyenler");raw.Controls.Add(events);raw.Controls.Add(Bar(ignore));var history=new TabPage("Eşleme geçmişi");history.Controls.Add(matches);history.Controls.Add(Bar(release));tabs.TabPages.AddRange([raw,history]);Controls.Add(tabs);Controls.Add(note);
        var refresh=Button("Yenile");var close=Button("Kapat");Controls.Add(Bar(single,csv,link,refresh,close));refresh.Click+=async(_,_)=>await ReloadAsync();close.Click+=(_,_)=>Close();RefreshAction=ReloadAsync;
        single.Click+=async(_,_)=>await ImportAsync(false);csv.Click+=async(_,_)=>await ImportAsync(true);ignore.Click+=async(_,_)=>await IgnoreAsync();release.Click+=async(_,_)=>await ReleaseAsync();
        link.Click+=async(_,_)=>{if(this.target==null||targetChanged)return;using var d=new AttendanceMatchDialog(project,User,this.target,eventData);if(await DialogAsync(d)){targetChanged=true;RefreshEditState();UpdateNote();}};
        events.SelectionChanged+=(_,_)=>RefreshEditState();matches.SelectionChanged+=(_,_)=>RefreshEditState();ClientSizeChanged+=(_,_)=>note.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,560));}
    protected override void RefreshEditState()
    {
        bool ready=!IsOperationRunning&&!loading;single.Enabled=csv.Enabled=ready&&enter;
        link.Enabled=ready&&enter&&!targetChanged&&target!=null&&target.IsNull("ActiveMatchId")&&target.IsNull("ActiveAllocationId")&&target.Field<string>("Status") is "Draft" or "Returned";
        ignore.Enabled=ready&&enter&&Row(events) is{} e&&e.IsNull("IgnoredUtc")&&e.IsNull("ActiveMatchId");
        release.Enabled=ready&&enter&&Row(matches) is{} m&&m.IsNull("ReleasedUtc")&&m.IsNull("ActiveAllocationId")&&m.Field<string>("TimesheetStatus") is "Draft" or "Returned";
    }
    private void UpdateNote()
    {
        string chosen=target==null?"Eşleme için puantaj listesinde bir taslak/iade kaydı seçip bu ekranı yeniden açın.":targetChanged?"Puantaj değişti. Yeni eşleme için bu ekranı kapatıp güncel puantajı yeniden seçin.":$"Hedef: {target["AD"]} {target["SOYAD"]} · {target.Field<DateTime>("WorkDate"):dd.MM.yyyy} · {target["ShiftName"]}. Özgün seçim sürümü korunur.";
        note.Text=$"{first:dd.MM.yyyy} – {first.AddDays(6):dd.MM.yyyy} haftası; gece çıkışları için sonraki günün olayları da gösterilir. Yön/sıra tahmini ve otomatik onay yok.\n{chosen}\nYoksayılan olay ve kaldırılan eşleme geçmişte kalır. Eşlemeyi kaldırma puantaj saatlerini/mola/ücret dakikasını temizler. Son okuma: {DateTime.Now:HH:mm:ss}";
    }
    private async Task ReloadAsync()
    {
        if(loading)return;loading=true;try{await RunOperationAsync(async ct=>{var data=await Service.EventsAsync(project,first,ct);if(IsDisposed)return;eventData=data.Events;events.DataSource=data.Events;matches.DataSource=data.Matches;events.ClearSelection();matches.ClearSelection();UpdateNote();});}finally{loading=false;if(!IsDisposed)RefreshEditState();}
    }
    private async Task<bool> DialogAsync(CalendarEditDialog d)
    {d.SessionRevoked+=(_,_)=>NotifySessionRevoked();d.ShowDialog(this);bool saved=d.Saved;if(!IsDisposed)await ReloadAsync();return saved;}
    private async Task ImportAsync(bool csv)
    {
        using var picker=new CalendarPersonPicker(project,User,"Ham olay — Proje personeli seç");picker.SessionRevoked+=(_,_)=>NotifySessionRevoked();
        if(picker.ShowDialog(this)!=DialogResult.OK||picker.SelectedPerson is not{} person||IsDisposed)return;
        DataTable? rosters=null;if(!await RunOperationAsync(async ct=>rosters=await Service.RostersAsync(project,person.Id,person.Group,ct))||IsDisposed)return;
        if(rosters==null||rosters.Rows.Count==0){MessageBox.Show(this,"Kişinin güncel döneminde etkin proje/ekip ataması bulunamadı.","Ham olay aktarımı");return;}
        using var d=new AttendanceImportDialog(project,User,person,rosters,first,csv);await DialogAsync(d);
    }
    private async Task IgnoreAsync()
    {
        if(Row(events) is not{} e)return;var input=new Wire.AttendanceIgnore(project,Guid.NewGuid(),e.Field<Guid>("EventId"),((byte[])e["RecordVersion",DataRowVersion.Original]).ToArray(),"");
        using var d=new AttendanceDecisionDialog(User,input,"Muhasebe — Ham olayı yoksay",$"{e["AD"]} {e["SOYAD"]} · {e["SourceSystem"]}/{e["ExternalId"]}\n{e.Field<DateTime>("OccurredLocal"):dd.MM.yyyy HH:mm:ss.fffffff} · {e["Direction"]}\nKayıt silinmez veya yeniden etkinleştirilmez. Yanlış olayı yeni kimlikle düzeltebilirsiniz; bağlı olay için önce eşlemeyi kaldırın.");await DialogAsync(d);
    }
    private async Task ReleaseAsync()
    {
        if(Row(matches) is not{} m)return;var input=new Wire.AttendanceRelease(project,Guid.NewGuid(),m.Field<Guid>("MatchId"),((byte[])m["RecordVersion",DataRowVersion.Original]).ToArray(),((byte[])m["TimesheetVersion",DataRowVersion.Original]).ToArray(),"");
        using var d=new AttendanceDecisionDialog(User,input,"Muhasebe — Eşlemeyi kaldır",$"{m["AD"]} {m["SOYAD"]} · {m.Field<DateTime>("WorkDate"):dd.MM.yyyy}\nPuantajın giriş/çıkış saatleri, mola ve ücretlendirilecek dakikası temizlenecek. Eski değer/kararlar audit'te, olaylar ve eşleme geçmişi yerinde kalır. Olaylar yeni eşleme için serbest kalır. Onaylı/gönderilmiş kayıt önce yetkili hesapla yeniden açılmalı/iade edilmelidir.");
        if(await DialogAsync(d)&&target!=null&&target.Field<Guid>("TimesheetId")==m.Field<Guid>("TimesheetId")){targetChanged=true;RefreshEditState();UpdateNote();}
    }
}
