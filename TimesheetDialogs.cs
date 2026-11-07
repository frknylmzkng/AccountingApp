using System.Data;
using System.Text.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class TimesheetEditDialog : CalendarEditDialog
{
    private readonly Guid project; private Guid id,command=Guid.NewGuid(); private readonly DataRow row;private readonly bool edit;
    private readonly DateTimePicker start=Moment(),end=Moment();private readonly NumericUpDown pause=Number(),paid=Number();private readonly CheckBox paidKnown=new(){Text="Ücretlendirilecek dakika belirtildi",AutoSize=true};
    private static NumericUpDown Number()=>new(){Minimum=0,Maximum=1440,Width=170};
    private static DateTimePicker Moment()=>new(){Width=280,Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",ShowCheckBox=true,MinDate=new(1900,1,1),MaxDate=new(2199,12,31)};
    private static DateTime? Read(DateTimePicker p)=>p.Checked?DateTime.SpecifyKind(new DateTime(p.Value.Year,p.Value.Month,p.Value.Day,p.Value.Hour,p.Value.Minute,0),DateTimeKind.Unspecified):null;
    internal TimesheetEditDialog(Guid project,UserSession user,DataRow row,bool edit):base(user,"Muhasebe — Gerçekleşen çalışma girişi","Puantaj kaydı")
    {
        this.project=project;this.row=row;this.edit=edit;id=edit?row.Field<Guid>("TimesheetId"):Guid.NewGuid();
        Fields.Controls.Add(new Label{Text=row["AD"]+" "+row["SOYAD"]+" · "+row["ShiftName"],AutoSize=true});
        Fields.Controls.Add(new Label{Text=$"Plan: {row.Field<DateTime>("PlannedStart"):dd.MM.yyyy HH:mm} – {row.Field<DateTime>("PlannedEnd"):dd.MM.yyyy HH:mm} (İstanbul). Plan saatleri gerçekleşen süre olarak kopyalanmaz.",AutoSize=true});
        start.Value=edit&&!row.IsNull("LocalStart")?row.Field<DateTime>("LocalStart"):row.Field<DateTime>("PlannedStart");end.Value=edit&&!row.IsNull("LocalEnd")?row.Field<DateTime>("LocalEnd"):row.Field<DateTime>("PlannedEnd");start.Checked=edit&&!row.IsNull("LocalStart");end.Checked=edit&&!row.IsNull("LocalEnd");
        pause.Value=edit?row.Field<int>("BreakMinutes"):0;paidKnown.Checked=edit&&!row.IsNull("PayableMinutes");paid.Value=paidKnown.Checked?row.Field<int>("PayableMinutes"):0;paid.Enabled=paidKnown.Checked;paidKnown.CheckedChanged+=(_,_)=>paid.Enabled=paidKnown.Checked;
        Field("Gerçek giriş (bilinmiyorsa işareti kaldırın)",start);Field("Gerçek çıkış (gece vardiyasında ertesi günü seçin)",end);Field("Gerçek mola (dakika)",pause);Field("Ücretlendirilecek süre (tutar hesaplanmaz)",paidKnown);Field("Ücretlendirilecek dakika (net süreyi aşamaz)",paid);
        Fields.Controls.Add(new Label{Text="Eksik saatler taslakta saklanır, onaya gönderilemez. Net süre UTC farkından mola düşülerek hesaplanır. Ücretlendirilecek süreyi yetkili kişi açıkça belirler; mevzuat / fazla mesai katsayısı uygulanmaz.",AutoSize=true});AcceptTrackedChanges();
        PersistentDraft(project,"Planned",edit?"edit:"+id.ToString("D"):"new:"+row.Field<Guid>("ShiftAssignmentId").ToString("D"),
            TimesheetDraftStore.RowKey(row,"ShiftAssignmentId","TimesheetId","RecordVersion","AssignmentVersion","ShiftVersion"),row["AD"]+" "+row["SOYAD"]+" · "+row["ShiftName"]+" · "+row.Field<DateTime>("PlannedStart").ToString("dd.MM.yyyy"),
            ()=>JsonSerializer.SerializeToElement(new TimesheetDraftExtra(id,command)),extra=>
            {var x=TimesheetDraftExtra.Read(extra);if(edit&&x.Id!=id)throw new InvalidOperationException("Taslak puantaj hedefi farklı.");return ()=>{id=x.Id;command=x.Command;};},
            ()=>$"Gerçek giriş: {Read(start):dd.MM.yyyy HH:mm} · Çıkış: {Read(end):dd.MM.yyyy HH:mm} · Mola: {pause.Value} dk · Ücret dk: {(paidKnown.Checked?paid.Value.ToString():"Belirtilmedi")}");
    }
    protected override void RefreshEditState(){base.RefreshEditState();paid.Enabled=paid.Enabled&&paidKnown.Checked;}
    protected override object MakeRequest(string reason)=>new Wire.TimesheetBatch(project,command,[new Wire.TimesheetWrite(id,edit?((byte[])row["RecordVersion"]).ToArray():null,row.Field<Guid>("ShiftAssignmentId"),((byte[])row["AssignmentVersion"]).ToArray(),((byte[])row["ShiftVersion"]).ToArray(),edit?row.Field<string>("SourceKey")!:"manual:"+id.ToString("N"),Read(start),Read(end),(int)pause.Value,paidKnown.Checked?(int)paid.Value:null)],reason);
}
internal sealed class TimesheetStateDialog : CalendarEditDialog
{
    private readonly Wire.TimesheetState input;
    internal TimesheetStateDialog(Guid project,UserSession user,DataRow row,string state):base(user,"Muhasebe — Puantaj kararı","Puantaj kararı")
    {
        input=new(project,Guid.NewGuid(),row.Field<Guid>("TimesheetId"),((byte[])row["RecordVersion",DataRowVersion.Original]).ToArray(),state,"");
        string caption=state switch{"Submitted"=>"Onaya gönder","Approved"=>"Onayla","Returned"=>"İade et / yeniden aç",_=>"İptal et"};
        Fields.Controls.Add(new Label{Text=caption+" · "+row["AD"]+" "+row["SOYAD"]+" · "+row["ShiftName"],AutoSize=true});
        Fields.Controls.Add(new Label{Text="Kaydet ile gerekçe istenir. Onay, hazırlayan / son düzenleyen / gönderen hesaptan farklı bir yetkili hesap ister. Bağlı ham olayları giren veya eşleyen hesap da onaylayamaz. Dağıtılmış kaydın onay/iadesi proje parçası sekmesinden bağımsız verilir; ana durum kararlardan türetilir. Onaylı kaydı yeniden açmak onaylı süre toplamından çıkarır; eski değerler ve kararlar işlem geçmişinde korunur. İptal kalıcıdır; aynı vardiya ataması için yeni kayıt açılamaz.",AutoSize=true});AcceptTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>input with{Reason=reason};
}
internal sealed class TimesheetCsvDialog : CalendarEditDialog
{
    private readonly Guid project;private Guid command=Guid.NewGuid();private readonly DataTable assignments;private Wire.TimesheetWrite[]? rows;
    private readonly TextBox csvToken=new(){Visible=false};
    private readonly DataGridView preview=new(){Width=570,Height=280,ReadOnly=true,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells};
    internal TimesheetCsvDialog(Guid project,UserSession user,DataTable assignments):base(user,"Muhasebe — CSV puantaj önizlemesi","Puantaj CSV aktarımı")
    {
        this.project=project;this.assignments=assignments.Copy();Fields.Controls.Add(csvToken);TrackChanges("Puantaj CSV aktarımı",csvToken);ClientSize=new(850,660);RecordSelection.Configure(preview);
        Fields.Controls.Add(new Label{Text="UTF-8, noktalı virgül, en çok 200 satır / 1 MB. Kaynak kimliği dış sistemde sabit ve proje içinde benzersiz olmalı. Yalnız yüklenen haftanın yayınlanmış atamaları kabul edilir. Aynı içerikle tekrar aktarım çoğaltmaz; farklı içerik listeden sürümlü düzeltilir.",AutoSize=true});
        Fields.Controls.Add(new Label{Text="Başlık: SourceKey;ShiftAssignmentId;Start;End;BreakMinutes;PayableMinutes\nSaat biçimi: yyyy-MM-dd HH:mm (İstanbul). Eksik saat / ücret dakikası boş bırakılabilir. Gerçekleşen veriyi kullanın; planı gerçekleşen saymayın.",AutoSize=true});
        var choose=new Button{Text="CSV dosyasını seç / önizle…",AutoSize=true};Field("Dosya",choose);Fields.Controls.Add(preview);Fields.SizeChanged+=(_,_)=>preview.Width=Math.Max(1,Fields.ClientSize.Width-48);
        choose.Click+=(_,_)=>{using var dialog=new OpenFileDialog{Filter="CSV dosyası|*.csv",CheckFileExists=true};if(dialog.ShowDialog(this)!=DialogResult.OK)return;try{LoadCsv(dialog.FileName);}catch(Exception ex)when(ex is IOException or ArgumentException or MalformedLineException or FormatException or OverflowException){rows=null;preview.DataSource=null;MessageBox.Show(this,ex.Message,"CSV okunamadı");}};AcceptTrackedChanges();
        PersistentDraft(project,"Csv","import",TimesheetDraftStore.Fingerprint(assignments.AsEnumerable().Select(a=>TimesheetDraftStore.RowKey(a,"ShiftAssignmentId","AssignmentVersion","ShiftVersion","TimesheetId")).Order().ToArray()),"CSV puantaj önizlemesi",
            ()=>JsonSerializer.SerializeToElement(new TimesheetDraftExtra(command,command,rows)),extra=>
            {
                var x=TimesheetDraftExtra.Read(extra);if(x.Rows?.Length>200)throw new InvalidOperationException("CSV taslak sınırı aşıldı.");
                if(x.Rows!=null&&x.Rows.Any(r=>r==null||!assignments.AsEnumerable().Any(a=>a.Field<Guid>("ShiftAssignmentId")==r.ShiftAssignmentId)))throw new InvalidOperationException("CSV kaynak ataması değişmiş.");
                return ()=>{command=x.Command;rows=x.Rows;var data=new DataTable();foreach(string title in new[]{"Kaynak","Giriş","Çıkış","Mola dk","Ücret dk"})data.Columns.Add(title);foreach(var r in rows??[])data.Rows.Add(r.SourceKey,r.Start,r.End,r.BreakMinutes,r.PayableMinutes);preview.DataSource=data;csvToken.Text=Guid.NewGuid().ToString("N");};
            },()=>"CSV önizlemesi: "+(rows?.Length??0)+" satır. Kaynak kimlikleri, özgün atama sürümleri ve gerçekleşen saatler korunur.");
    }
    private void LoadCsv(string path)
    {
        if(new FileInfo(path).Length>1024*1024)throw new ArgumentException("CSV 1 MB sınırını aşıyor.");
        using var parser=new TextFieldParser(path,new UTF8Encoding(false,true),detectEncoding:false){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=true};parser.SetDelimiters(";");
        string[] header=["SourceKey","ShiftAssignmentId","Start","End","BreakMinutes","PayableMinutes"];
        if(!(parser.ReadFields()??[]).SequenceEqual(header))throw new ArgumentException("CSV başlıkları / sırası geçersiz.");
        var result=new List<Wire.TimesheetWrite>();var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<Guid>();var data=new DataTable();foreach(string name in new[]{"Kaynak","Personel","Vardiya","Giriş","Çıkış","Mola dk","Ücret dk"})data.Columns.Add(name);
        DateTime? Moment(string s)=>s.Length==0?null:DateTime.SpecifyKind(DateTime.ParseExact(s,"yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture),DateTimeKind.Unspecified);
        while(!parser.EndOfData)
        {
            var fields=parser.ReadFields()??[];int line=result.Count+2;if(fields.Length!=6||result.Count>=200)throw new ArgumentException($"Satır {line}: 6 alan ve en çok 200 satır gerekiyor.");string key=fields[0];
            if(key.Length is <1 or >100||key.Any(char.IsControl)||!keys.Add(key))throw new ArgumentException($"Satır {line}: kaynak kimliği boş, uzun veya tekrarlı.");
            var assignment=Guid.Parse(fields[1]);if(!ids.Add(assignment))throw new ArgumentException($"Satır {line}: vardiya ataması tekrarlı.");
            var a=assignments.AsEnumerable().SingleOrDefault(r=>r.Field<Guid>("ShiftAssignmentId")==assignment)??throw new ArgumentException($"Satır {line}: atama seçili proje / haftada bulunamadı.");
            var hash=SHA256.HashData(Encoding.UTF8.GetBytes(project.ToString("N")+"|csv|"+key.ToUpperInvariant()));var id=new Guid(hash.AsSpan(0,16));
            int pause=int.Parse(fields[4],CultureInfo.InvariantCulture);int? paid=fields[5].Length==0?null:int.Parse(fields[5],CultureInfo.InvariantCulture);if(pause is <0 or >=1440||paid is <0 or >1440)throw new ArgumentException($"Satır {line}: dakika sınırı geçersiz.");
            result.Add(new(id,null,assignment,((byte[])a["AssignmentVersion"]).ToArray(),((byte[])a["ShiftVersion"]).ToArray(),key,Moment(fields[2]),Moment(fields[3]),pause,paid));
            data.Rows.Add(key,a["AD"]+" "+a["SOYAD"],a["ShiftName"],fields[2],fields[3],pause,fields[5]);
        }
        if(result.Count==0)throw new ArgumentException("CSV boş.");rows=result.ToArray();preview.DataSource=data;csvToken.Text=Guid.NewGuid().ToString("N");MarkTrackedChanges();
    }
    protected override object MakeRequest(string reason)=>new Wire.TimesheetBatch(project,command,rows??throw new ArgumentException("Önce CSV seçip önizlemeyi kontrol edin."),reason);
}
