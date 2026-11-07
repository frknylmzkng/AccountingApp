using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class FinancialFilterBar:FlowLayoutPanel
{
    private readonly DateTimePicker from=new(){Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false,Width=135},to=new(){Format=DateTimePickerFormat.Short,ShowCheckBox=true,Checked=false,Width=135};
    private readonly TextBox person=new(){Width=270,MaxLength=100,PlaceholderText="Kayıtlı hesap adı / kalıcı kişi no"};
    internal event Action? Changed;
    internal Func<Task>? ApplyAsync;
    internal Wire.FinancialFilter Value=>new(from.Checked?DateTime.SpecifyKind(from.Value.Date,DateTimeKind.Unspecified):null,to.Checked?DateTime.SpecifyKind(to.Value.Date,DateTimeKind.Unspecified):null,person.Text.Trim());
    internal bool Matches(Wire.FinancialFilter applied)=>Value==applied;
    internal FinancialFilterBar(SessionForm owner,Guid project,UserSession user,Action revoked,string dateCaption="Kayıt tarihi (İstanbul):",string personHint="Kayıtlı hesap adı / kalıcı kişi no")
    {
        Dock=DockStyle.Top;AutoSize=true;WrapContents=true;Padding=new(8);person.PlaceholderText=personHint;
        var apply=new Button{Text="Filtreyi uygula",AutoSize=true};var clear=new Button{Text="Filtreyi temizle",AutoSize=true};var pick=new Button{Text="Kişi seç…",AutoSize=true};
        Controls.AddRange([new Label{Text=dateCaption,AutoSize=true,Padding=new(0,6,0,0)},from,to,person,pick,apply,clear]);
        from.ValueChanged+=(_,_)=>Changed?.Invoke();to.ValueChanged+=(_,_)=>Changed?.Invoke();from.MouseUp+=(_,_)=>Changed?.Invoke();to.MouseUp+=(_,_)=>Changed?.Invoke();from.KeyUp+=(_,_)=>Changed?.Invoke();to.KeyUp+=(_,_)=>Changed?.Invoke();person.TextChanged+=(_,_)=>Changed?.Invoke();
        async Task Apply(){if(owner.IsOperationRunning)return;try{_ =new FinancialQueryScope(Value);if(ApplyAsync!=null)await ApplyAsync();}catch(ArgumentException ex){MessageBox.Show(owner,ex.Message,"Filtre",MessageBoxButtons.OK,MessageBoxIcon.Information);}}
        apply.Click+=async(_,_)=>await Apply();clear.Click+=async(_,_)=>{if(owner.IsOperationRunning)return;from.Checked=to.Checked=false;person.Clear();await Apply();};
        pick.Click+=(_,_)=>{if(owner.IsOperationRunning)return;using var d=new CalendarPersonPicker(project,user,"Projedeki kişi filtresini seç");d.SessionRevoked+=(_,_)=>revoked();d.ShowDialog(owner);if(d.SelectedPerson is{} chosen)person.Text=chosen.Id.ToString("D");};
    }
}
