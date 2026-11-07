using System.Data;
namespace WinFormsApp2;

internal sealed class TimesheetForm : SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private readonly Guid project;private readonly bool enter,approve;
    private readonly DateTimePicker week=new(){Format=DateTimePickerFormat.Short,Width=150};private readonly CheckBox exceptionsOnly=new(){Text="Yalnız istisnalar",AutoSize=true};
    private readonly DataGridView rows=Grid("Timesheets"),assignments=Grid("TimesheetAssignments");
    private readonly Label summary=new(){Dock=DockStyle.Top,AutoSize=true,Padding=new(8)};
    private readonly Button approvePiece=Button("Seçili parçayı onayla…"),returnPiece=Button("Seçili parçayı iade et / yeniden aç…"),pieceHistory=Button("Parça karar geçmişi…");
    private readonly CheckBox segmentHistory=new(){Text="Kaldırılmış dağılımları da göster",AutoSize=true};
    private readonly Button allocations=Button("Proje / mola dağılımı…");
    private readonly DataGridView pieces=Grid("ProjectWorkSegments");
    private readonly Button rawEvents=Button("Ham giriş / çıkış olayları…");
    private readonly Button manualBatch=Button("Çok kişili manuel giriş…");
    private readonly Button drafts=Button("Kalıcı taslaklar / kurtarma…");
    private readonly Button unplanned=Button("Vardiyasız çalışma…");
    private readonly Button create=Button("Seçili atamaya puantaj…"),edit=Button("Puantajı düzenle…"),csv=Button("CSV içe al / önizle…"),send=Button("Onaya gönder…"),accept=Button("Onayla…"),sendBack=Button("İade et / yeniden aç…"),cancel=Button("İptal et…"),template=Button("CSV şablonunu kaydet…");
    private DataTable assignmentData=new();private DateTime? loadedWeek;private bool loading;
    private TimesheetService Service=>new(AppDatabase.ConnectionString,User);
    private DateTime First=>DateTime.SpecifyKind(week.Value.Date.AddDays(-(((int)week.Value.DayOfWeek+6)%7)),DateTimeKind.Unspecified);
    private static Button Button(string text)=>new(){Text=text,AutoSize=true};
    private static FlowLayoutPanel Bar(params Control[] controls){var p=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new(6)};p.Controls.AddRange(controls);return p;}
    private static DataRow? Row(DataGridView grid)=>grid.SelectedRows.Count==1&&grid.SelectedRows[0].DataBoundItem is DataRowView r?r.Row:null;
    private static DataGridView Grid(string gridName)
    {
        var grid=new DataGridView{Name=gridName,Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};RecordSelection.Configure(grid);
        grid.DataBindingComplete+=(_,_)=>
        {
            var names=new Dictionary<string,string>{{"EffectiveState","Etkin parça durumu"},{"DecisionState","Son tur kararı"},{"DecidedBy","Karar veren"},{"DecidedUtc","Karar UTC"},{"DecisionReason","Bu projenin gerekçesi"},{"UsesSegmentApproval","Bağımsız onay akışı"},{"AllocationReleasedUtc","Dağılım kaldırma UTC"},{"RoundClosedUtc","Tur kapanış UTC"},{"OwnPreparation","Kimlik/görev çakışması; onay verilemez"},{"NetMinutes","Projeye ait net dk"},{"SequenceNo","Parça sırası"},{"TeamName","Ekip"},{"HasProjectAllocation","Projelere dağıtıldı"},{"HasAttendanceMatch","Ham olay bağlı"},{"WorkKind","Çalışma türü"},{"AD","Ad"},{"SOYAD","Soyad"},{"WorkDate","İş günü"},{"ShiftName","Vardiya"},{"PlannedStart","Plan giriş"},{"PlannedEnd","Plan çıkış"},{"LocalStart","Gerçek giriş"},{"LocalEnd","Gerçek çıkış"},{"BreakMinutes","Gerçek mola dk"},{"PlannedBreak","Plan mola dk"},{"GrossMinutes","Gerçek brüt dk"},{"PlannedMinutes","Plan net dk"},{"ActualMinutes","Gerçek net dk"},{"PayableMinutes","Ücretlendirilecek dk"},{"Status","Durum"},{"Exception","İstisna"},{"CreatedBy","Hazırlayan"},{"EditedBy","Son düzenleyen"},{"DecidedBy","Karar veren"},{"SourceKey","Kaynak satır kimliği"},{"PolicyVersion","Süre politikası"},{"ShiftAssignmentId","CSV atama kimliği"}};
            foreach(DataGridViewColumn c in grid.Columns){if(names.TryGetValue(c.Name,out var name))c.HeaderText=name;if(c.ValueType==typeof(byte[])||(c.ValueType==typeof(Guid)&&c.Name!="ShiftAssignmentId")||c.Name is "StartUtc" or "EndUtc")c.Visible=false;if(gridName=="Timesheets"&&c.Name=="ActualMinutes")c.HeaderText="Ana puantaj net dk";if(gridName=="Timesheets"&&c.Name=="PayableMinutes")c.HeaderText="Ana puantaj ücret dk";if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name=="WorkDate"?"dd.MM.yyyy":"dd.MM.yyyy HH:mm";}
        };
        grid.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0)return;if(grid.Columns[e.ColumnIndex].Name=="WorkKind"){e.Value=e.Value?.ToString()=="Unplanned"?"Vardiyasız":"Vardiyalı";e.FormattingApplied=true;}
            if(grid.Columns[e.ColumnIndex].Name=="Status"){e.Value=e.Value?.ToString() switch{"Draft"=>"Taslak","Submitted"=>"Onay bekliyor","Approved"=>"Onaylı","Returned"=>"İade / yeniden açıldı","Cancelled"=>"İptal",var v=>v};e.FormattingApplied=true;}};return grid;
    }
    internal TimesheetForm(Guid project,UserSession user,bool enter,bool approve)
    {
        this.project=project;this.enter=enter;this.approve=approve;Session=user;PersistGridLayout=false;Text="Muhasebe — Haftalık puantaj ve amir onayı";ClientSize=new(1240,820);week.Value=TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,DailyClosingService.BusinessZone).Date;
        var tabs=new TabControl{Dock=DockStyle.Fill};var work=new TabPage("Puantaj / istisnalar");work.Controls.Add(rows);work.Controls.Add(Bar(allocations,rawEvents,unplanned,edit,send,accept,sendBack,cancel,exceptionsOnly));var plans=new TabPage("Yayınlanmış vardiya atamaları");plans.Controls.Add(assignments);plans.Controls.Add(Bar(create,manualBatch,csv,template));var parts=new TabPage("Projeye ait zaman parçaları");parts.Controls.Add(pieces);parts.Controls.Add(Bar(approvePiece,returnPiece,pieceHistory,segmentHistory,new Label{AutoSize=true,Text="Yalnız bu projeye ait parçanın kararını verirsiniz. Bir iade turu kapatır; diğer kararlar geçmişte korunur. Eski toplu kayıtta önce iade gerekir."}));tabs.TabPages.AddRange([work,plans,parts]);Controls.Add(tabs);Controls.Add(summary);
        var refresh=Button("Haftayı yükle");var previous=Button("← Önceki hafta");var next=Button("Sonraki hafta →");var close=Button("Kapat");Controls.Add(Bar(new Label{Text="Hafta",AutoSize=true},week,previous,next,refresh,drafts,close));
        refresh.Click+=async(_,_)=>await ReloadAsync();previous.Click+=async(_,_)=>{week.Value=First.AddDays(-7);await ReloadAsync();};next.Click+=async(_,_)=>{week.Value=First.AddDays(7);await ReloadAsync();};close.Click+=(_,_)=>Close();RefreshAction=ReloadAsync;
        drafts.Click+=async(_,_)=>{using var d=new TimesheetDraftsForm(project,User);await DialogAsync(d);};
        create.Click+=async(_,_)=>{if(Row(assignments) is not{} r)return;using var d=new TimesheetEditDialog(project,User,r,false);await DialogAsync(d);};edit.Click+=async(_,_)=>
        {
            if(Row(rows) is not{} r)return;
            using SessionForm d=r.IsNull("ShiftAssignmentId")?new UnplannedTimesheetDialog(project,User,new ScopedPerson(r.Field<Guid>("PersonId"),(AccountingApp.Contracts.PersonnelGroup)r.Field<int>("PersonnelGroup"),((byte[])r["PersonVersion",DataRowVersion.Original]).ToArray(),r["AD"]+" "+r["SOYAD"]),new DataTable(),r.Field<DateTime>("WorkDate"),r):new TimesheetEditDialog(project,User,r,true);
            await DialogAsync(d);
        };
        unplanned.Click+=async(_,_)=>await UnplannedAsync();
        allocations.Click+=async(_,_)=>{if(Row(rows) is not{} r)return;using var d=new WorkAllocationForm(project,User,r,enter);await DialogAsync(d);};
        rawEvents.Click+=async(_,_)=>{if(loadedWeek is not{} first)return;using var d=new AttendanceEventsForm(project,User,first,enter,Row(rows));await DialogAsync(d);};
        manualBatch.Click+=async(_,_)=>{if(loadedWeek is not{} first)return;using var d=new ManualTimesheetBatchDialog(project,User,first,assignmentData);await DialogAsync(d);};
        csv.Click+=async(_,_)=>{using var d=new TimesheetCsvDialog(project,User,assignmentData);await DialogAsync(d);};template.Click+=async(_,_)=>await ExportTemplateAsync();
        approvePiece.Click+=async(_,_)=>await PieceDecisionAsync("Approved");returnPiece.Click+=async(_,_)=>await PieceDecisionAsync("Returned");pieceHistory.Click+=async(_,_)=>{if(Row(pieces) is not{} r)return;using var d=new SegmentDecisionHistoryForm(project,r.Field<Guid>("SegmentId"),User);await DialogAsync(d);};segmentHistory.CheckedChanged+=async(_,_)=>await ReloadAsync();pieces.SelectionChanged+=(_,_)=>RefreshEditState();
        send.Click+=async(_,_)=>await StateAsync("Submitted");accept.Click+=async(_,_)=>await StateAsync("Approved");sendBack.Click+=async(_,_)=>await StateAsync("Returned");cancel.Click+=async(_,_)=>await StateAsync("Cancelled");
        rows.SelectionChanged+=(_,_)=>RefreshEditState();assignments.SelectionChanged+=(_,_)=>RefreshEditState();week.ValueChanged+=(_,_)=>RefreshEditState();exceptionsOnly.CheckedChanged+=(_,_)=>ApplyFilter();ClientSizeChanged+=(_,_)=>summary.MaximumSize=new(Math.Max(1,ClientSize.Width-24),0);
        Load+=async(_,_)=>await ReloadAsync();SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
    protected override void OnLoad(EventArgs e){base.OnLoad(e);MinimumSize=LogicalToDeviceUnits(new Size(820,560));}
    protected override void RefreshEditState()
    {
        manualBatch.Enabled=!IsOperationRunning&&!loading&&enter&&loadedWeek==First;drafts.Enabled=!IsOperationRunning&&enter;bool ready=!IsOperationRunning&&!loading&&loadedWeek==First;allocations.Enabled=ready&&Row(rows)!=null;rawEvents.Enabled=ready;unplanned.Enabled=ready&&enter;var r=Row(rows);string? state=r?.Field<string>("Status");create.Enabled=ready&&enter&&Row(assignments) is{} a&&a.IsNull("TimesheetId");send.Enabled=ready&&enter&&state is "Draft" or "Returned";edit.Enabled=cancel.Enabled=send.Enabled&&r!=null&&r.IsNull("ActiveMatchId")&&r.IsNull("ActiveAllocationId");accept.Enabled=ready&&approve&&r!=null&&r.IsNull("ActiveAllocationId")&&!r.Field<bool>("OwnPreparation")&&state=="Submitted";sendBack.Enabled=ready&&approve&&r!=null&&r.IsNull("ActiveAllocationId")&&state is "Submitted" or "Approved";csv.Enabled=ready&&enter;template.Enabled=ready;
        var p=Row(pieces);bool live=ready&&approve&&p!=null&&p.IsNull("AllocationReleasedUtc")&&p.Field<string>("Status") is "Submitted" or "Approved";
        approvePiece.Enabled=live&&p!=null&&p.Field<bool>("UsesSegmentApproval")&&!p.IsNull("RoundId")&&p.IsNull("RoundClosedUtc")&&p.Field<string>("DecisionState")=="Pending"&&!p.Field<bool>("OwnPreparation")&&p.Field<string>("Status")=="Submitted";
        returnPiece.Enabled=live&&p!=null&&(!p.Field<bool>("UsesSegmentApproval")||(!p.IsNull("RoundId")&&p.IsNull("RoundClosedUtc")&&p.Field<string>("DecisionState") is "Pending" or "Approved"));pieceHistory.Enabled=ready&&p!=null;
    }
    private void ApplyFilter(){if(rows.DataSource is DataTable data)data.DefaultView.RowFilter=exceptionsOnly.Checked?"Exception <> '' AND Status <> 'Cancelled'":"";rows.ClearSelection();}
    private async Task ReloadAsync()
    {
        if(loading)return;var first=First;loading=true;try{await RunOperationAsync(async ct=>{var data=await Service.WeekAsync(project,first,ct,segmentHistory.Checked);if(IsDisposed)return;assignmentData=data.Assignments;rows.DataSource=data.Rows;assignments.DataSource=data.Assignments;pieces.DataSource=data.Segments;pieces.ClearSelection();loadedWeek=first;ApplyFilter();assignments.ClearSelection();var s=data.Summary.Rows[0];int missing=data.Assignments.AsEnumerable().Count(r=>r.IsNull("TimesheetId"));summary.Text=$"Yüklenen hafta: {first:dd.MM.yyyy} – {first.AddDays(6):dd.MM.yyyy} · İş günü vardiyalıda planın, vardiyasızda gerçek girişin başlangıç günüdür; gece çalışması tek kayıttır.\nKaynak puantaj: {s["RecordCount"]} · Bu projeye ait etkin parça: {data.Segments.AsEnumerable().Count(p=>p.IsNull("AllocationReleasedUtc"))} · Puantajı olmayan atama: {missing} · Onay bekleyen: {s["AwaitingApproval"]} · Onaylı proje net: {s["ApprovedActualMinutes"]} dk · Onaylı ücretlendirilecek: {s["ApprovedPayableMinutes"]} dk. Ücret / hakediş üretilmez. Son okuma {DateTime.Now:HH:mm:ss}";});}finally{loading=false;if(!IsDisposed)RefreshEditState();}
    }
    private async Task UnplannedAsync()
    {
        if(loadedWeek is not{} first||!enter||IsOperationRunning)return;
        using var picker=new CalendarPersonPicker(project,User,"Puantaj — Proje personeli seç");picker.SessionRevoked+=(_,_)=>NotifySessionRevoked();
        if(picker.ShowDialog(this)!=DialogResult.OK||picker.SelectedPerson is not{} person||IsDisposed)return;
        DataTable? candidates=null;
        if(!await RunOperationAsync(async ct=>candidates=await Service.RostersAsync(project,person.Id,person.Group,ct))||IsDisposed)return;
        if(candidates==null||candidates.Rows.Count==0){MessageBox.Show(this,"Kişinin güncel döneminde etkin proje / ekip ataması bulunamadı. Önce tarihli atamayı düzenleyin.","Vardiyasız çalışma");return;}
        using var dialog=new UnplannedTimesheetDialog(project,User,person,candidates,first);await DialogAsync(dialog);
    }
    private async Task DialogAsync(SessionForm dialog){dialog.SessionRevoked+=(_,_)=>NotifySessionRevoked();dialog.ShowDialog(this);if(!IsDisposed)await ReloadAsync();}
    private async Task PieceDecisionAsync(string state){if(Row(pieces) is not{} row)return;using var d=new SegmentApprovalDialog(project,User,row,state);await DialogAsync(d);}
    private async Task StateAsync(string state){if(Row(rows) is not{} row)return;using var d=new TimesheetStateDialog(project,User,row,state);await DialogAsync(d);}
    private async Task ExportTemplateAsync()
    {
        if(loadedWeek is not{} first)return;using var save=new SaveFileDialog{Filter="CSV dosyası|*.csv",FileName=$"puantaj-sablonu-{first:yyyyMMdd}.csv"};if(save.ShowDialog(this)!=DialogResult.OK)return;
        await RunChangeOperationAsync(async ct=>{var text=await Service.CsvTemplateAsync(project,first,ct);await File.WriteAllTextAsync(save.FileName,text,new System.Text.UTF8Encoding(true),ct);},"CSV şablonu kaydedildi.");
    }
}
