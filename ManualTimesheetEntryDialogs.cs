using System.Data;
namespace WinFormsApp2;

internal sealed class ManualTimesheetValuesDialog:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    internal ManualTimesheetValues? Result{get;private set;}
    internal ManualTimesheetValuesDialog(UserSession user,DateTime day,ManualTimesheetValues values,int count)
    {
        Session=user;PersistGridLayout=false;Text="Muhasebe — Gerçekleşen alanları gözden geçir";ClientSize=new(620,450);
        DateTimePicker Moment(DateTime? value,int hour)=>new(){Width=300,Format=DateTimePickerFormat.Custom,CustomFormat="dd.MM.yyyy HH:mm",ShowCheckBox=true,MinDate=new(1900,1,1),MaxDate=new(2199,12,31),Value=value??day.Date.AddHours(hour),Checked=value.HasValue};
        var start=Moment(values.Start,9);var end=Moment(values.End,17);var pause=new NumericUpDown{Minimum=0,Maximum=1439,Value=values.BreakMinutes,Width=180};var paid=new NumericUpDown{Minimum=0,Maximum=1440,Value=values.PayableMinutes??0,Width=180};var known=new CheckBox{Text="Ücretlendirilecek dakika belirtildi",AutoSize=true,Checked=values.PayableMinutes.HasValue};paid.Enabled=known.Checked;known.CheckedChanged+=(_,_)=>paid.Enabled=known.Checked;
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new(16),FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var note=new Label{AutoSize=true,Text=$"{count} seçili satır · İş günü {day:dd.MM.yyyy}. Çoklu seçimde alanlar seçili tüm satırların önceki alanlarının yerini alır. Bu alanlar yalnız önizlemeye uygulanır; veritabanına yazılmaz. Gece çıkışında ertesi günü seçin. Eksik saat/ücret bilinmiyorsa işareti kaldırıp bilinçli olarak taslak bırakabilirsiniz."};
        layout.Controls.Add(note);layout.Controls.Add(new Label{Text="Gerçek giriş",AutoSize=true});layout.Controls.Add(start);layout.Controls.Add(new Label{Text="Gerçek çıkış",AutoSize=true});layout.Controls.Add(end);layout.Controls.Add(new Label{Text="Gerçek mola (dk)",AutoSize=true});layout.Controls.Add(pause);layout.Controls.Add(known);layout.Controls.Add(paid);
        var bar=new FlowLayoutPanel{AutoSize=true,WrapContents=true};var apply=new Button{Text="Alanları önizlemeye uygula",AutoSize=true};var cancel=new Button{Text="Vazgeç",AutoSize=true};bar.Controls.AddRange([apply,cancel]);layout.Controls.Add(bar);Controls.Add(layout);
        static DateTime? Read(DateTimePicker d)=>d.Checked?DateTime.SpecifyKind(new DateTime(d.Value.Year,d.Value.Month,d.Value.Day,d.Value.Hour,d.Value.Minute,0),DateTimeKind.Unspecified):null;
        apply.Click+=(_,_)=>{Result=new(Read(start),Read(end),(int)pause.Value,known.Checked?(int)paid.Value:null);DialogResult=DialogResult.OK;Close();};cancel.Click+=(_,_)=>Close();
        void Fit()=>note.MaximumSize=new(Math.Max(1,layout.ClientSize.Width-48),0);layout.SizeChanged+=(_,_)=>Fit();Fit();Load+=(_,_)=>MinimumSize=LogicalToDeviceUnits(new Size(500,420));SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
}

internal sealed class ManualTimesheetRosterDialog:SessionForm
{
    protected override bool ShowNavigationTools=>false;
    private sealed record Choice(DataRow Row){public override string ToString()=>$"{Row.Field<DateTime>("StartDate"):dd.MM.yyyy} – {(Row.IsNull("EndDate")?"Açık":Row.Field<DateTime>("EndDate").ToString("dd.MM.yyyy"))} · {Row["TeamName"]}";}
    internal DataRow? Selected{get;private set;}
    internal DateTime Day{get;private set;}
    internal ManualTimesheetRosterDialog(UserSession user,ScopedPerson person,DataTable rosters,DateTime first)
    {
        Session=user;PersistGridLayout=false;Text="Muhasebe — Vardiyasız kişi / tarihli atama";ClientSize=new(620,300);
        var day=new DateTimePicker{Format=DateTimePickerFormat.Short,MinDate=first,MaxDate=first.AddDays(6),Value=first,Width=180};var choices=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Width=500};foreach(DataRow r in rosters.Rows)choices.Items.Add(new Choice(r));if(choices.Items.Count>0)choices.SelectedIndex=0;
        var layout=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false};var name=new Label{Text=person.Name,AutoSize=true};layout.Controls.AddRange([name,new Label{Text="İş günü (yüklenen hafta)",AutoSize=true},day,new Label{Text="Özgün tarihli proje / ekip ataması",AutoSize=true},choices]);
        var add=new Button{Text="Önizlemeye ekle",AutoSize=true};var cancel=new Button{Text="Vazgeç",AutoSize=true};layout.Controls.Add(add);layout.Controls.Add(cancel);Controls.Add(layout);
        add.Click+=(_,_)=>{if(choices.SelectedItem is not Choice c){MessageBox.Show(this,"Geçerli tarihli atama seçin.");return;}if(c.Row.Field<DateTime>("StartDate")>day.Value.Date||(!c.Row.IsNull("EndDate")&&c.Row.Field<DateTime>("EndDate")<day.Value.Date)){MessageBox.Show(this,"Atama iş gününü kapsamıyor; doğru atamayı seçin.");return;}Selected=c.Row;Day=DateTime.SpecifyKind(day.Value.Date,DateTimeKind.Unspecified);DialogResult=DialogResult.OK;Close();};cancel.Click+=(_,_)=>Close();layout.SizeChanged+=(_,_)=>{choices.Width=Math.Max(1,Math.Min(500,layout.ClientSize.Width-48));name.MaximumSize=new(Math.Max(1,layout.ClientSize.Width-48),0);};Load+=(_,_)=>MinimumSize=LogicalToDeviceUnits(new Size(480,290));SessionRevoked+=(_,_)=>Close();PageRequested+=(_,_)=>Close();
    }
}
