using System.Data;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record ManualTimesheetDraftRow(Guid Id,string Caption,DateTime WorkDate,Wire.TimesheetWrite? Planned,Wire.UnplannedTimesheetWrite? Unplanned,bool Reviewed);
internal sealed record ManualTimesheetValues(DateTime? Start,DateTime? End,int BreakMinutes,int? PayableMinutes);

internal sealed class ManualTimesheetBatchDialog:CalendarEditDialog
{
    private readonly Guid project;
    private Guid command=Guid.NewGuid();
    private readonly DateTime first;
    private readonly DataTable source;
    private readonly List<ManualTimesheetDraftRow> entries=[];
    private readonly DataGridView candidates=Grid(),preview=Grid();
    private readonly TextBox changeToken=new(){Visible=false};
    private readonly Label summary=new(){AutoSize=true};
    private readonly ComboBox dayFilter=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    private readonly Panel workspace=new(){Width=900,Height=450};
    private static DataGridView Grid()
    {var g=new DataGridView{Dock=DockStyle.Fill,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};RecordSelection.Configure(g);g.MultiSelect=true;g.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in g.Columns)if(c.ValueType==typeof(Guid))c.Visible=false;};return g;}
    private static Guid[] Selected(DataGridView g)=>g.SelectedRows.Cast<DataGridViewRow>().Where(r=>r.DataBoundItem is DataRowView).Select(r=>((DataRowView)r.DataBoundItem!).Row.Field<Guid>("Id")).ToArray();
    private static Button Button(string text)=>new(){Text=text,AutoSize=true};
    internal ManualTimesheetBatchDialog(Guid project,UserSession user,DateTime first,DataTable assignments):base(user,"Muhasebe — Çok kişili manuel puantaj","Toplu manuel puantaj")
    {
        this.project=project;this.first=first;source=assignments.Copy();ClientSize=new(1120,820);
        Fields.Controls.Add(new Label{AutoSize=true,Text=$"{first:dd.MM.yyyy} – {first.AddDays(6):dd.MM.yyyy} · Tek proje, en çok 200 yeni satır. Vardiya atamalarını veya vardiyasız kişileri önizlemeye ekleyin. Her satırın gerçekleşen alanlarını açıkça gözden geçirin. Plan saatleri gerçekleşen sayılmaz. Kaydet bütün paketi tek transaction ile taslak olarak saklar; onay/ödeme üretmez."});
        var tabs=new TabControl{Dock=DockStyle.Fill};var a=new TabPage("Yayınlanmış atamalardan ekle");var b=new TabPage("Kaydedilecek satırlar / önizleme");a.Controls.Add(candidates);b.Controls.Add(preview);tabs.TabPages.AddRange([a,b]);workspace.Controls.Add(tabs);
        var bar=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true};
        Button add=Button("Seçili atamaları ekle"),unplanned=Button("Vardiyasız kişi ekle…"),edit=Button("Seçili satırları gözden geçir…"),remove=Button("Seçili satırları çıkar");
        dayFilter.Items.Add("Tüm hafta");for(int i=0;i<7;i++)dayFilter.Items.Add(first.AddDays(i).ToString("dd.MM.yyyy"));dayFilter.SelectedIndex=0;
        bar.Controls.AddRange([new Label{AutoSize=true,Text="Atama günü"},dayFilter,add,unplanned,edit,remove]);workspace.Controls.Add(bar);Fields.Controls.Add(workspace);Fields.Controls.Add(summary);Fields.Controls.Add(changeToken);TrackChanges("Toplu manuel puantaj",changeToken);
        Fields.SizeChanged+=(_,_)=>{workspace.Width=Math.Max(1,Fields.ClientSize.Width-48);workspace.Height=Math.Max(320,Fields.ClientSize.Height-170);};
        dayFilter.SelectedIndexChanged+=(_,_)=>FillCandidates();
        add.Click+=(_,_)=>
        {
            try
            {
                var ids=Selected(candidates);if(ids.Length==0)throw new ArgumentException("Ctrl/Shift ile atamaları seçin.");if(entries.Count+ids.Length>200)throw new ArgumentException("Paket en fazla 200 satır olabilir.");
                if(ids.Any(id=>entries.Any(e=>e.Planned?.ShiftAssignmentId==id)))throw new ArgumentException("Seçili atamalardan biri zaten önizlemede. Seçimi değiştirin.");
                foreach(Guid assignment in ids)
                {
                    var r=source.AsEnumerable().Single(r=>r.Field<Guid>("ShiftAssignmentId")==assignment);if(!r.IsNull("TimesheetId"))throw new InvalidOperationException("Bu atamada puantaj var; mevcut kaydı listeden düzenleyin.");Guid id=Guid.NewGuid();
                    entries.Add(new(id,r["AD"]+" "+r["SOYAD"]+" · "+r["ShiftName"],r.Field<DateTime>("PlannedStart").Date,new(id,null,assignment,((byte[])r["AssignmentVersion"]).ToArray(),((byte[])r["ShiftVersion"]).ToArray(),"bulk:"+id.ToString("N"),null,null,0,null),null,false));
                }
                Changed();tabs.SelectedTab=b;
            }
            catch(Exception ex)when(ex is ArgumentException or InvalidOperationException){MessageBox.Show(this,ex.Message,"Satır eklenemedi");}
        };
        unplanned.Click+=async(_,_)=>{await AddUnplannedAsync();tabs.SelectedTab=b;};
        edit.Click+=(_,_)=>EditSelected();remove.Click+=(_,_)=>{var ids=Selected(preview);entries.RemoveAll(x=>ids.Contains(x.Id));Changed();};
        preview.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)EditSelected();};FillCandidates();FillPreview();AcceptTrackedChanges();
        PersistentDraft(project,"ManualBatch",first.ToString("yyyyMMdd"),TimesheetDraftStore.Fingerprint(first,source.AsEnumerable().Select(r=>TimesheetDraftStore.RowKey(r,"ShiftAssignmentId","AssignmentVersion","ShiftVersion","TimesheetId")).Order().ToArray()),$"Toplu manuel puantaj · {first:dd.MM.yyyy}",
            ()=>JsonSerializer.SerializeToElement(new TimesheetDraftExtra(command,command,ManualRows:entries.ToArray())),extra=>
            {
                var x=TimesheetDraftExtra.Read(extra);var rows=x.ManualRows??[];
                ValidateRows(rows);
                foreach(var e in rows.Where(e=>e.Planned!=null))
                {var p=e.Planned!;var r=source.AsEnumerable().SingleOrDefault(r=>r.Field<Guid>("ShiftAssignmentId")==p.ShiftAssignmentId);if(r==null||!r.IsNull("TimesheetId")||!((byte[])r["AssignmentVersion"]).SequenceEqual(p.AssignmentVersion)||!((byte[])r["ShiftVersion"]).SequenceEqual(p.ShiftVersion))throw new InvalidOperationException("Taslağın vardiya ataması değişmiş; güncel sürüme taşınmadı.");}
                return ()=>{command=x.Command;entries.Clear();entries.AddRange(rows);FillCandidates();FillPreview();};
            },()=>$"Toplu manuel giriş · {entries.Count} satır · {entries.Count(x=>x.Reviewed)} satır gözden geçirildi. Eksik saatler bilinçli olarak taslak bırakılabilir.",ValidateRecoveredUnplannedAsync);
    }
    private static void ValidateRows(ManualTimesheetDraftRow[] rows)
    {
        if(rows.Length>200||rows.Any(x=>x==null||x.Id==Guid.Empty||(x.Planned==null)==(x.Unplanned==null)||x.Id!=(x.Planned?.TimesheetId??x.Unplanned!.TimesheetId))||rows.Select(x=>x.Id).Distinct().Count()!=rows.Length)throw new ArgumentException("Toplu taslak satır kimlikleri geçersiz.");
    }
    private async Task ValidateRecoveredUnplannedAsync(JsonElement extra,CancellationToken ct)
    {
        var rows=TimesheetDraftExtra.Read(extra).ManualRows??[];ValidateRows(rows);
        foreach(var group in rows.Where(e=>e.Unplanned!=null).GroupBy(e=>(e.Unplanned!.PersonId,e.Unplanned.Group)))
        {
            var current=await new TimesheetService(AppDatabase.ConnectionString,User).RostersAsync(project,group.Key.PersonId,group.Key.Group,ct);
            foreach(var e in group)
            {var u=e.Unplanned!;var r=current.AsEnumerable().SingleOrDefault(r=>r.Field<Guid>("RosterAssignmentId")==u.RosterAssignmentId&&r.Field<Guid>("EmploymentId")==u.EmploymentId);if(r==null||!((byte[])r["PersonVersion"]).SequenceEqual(u.PersonVersion)||!((byte[])r["RosterVersion"]).SequenceEqual(u.RosterVersion))throw new InvalidOperationException("Taslağın vardiyasız kişi/dönem/atama sürümü değişmiş; eski seçim kurtarılmadı.");}
        }
    }
    private void FillCandidates()
    {
        var data=new DataTable();data.Columns.Add("Id",typeof(Guid));data.Columns.Add("Personel");data.Columns.Add("Vardiya");data.Columns.Add("Plan giriş",typeof(DateTime));data.Columns.Add("Plan çıkış",typeof(DateTime));
        foreach(DataRow r in source.Rows)if(r.IsNull("TimesheetId")&&!entries.Any(e=>e.Planned?.ShiftAssignmentId==r.Field<Guid>("ShiftAssignmentId"))&&(dayFilter.SelectedIndex<=0||r.Field<DateTime>("PlannedStart").Date==first.AddDays(dayFilter.SelectedIndex-1)))data.Rows.Add(r["ShiftAssignmentId"],r["AD"]+" "+r["SOYAD"],r["ShiftName"],r["PlannedStart"],r["PlannedEnd"]);
        candidates.DataSource=data;foreach(DataGridViewColumn c in candidates.Columns)if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format="dd.MM.yyyy HH:mm";candidates.ClearSelection();
    }
    private void Changed(){changeToken.Text=Guid.NewGuid().ToString("N");MarkTrackedChanges();FillCandidates();FillPreview();}
    private void FillPreview()
    {
        var data=new DataTable();data.Columns.Add("Id",typeof(Guid));data.Columns.Add("Paket satırı");data.Columns.Add("Personel / kaynak");data.Columns.Add("İş günü",typeof(DateTime));data.Columns.Add("Tür");data.Columns.Add("Gerçek giriş",typeof(DateTime));data.Columns.Add("Gerçek çıkış",typeof(DateTime));data.Columns.Add("Mola dk",typeof(int));data.Columns.Add("Ücret dk",typeof(int));data.Columns.Add("Gözden geçirildi",typeof(bool));data.Columns.Add("Bilgi");
        int plannedNo=0,unplannedNo=0;
        foreach(var e in entries)
        {
            var v=Values(e);string note=e.Unplanned!=null&&v.Start!=null&&v.Start.Value.Date!=e.WorkDate?"Giriş günü iş günüyle farklı; kayıt reddedilir":v.Start==null||v.End==null?"Eksik saat; taslak":v.End<=v.Start?"Saat sırası hatalı":(v.End-v.Start).Value.TotalMinutes<=v.BreakMinutes?"Mola net süreyi tüketiyor":(v.End-v.Start).Value.TotalMinutes>1440?"Çalışma 24 saatten uzun":v.PayableMinutes==null?"Ücret dakikası belirtilmedi":v.PayableMinutes>(v.End-v.Start).Value.TotalMinutes-v.BreakMinutes?"Ücret dakikası net süreyi aşıyor":"Onaya gönderimde çakışma/istisna denetlenir";
            data.Rows.Add(e.Id,e.Planned!=null?"Vardiyalı #"+(++plannedNo):"Vardiyasız #"+(++unplannedNo),e.Caption,e.WorkDate,e.Planned!=null?"Vardiyalı":"Vardiyasız",(object?)v.Start??DBNull.Value,(object?)v.End??DBNull.Value,v.BreakMinutes,(object?)v.PayableMinutes??DBNull.Value,e.Reviewed,note);
        }
        preview.DataSource=data;foreach(DataGridViewColumn c in preview.Columns)if(c.ValueType==typeof(DateTime))c.DefaultCellStyle.Format=c.Name=="İş günü"?"dd.MM.yyyy":"dd.MM.yyyy HH:mm";preview.ClearSelection();
        summary.Text=$"Önizleme: {entries.Count}/200 satır · Gözden geçirilen {entries.Count(x=>x.Reviewed)} · Vardiyasız {entries.Count(x=>x.Unplanned!=null)}. Ctrl/Shift ile çoklu seçim; yalnız aynı iş gününe ortak saat uygulayın. Kaydet tek gerekçeyle bütün paketi saklar; bir satır reddedilirse hiçbir satır kaydedilmez.";
        summary.MaximumSize=new(Math.Max(1,Fields.ClientSize.Width-48),0);
    }
    private static ManualTimesheetValues Values(ManualTimesheetDraftRow e)=>e.Planned is{} p?new(p.Start,p.End,p.BreakMinutes,p.PayableMinutes):new(e.Unplanned!.Start,e.Unplanned.End,e.Unplanned.BreakMinutes,e.Unplanned.PayableMinutes);
    private void EditSelected()
    {
        var ids=Selected(preview);var selected=entries.Where(e=>ids.Contains(e.Id)).ToArray();if(selected.Length==0)return;
        if(selected.Select(x=>x.WorkDate).Distinct().Count()!=1){MessageBox.Show(this,"Ortak saat için yalnız aynı iş günündeki satırları seçin; diğer günleri ayrı düzenleyin.");return;}
        using var d=new ManualTimesheetValuesDialog(User,selected[0].WorkDate,selected.Length==1?Values(selected[0]):new(null,null,0,null),selected.Length);
        d.SessionRevoked+=(_,_)=>NotifySessionRevoked();if(d.ShowDialog(this)!=DialogResult.OK||d.Result==null)return;
        var v=d.Result;for(int i=0;i<entries.Count;i++)if(ids.Contains(entries[i].Id)){var e=entries[i];entries[i]=e with{Reviewed=true,Planned=e.Planned is{} p?p with{Start=v.Start,End=v.End,BreakMinutes=v.BreakMinutes,PayableMinutes=v.PayableMinutes}:null,Unplanned=e.Unplanned is{} u?u with{Start=v.Start,End=v.End,BreakMinutes=v.BreakMinutes,PayableMinutes=v.PayableMinutes}:null};}Changed();
    }
    private async Task AddUnplannedAsync()
    {
        if(entries.Count>=200){MessageBox.Show(this,"Paket en fazla 200 satır olabilir.");return;}
        using var picker=new CalendarPersonPicker(project,User,"Toplu manuel giriş — Proje personeli seç");picker.SessionRevoked+=(_,_)=>NotifySessionRevoked();if(picker.ShowDialog(this)!=DialogResult.OK||picker.SelectedPerson==null)return;var person=picker.SelectedPerson;
        DataTable? rosters=null;if(!await RunOperationAsync(async ct=>rosters=await new TimesheetService(AppDatabase.ConnectionString,User).RostersAsync(project,person.Id,person.Group,ct))||IsDisposed||rosters==null)return;
        using var target=new ManualTimesheetRosterDialog(User,person,rosters,first);target.SessionRevoked+=(_,_)=>NotifySessionRevoked();if(target.ShowDialog(this)!=DialogResult.OK||target.Selected==null)return;
        if(entries.Any(e=>e.Unplanned?.PersonId==person.Id&&e.WorkDate==target.Day)){MessageBox.Show(this,"Bu kişi/iş günü önizlemede zaten var.");return;}
        var r=target.Selected;if(!((byte[])r["PersonVersion"]).SequenceEqual(person.Version)){MessageBox.Show(this,"Personel seçimden sonra değişmiş; yeniden seçin.");return;}
        Guid id=Guid.NewGuid();var u=new Wire.UnplannedTimesheetWrite(project,command,id,null,person.Id,person.Group,person.Version.ToArray(),r.Field<Guid>("EmploymentId"),r.Field<Guid>("RosterAssignmentId"),((byte[])r["RosterVersion"]).ToArray(),target.Day,null,null,0,null,"bulk-unplanned:"+id.ToString("N"),"");
        entries.Add(new(id,person.Name,target.Day,null,u,false));Changed();
    }
    protected override object MakeRequest(string reason)
    {
        ValidateRows(entries.ToArray());if(entries.Count==0||entries.Any(e=>!e.Reviewed))throw new ArgumentException("En az bir satır ekleyin ve tüm satırların gerçekleşen alanlarını Gözden geçir ile açıkça kabul edin.");
        return new Wire.ManualTimesheetBatch(project,command,entries.Where(e=>e.Planned!=null).Select(e=>e.Planned!).ToArray(),entries.Where(e=>e.Unplanned!=null).Select(e=>e.Unplanned! with{CommandId=command,Reason=reason}).ToArray(),reason);
    }
}
