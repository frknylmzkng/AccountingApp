using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record TimesheetDraftExtra(Guid Id,Guid Command,Wire.TimesheetWrite[]? Rows=null,Wire.WorkSegment[]? Pieces=null,string[]? Labels=null,ManualTimesheetDraftRow[]? ManualRows=null)
{
    internal static TimesheetDraftExtra Read(JsonElement json)
    {var x=json.Deserialize<TimesheetDraftExtra>()??throw new ArgumentException("Taslak kimlikleri boş.");if(x.Id==Guid.Empty||x.Command==Guid.Empty)throw new ArgumentException("Taslak kimliği geçersiz.");return x;}
}
internal interface ITimesheetDraftChoice { string DraftKey { get; } }
internal sealed record TimesheetDraftRecord(int Format,string Kind,string Target,string Baseline,Guid Epoch,DateTime SavedUtc,string Caption,string Preview,JsonElement Controls,JsonElement Extra,JsonElement? Intent);
internal sealed class TimesheetDraftStore
{
    internal string DirectoryPath { get; }
    private readonly string scope;
    internal TimesheetDraftStore(string connection,Wire.TimesheetDraftContext owner,Guid project)
    {
        scope=$"TimesheetDraft.v1|{connection}|{owner.DatabaseScope}|{owner.AccountId:D}|{project:D}";
        DirectoryPath=Path.Combine(UserPreferences.DirectoryPath,"timesheet-drafts",Hash(scope));
    }
    internal static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static string Fingerprint(params object?[] values)=>Hash(JsonSerializer.Serialize(values));
    internal static string RowKey(DataRow r,params string[] names)=>Fingerprint(names.Select(n=>r.Table.Columns.Contains(n)&&!r.IsNull(n)?r[n,DataRowVersion.Original]:null).ToArray());
    internal string PathFor(string kind,string target)=>Path.Combine(DirectoryPath,Hash(kind+"|"+target)+".draft");
    internal Lease Acquire(string path)
    {
        if(Path.GetDirectoryName(Path.GetFullPath(path))!=Path.GetFullPath(DirectoryPath)||Path.GetExtension(path)!=".draft")throw new ArgumentException("Taslak yolu geçersiz.");
        Directory.CreateDirectory(DirectoryPath);
        if(!File.Exists(path)&&Directory.EnumerateFiles(DirectoryPath,"*.draft").Take(200).Count()>=200)throw new InvalidOperationException("200 taslak sınırına ulaşıldı; önce kurtarma listesinden eski taslakları sonuçlandırın.");
        try{return new(this,path,new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None));}
        catch(IOException ex){throw new InvalidOperationException("Taslak başka bir pencerede açık. Önce o pencereyi kapatın.",ex);}
    }
    internal TimesheetDraftRecord Read(string path)
    {
        if(new FileInfo(path).Length>4*1024*1024)throw new InvalidOperationException("Taslak 4 MB sınırını aşıyor.");
        var r=DraftStore.Load<TimesheetDraftRecord>(path,scope)??throw new InvalidOperationException("Taslak okunamadı.");
        if(r.Format!=1||r.Epoch==Guid.Empty||r.Kind is not("Planned" or "Unplanned" or "Csv" or "Allocation" or "ManualBatch")||PathFor(r.Kind,r.Target)!=path)throw new InvalidOperationException("Taslak sürümü/kapsamı geçersiz.");
        return r;
    }
    internal sealed class Lease(TimesheetDraftStore store,string path,FileStream gate):IDisposable
    {
        internal TimesheetDraftRecord? Read()=>File.Exists(path)?store.Read(path):null;
        internal void Write(TimesheetDraftRecord value)
        {if(JsonSerializer.SerializeToUtf8Bytes(value).Length>3*1024*1024)throw new InvalidOperationException("Taslak içeriği 3 MB sınırını aşıyor.");DraftStore.Save(path,store.scope,value,durable:true);}
        internal void Clear()=>File.Delete(path);
        public void Dispose()=>gate.Dispose();
    }
}

internal static class TimesheetDraftFields
{
    private sealed record Value(string Kind,DateTime? Date,bool Checked,decimal Number,string? Key);
    private static Control[] Inputs(Control root)=>root.Controls.Cast<Control>().Where(c=>c is DateTimePicker or NumericUpDown or CheckBox or ComboBox).ToArray();
    internal static JsonElement Capture(Control root)=>JsonSerializer.SerializeToElement(Inputs(root).Select(c=>c switch
    {
        DateTimePicker d=>new Value("Date",d.Value,d.Checked,0,null),
        NumericUpDown n=>new Value("Number",null,false,n.Value,null),
        CheckBox b=>new Value("Check",null,b.Checked,0,null),
        ComboBox b=>new Value("Choice",null,false,0,(b.SelectedItem as ITimesheetDraftChoice)?.DraftKey),
        _=>throw new InvalidOperationException()
    }).ToArray());
    internal static Action PrepareRestore(Control root,JsonElement payload)
    {
        var values=payload.Deserialize<Value[]>()??throw new ArgumentException("Taslak alanları boş.");var inputs=Inputs(root);if(values.Length!=inputs.Length)throw new InvalidOperationException("Taslak ekran alanları bu sürümle uyumlu değil.");
        var actions=new List<Action>();
        for(int i=0;i<inputs.Length;i++)
        {
            var v=values[i];switch(inputs[i])
            {
                case DateTimePicker d when v.Kind=="Date"&&v.Date>=d.MinDate&&v.Date<=d.MaxDate:actions.Add(()=>{d.Value=v.Date!.Value;d.Checked=v.Checked;});break;
                case NumericUpDown n when v.Kind=="Number"&&v.Number>=n.Minimum&&v.Number<=n.Maximum:actions.Add(()=>n.Value=v.Number);break;
                case CheckBox b when v.Kind=="Check":actions.Add(()=>b.Checked=v.Checked);break;
                case ComboBox b when v.Kind=="Choice":
                    object? choice=v.Key==null?null:b.Items.Cast<object>().SingleOrDefault(x=>(x as ITimesheetDraftChoice)?.DraftKey==v.Key);
                    if(v.Key!=null&&choice==null)throw new InvalidOperationException("Taslağın tarihli ataması/proje sürümü değişmiş. Eski seçim güncel seçimle değiştirilemez.");
                    actions.Add(()=>b.SelectedItem=choice);break;
                default:throw new InvalidOperationException("Taslak alan türü/değeri geçersiz.");
            }
        }
        return ()=>{foreach(var action in actions)action();};
    }
    internal static void Watch(Control root,EventHandler changed)
    {
        foreach(Control c in root.Controls)
        {
            if(c is DateTimePicker d){d.ValueChanged+=changed;d.CloseUp+=changed;d.MouseUp+=(_,e)=>changed(d,e);d.KeyUp+=(_,e)=>changed(d,e);}
            else if(c is NumericUpDown n)n.ValueChanged+=changed;
            else if(c is CheckBox b)b.CheckedChanged+=changed;
            else if(c is ComboBox combo)combo.SelectedIndexChanged+=changed;
            else if(c is TextBox t)t.TextChanged+=changed;
        }
    }
}
