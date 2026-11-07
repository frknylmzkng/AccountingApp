using System.Data;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class UnplannedTimesheetDialog : CalendarEditDialog
{
    private sealed record Roster(DataRow Row):ITimesheetDraftChoice
    {
        public string DraftKey=>TimesheetDraftStore.RowKey(Row,"RosterAssignmentId","RosterVersion","EmploymentId");
        public override string ToString()=> $"{Row.Field<DateTime>("StartDate"):dd.MM.yyyy} – {(Row.IsNull("EndDate")?"Açık uçlu":Row.Field<DateTime>("EndDate").ToString("dd.MM.yyyy"))} · {(Row.IsNull("TeamName")?"Proje geneli":Row["TeamName"])}";
    }
    private readonly Guid project;private Guid id,command=Guid.NewGuid();
    private readonly bool edit;private readonly DataRow? old;private readonly ScopedPerson person;
    private readonly ComboBox roster=new(){Width=440,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly DateTimePicker day=new(){Format=DateTimePickerFormat.Short,Width=170,MinDate=new(1900,1,1),MaxDate=new(2199,12,31)};
    private readonly DateTimePicker start=Moment(),end=Moment();
    private readonly NumericUpDown pause=Number(),paid=Number();
    private readonly CheckBox paidKnown=new(){Text="Ücretlendirilecek dakika belirtildi",AutoSize=true};
    private static DateTimePicker Moment()=>new(){Width=280,Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",ShowCheckBox=true,MinDate=new(1900,1,1),MaxDate=new(2199,12,31)};
    private static NumericUpDown Number()=>new(){Minimum=0,Maximum=1440,Width=170};
    private static DateTime? Read(DateTimePicker p)=>p.Checked?DateTime.SpecifyKind(new DateTime(p.Value.Year,p.Value.Month,p.Value.Day,p.Value.Hour,p.Value.Minute,0),DateTimeKind.Unspecified):null;

    internal UnplannedTimesheetDialog(Guid project,UserSession user,ScopedPerson person,DataTable candidates,DateTime first,DataRow? old=null)
        :base(user,"Muhasebe — Vardiyasız gerçekleşen çalışma","Vardiyasız puantaj")
    {
        this.project=project;this.person=person;this.old=old;edit=old!=null;id=old?.Field<Guid>("TimesheetId")??Guid.NewGuid();
        ClientSize=new(680,720);
        Fields.Controls.Add(new Label{Text=person.Name+" · Vardiyasız çalışma",AutoSize=true});
        day.Value=old?.Field<DateTime>("WorkDate")??first;
        if(old==null)
        {
            foreach(DataRow r in candidates.Rows)roster.Items.Add(new Roster(r));
            roster.SelectedItem=roster.Items.Cast<Roster>().FirstOrDefault(r=>r.Row.Field<DateTime>("StartDate")<=day.Value.Date&&(r.Row.IsNull("EndDate")||r.Row.Field<DateTime>("EndDate")>=day.Value.Date));
            if(roster.SelectedIndex<0&&roster.Items.Count>0)roster.SelectedIndex=0;
            Field("Tarihli proje / ekip ataması (çalışmanın tamamını kapsamalı)",roster);
        }
        else Fields.Controls.Add(new Label{Text="Kişi, dönem, proje ataması ve iş günü bu kaydın değişmez kimliğidir. Başka gün / atama gerekiyorsa gerekçeli iptal ve yeni kayıt kullanın.",AutoSize=true});
        start.Value=edit&&!old!.IsNull("LocalStart")?old.Field<DateTime>("LocalStart"):day.Value.Date.AddHours(9);
        end.Value=edit&&!old!.IsNull("LocalEnd")?old.Field<DateTime>("LocalEnd"):day.Value.Date.AddHours(17);
        start.Checked=edit&&!old!.IsNull("LocalStart");end.Checked=edit&&!old!.IsNull("LocalEnd");
        pause.Value=old?.Field<int>("BreakMinutes")??0;paidKnown.Checked=edit&&!old!.IsNull("PayableMinutes");paid.Value=paidKnown.Checked?old!.Field<int>("PayableMinutes"):0;
        paidKnown.CheckedChanged+=(_,_)=>paid.Enabled=paidKnown.Checked;paid.Enabled=paidKnown.Checked;
        day.ValueChanged+=(_,_)=>{if(!start.Checked)start.Value=day.Value.Date.AddHours(9);if(!end.Checked)end.Value=day.Value.Date.AddHours(17);};
        Field("İş günü (gerçek girişin günü)",day);Field("Gerçek giriş (bilinmiyorsa işareti kaldırın)",start);Field("Gerçek çıkış (gece için ertesi günü seçin)",end);
        Field("Gerçek mola (dakika)",pause);Field("Ücretlendirilecek süre (tutar hesaplanmaz)",paidKnown);Field("Ücretlendirilecek dakika",paid);
        Fields.Controls.Add(new Label{Text="Vardiya oluşturulmaz veya yayınlanmaz. Aynı aralıkta planlı vardiya varsa vardiyalı puantajı kullanın. Eksik saatler taslakta saklanır; onaya gönderim için saatler, mola, süre ve çakışmalar düzeltilmelidir. İş günü girişin günü, çıkış anı hariçtir. Ücret / hakediş üretilmez.",AutoSize=true});
        Fields.SizeChanged+=(_,_)=>roster.Width=Math.Max(1,Math.Min(440,Fields.ClientSize.Width-48));AcceptTrackedChanges();
        PersistentDraft(project,"Unplanned",edit?"edit:"+id.ToString("D"):"new:"+person.Id.ToString("D"),
            TimesheetDraftStore.Fingerprint(person.Id,person.Group,person.Version,edit?TimesheetDraftStore.RowKey(old!,"TimesheetId","RecordVersion","RosterVersion","EmploymentId"):"new"),person.Name+" · Vardiyasız çalışma",
            ()=>JsonSerializer.SerializeToElement(new TimesheetDraftExtra(id,command)),extra=>
            {var x=TimesheetDraftExtra.Read(extra);if(edit&&x.Id!=id)throw new InvalidOperationException("Taslak puantaj hedefi farklı.");return ()=>{id=x.Id;command=x.Command;};},
            ()=>$"İş günü: {day.Value:dd.MM.yyyy} · Giriş: {Read(start):dd.MM.yyyy HH:mm} · Çıkış: {Read(end):dd.MM.yyyy HH:mm} · Mola: {pause.Value} dk · Ücret dk: {(paidKnown.Checked?paid.Value.ToString():"Belirtilmedi")}");
    }
    protected override void RefreshEditState(){base.RefreshEditState();day.Enabled=day.Enabled&&!edit;paid.Enabled=paid.Enabled&&paidKnown.Checked;}
    protected override object MakeRequest(string reason)
    {
        DataRow? selected=(roster.SelectedItem as Roster)?.Row;
        if(!edit&&selected==null)throw new ArgumentException("Geçerli tarihli proje / ekip ataması seçin.");
        return new Wire.UnplannedTimesheetWrite(project,command,id,edit?((byte[])old!["RecordVersion",DataRowVersion.Original]).ToArray():null,person.Id,person.Group,person.Version.ToArray(),
            edit?old!.Field<Guid>("EmploymentId"):selected!.Field<Guid>("EmploymentId"),edit?old!.Field<Guid>("RosterAssignmentId"):selected!.Field<Guid>("RosterAssignmentId"),
            ((byte[])(edit?old!["RosterVersion",DataRowVersion.Original]:selected!["RosterVersion",DataRowVersion.Original])).ToArray(),DateTime.SpecifyKind(day.Value.Date,DateTimeKind.Unspecified),Read(start),Read(end),(int)pause.Value,paidKnown.Checked?(int)paid.Value:null,
            edit?old!.Field<string>("SourceKey")!:"unplanned:"+id.ToString("N"),reason);
    }
}
