using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class AttendanceImportDialog : CalendarEditDialog
{
    private sealed record Roster(DataRow Row){public override string ToString()=>$"{Row.Field<DateTime>("StartDate"):dd.MM.yyyy} – {(Row.IsNull("EndDate")?"Açık":Row.Field<DateTime>("EndDate").ToString("dd.MM.yyyy"))} · {Row["TeamName"]}";}
    private sealed record Direction(string Code,string Text){public override string ToString()=>Text;}
    private readonly Guid project,command=Guid.NewGuid(),eventId=Guid.NewGuid();private readonly ScopedPerson person;private readonly bool csv;
    private readonly ComboBox roster=new(){Width=430,DropDownStyle=ComboBoxStyle.DropDownList},direction=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly TextBox source=new(){Width=340,MaxLength=50},key=new(){Width=340,MaxLength=100},csvToken=new(){Visible=false};
    private readonly DateTimePicker moment=new(){Width=300,Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm:ss",MinDate=new(1900,1,1),MaxDate=new(2199,12,31)};
    private readonly DataGridView preview=new(){Width=570,Height=230,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};private Wire.AttendanceEventRow[]? csvRows;
    internal AttendanceImportDialog(Guid project,UserSession user,ScopedPerson person,DataTable rosters,DateTime first,bool csv):base(user,csv?"Muhasebe — Ham olay CSV önizlemesi":"Muhasebe — Tek ham giriş / çıkış","Ham olay aktarımı")
    {
        this.project=project;this.person=person;this.csv=csv;ClientSize=new(740,760);source.Text=csv?"device-csv":"manual";key.Text=eventId.ToString("N");moment.Value=first.Date.AddHours(9);
        Fields.Controls.Add(new Label{Text=person.Name+" · Kaynak kişi/dönem/atama kayıttan sonra taşınamaz.",AutoSize=true});
        foreach(DataRow row in rosters.Rows)roster.Items.Add(new Roster(row));roster.SelectedItem=roster.Items.Cast<Roster>().FirstOrDefault(r=>r.Row.Field<DateTime>("StartDate")<=first&&(r.Row.IsNull("EndDate")||r.Row.Field<DateTime>("EndDate")>=first));if(roster.SelectedIndex<0&&roster.Items.Count>0)roster.SelectedIndex=0;
        Field("Tarihli proje / ekip ataması",roster);Field("Kaynak sistem adı (tüm projelerde aynı kaynak için sabit, harf duyarlı)",source);
        Fields.Controls.Add(new Label{Text="Yön açıkça In veya Out olmalı; olay sırasından tahmin edilmez. Ham kayıtlar silinmez/değiştirilmez. Kaynak + satır kimliği tekrarında aynı içerik çoğalmaz; farklı içerik reddedilir. Yanlış olay gerekçeli yoksayılıp yeni kimlikle düzeltilir.",AutoSize=true});
        if(csv)
        {
            Fields.Controls.Add(csvToken);TrackChanges("Ham olay aktarımı",csvToken);RecordSelection.Configure(preview);
            Fields.Controls.Add(new Label{Text="UTF-8, noktalı virgül, en çok 200 satır / 1 MB. Aynı dosya seçilen tek kişi ve proje atamasına aittir. Başlık: ExternalId;Direction;OccurredLocal\nZaman: yyyy-MM-dd HH:mm, yyyy-MM-dd HH:mm:ss veya 7 basamağa kadar kesir. İstanbul yerel saati; Z/offset kabul edilmez.",AutoSize=true});
            var choose=new Button{Text="CSV seç / önizle…",AutoSize=true};Field("Dosya",choose);Fields.Controls.Add(preview);
            Fields.SizeChanged+=(_,_)=>{preview.Width=Math.Max(1,Fields.ClientSize.Width-48);roster.Width=Math.Max(1,Math.Min(430,Fields.ClientSize.Width-48));};
            choose.Click+=(_,_)=>{using var d=new OpenFileDialog{Filter="CSV dosyası|*.csv",CheckFileExists=true};if(d.ShowDialog(this)!=DialogResult.OK)return;try{LoadCsv(d.FileName);}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException or MalformedLineException or FormatException){csvRows=null;preview.DataSource=null;MessageBox.Show(this,ex.Message,"Ham olay CSV okunamadı");}};
        }
        else
        {
            direction.Items.AddRange([new Direction("In","Giriş (In)"),new Direction("Out","Çıkış (Out)")]);
            Field("Kaynak olay / satır kimliği (aynı olay için sabit)",key);Field("Olay yönü (zorunlu)",direction);Field("Olayın gerçek yerel zamanı (İstanbul)",moment);
        }
        AcceptTrackedChanges();
    }
    private void LoadCsv(string path)
    {
        if(new FileInfo(path).Length>1024*1024)throw new ArgumentException("CSV 1 MB sınırını aşıyor.");
        using var p=new TextFieldParser(path,new UTF8Encoding(false,true),detectEncoding:false){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=true};p.SetDelimiters(";");var header=p.ReadFields()??[];if(header.Length>0)header[0]=header[0].TrimStart('\uFEFF');
        if(!header.SequenceEqual(new[]{"ExternalId","Direction","OccurredLocal"}))throw new ArgumentException("CSV başlıkları / sırası geçersiz.");
        var rows=new List<Wire.AttendanceEventRow>();var seen=new HashSet<string>(StringComparer.Ordinal);var table=new DataTable();table.Columns.Add("Kaynak kimliği");table.Columns.Add("Yön");table.Columns.Add("Ham yerel zaman",typeof(DateTime));
        while(!p.EndOfData)
        {
            var fields=p.ReadFields()??[];int line=rows.Count+2;if(fields.Length!=3||rows.Count>=200)throw new ArgumentException($"Satır {line}: 3 alan ve en çok 200 satır gerekiyor.");
            if(fields[0].Length is <1 or >100||fields[0].Any(char.IsControl)||!seen.Add(fields[0])||fields[1] is not ("In" or "Out"))throw new ArgumentException($"Satır {line}: kaynak kimliği / açık In-Out yönü geçersiz veya tekrarlı.");
            if(!DateTime.TryParseExact(fields[2],["yyyy-MM-dd HH:mm","yyyy-MM-dd HH:mm:ss","yyyy-MM-dd HH:mm:ss.FFFFFFF"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var time)||time.Year is <1900 or >2199)throw new ArgumentException($"Satır {line}: yerel zaman biçimi geçersiz.");
            time=DateTime.SpecifyKind(time,DateTimeKind.Unspecified);rows.Add(new(Guid.NewGuid(),fields[0],fields[1],time));table.Rows.Add(fields[0],fields[1]=="In"?"Giriş":"Çıkış",time);
        }
        if(rows.Count==0)throw new ArgumentException("CSV boş.");csvRows=rows.ToArray();preview.DataSource=table;preview.Columns["Ham yerel zaman"]!.DefaultCellStyle.Format="dd.MM.yyyy HH:mm:ss.fffffff";csvToken.Text=Guid.NewGuid().ToString("N");MarkTrackedChanges();
    }
    protected override object MakeRequest(string reason)
    {
        var r=(roster.SelectedItem as Roster)?.Row??throw new ArgumentException("Tarihli proje ataması seçin.");
        var rows=csv?csvRows??throw new ArgumentException("Önce CSV önizlemesini hazırlayın."):[new Wire.AttendanceEventRow(eventId,key.Text,(direction.SelectedItem as Direction)?.Code??throw new ArgumentException("Giriş / çıkış yönünü seçin."),DateTime.SpecifyKind(moment.Value,DateTimeKind.Unspecified))];
        return new Wire.AttendanceImport(project,command,person.Id,person.Group,person.Version.ToArray(),r.Field<Guid>("EmploymentId"),r.Field<Guid>("RosterAssignmentId"),((byte[])r["RosterVersion"]).ToArray(),source.Text,rows,reason);
    }
}

internal sealed class AttendanceMatchDialog : CalendarEditDialog
{
    private sealed record Event(DataRow Row){public override string ToString()=>$"{Row.Field<DateTime>("OccurredLocal"):dd.MM.yyyy HH:mm:ss.fffffff} · {Row["SourceSystem"]}/{Row["ExternalId"]}";}
    private readonly Guid project,command=Guid.NewGuid(),match=Guid.NewGuid();private readonly DataRow target;
    private readonly ComboBox entry=new(){Width=570,DropDownStyle=ComboBoxStyle.DropDownList},exit=new(){Width=570,DropDownStyle=ComboBoxStyle.DropDownList};
    private readonly NumericUpDown pause=new(){Maximum=1439,Width=150},paid=new(){Maximum=1440,Width=150};private readonly CheckBox paidKnown=new(){Text="Ücretlendirilecek dakika belirtildi",AutoSize=true};
    private readonly Label result=new(){AutoSize=true};
    internal AttendanceMatchDialog(Guid project,UserSession user,DataRow target,DataTable events):base(user,"Muhasebe — Kontrollü olay / puantaj eşlemesi","Ham olay eşlemesi")
    {
        this.project=project;this.target=target;ClientSize=new(760,720);
        Fields.Controls.Add(new Label{Text=$"{target["AD"]} {target["SOYAD"]} · {target.Field<DateTime>("WorkDate"):dd.MM.yyyy} · {target["ShiftName"]}\nMevcut taslak saatleri: {target["LocalStart"]} → {target["LocalEnd"]}. Kaydet, bu saatleri aşağıdaki olaylardan türetilen saatlerle değiştirir; onay vermez.",AutoSize=true});
        foreach(DataRow e in events.Rows)
        {
            if(!e.IsNull("IgnoredUtc")||!e.IsNull("ActiveMatchId")||e.Field<Guid>("PersonId")!=target.Field<Guid>("PersonId")||e.Field<Guid>("EmploymentId")!=target.Field<Guid>("EmploymentId")||e.Field<int>("PersonnelGroup")!=target.Field<int>("PersonnelGroup")||e.Field<Guid>("RosterAssignmentId")!=target.Field<Guid>("RosterAssignmentId"))continue;
            if(e.Field<string>("Direction")=="In"&&e.Field<DateTime>("OccurredLocal").Date==target.Field<DateTime>("WorkDate"))entry.Items.Add(new Event(e));
            else if(e.Field<string>("Direction")=="Out")exit.Items.Add(new Event(e));
        }
        pause.Value=target.Field<int>("BreakMinutes");paidKnown.Checked=!target.IsNull("PayableMinutes");paid.Value=paidKnown.Checked?target.Field<int>("PayableMinutes"):0;
        Field("Giriş olayı (aynı iş günü, açık seçim)",entry);Field("Çıkış olayı (gece için ertesi gün olabilir)",exit);Field("Gerçek mola (dakika)",pause);Field("Ücretlendirilecek süre (tutar hesaplanmaz)",paidKnown);Field("Ücretlendirilecek dakika",paid);
        Fields.Controls.Add(new Label{Text="EventMinuteFloor.v1: ham saniye/kesirler değiştirilmez; puantaja aktarılırken iki uçta da dakikadan küçük bölüm aşağıya atılır. Bu bir ücret/mevzuat kuralı değildir. Oluşan saatleri kontrol edip Kaydet ile açıkça kabul edin. Otomatik eşleme, çıkış tahmini veya onay yapılmaz.",AutoSize=true});Fields.Controls.Add(result);
        entry.SelectedIndexChanged+=(_,_)=>Preview();exit.SelectedIndexChanged+=(_,_)=>Preview();pause.ValueChanged+=(_,_)=>Preview();paidKnown.CheckedChanged+=(_,_)=>{paid.Enabled=paidKnown.Checked;Preview();};paid.ValueChanged+=(_,_)=>Preview();
        Fields.SizeChanged+=(_,_)=>entry.Width=exit.Width=Math.Max(1,Fields.ClientSize.Width-48);Preview();AcceptTrackedChanges();
    }
    private void Preview()
    {
        if(entry.SelectedItem is not Event a||exit.SelectedItem is not Event b){result.Text="Giriş ve çıkış olaylarını ayrı ayrı seçin.";return;}
        var start=TimesheetService.EventMinute(a.Row.Field<DateTime>("OccurredLocal"));var end=TimesheetService.EventMinute(b.Row.Field<DateTime>("OccurredLocal"));
        string duration;
        try{var utc=SchedulingRules.UtcRange(start,end);duration=$"Brüt: {(utc.End-utc.Start).TotalMinutes:0} dk · Net: {(utc.End-utc.Start).TotalMinutes-(double)pause.Value:0} dk";}
        catch(ArgumentException ex){duration=ex.Message;}
        result.Text=$"Puantaja yazılacak: {start:dd.MM.yyyy HH:mm} → {end:dd.MM.yyyy HH:mm}\n{duration} · Mola: {pause.Value} dk · Ücretlendirilecek: {(paidKnown.Checked?paid.Value.ToString():"Belirtilmedi")} dk. Kaydet taslak/iade durumunu korur; çakışma ve eksikler gönderimde yeniden kontrol edilir.";
    }
    protected override void RefreshEditState(){base.RefreshEditState();paid.Enabled=paid.Enabled&&paidKnown.Checked;}
    protected override object MakeRequest(string reason)
    {
        var a=(entry.SelectedItem as Event)?.Row??throw new ArgumentException("Giriş olayı seçin.");var b=(exit.SelectedItem as Event)?.Row??throw new ArgumentException("Çıkış olayı seçin.");
        return new Wire.AttendanceMatch(project,command,match,target.Field<Guid>("TimesheetId"),((byte[])target["RecordVersion",DataRowVersion.Original]).ToArray(),a.Field<Guid>("EventId"),((byte[])a["RecordVersion"]).ToArray(),b.Field<Guid>("EventId"),((byte[])b["RecordVersion"]).ToArray(),((byte[])target["PersonVersion"]).ToArray(),((byte[])target["RosterVersion"]).ToArray(),(int)pause.Value,paidKnown.Checked?(int)paid.Value:null,reason);
    }
}
internal sealed class AttendanceDecisionDialog : CalendarEditDialog
{
    private readonly object request;
    internal AttendanceDecisionDialog(UserSession user,object request,string title,string description):base(user,title,"Ham olay kararı")
    {this.request=request;Fields.Controls.Add(new Label{Text=description,AutoSize=true});AcceptTrackedChanges();}
    protected override object MakeRequest(string reason)=>request switch{Wire.AttendanceIgnore x=>x with{Reason=reason},Wire.AttendanceRelease x=>x with{Reason=reason},_=>throw new ArgumentException("Geçersiz olay kararı.")};
}
