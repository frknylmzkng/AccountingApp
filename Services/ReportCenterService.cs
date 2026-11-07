using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class ReportCenterService(string connectionString,UserSession user)
{
    internal static readonly IReadOnlyDictionary<string,string> Reports=new Dictionary<string,string>{
        ["Assignments"]="Personel / ekip atamaları",["Policies"]="Ücret / gider kural sürümleri",
        ["Claims"]="Ücret / gider talepleri",["Transfers"]="Ücret / gider defter aktarımları",
        ["Tasks"]="Bekleyen ücret / gider işleri"};
    private static Wire.ReportCenterScope Validate(Wire.ReportCenterScope s)
    {
        if(s.ProjectId==Guid.Empty||!Reports.ContainsKey(s.Report)||s.TeamId==Guid.Empty)throw new ArgumentException("Rapor/proje/ekip geçersiz.");
        var filter=new FinancialQueryScope(new(s.From,s.To,s.Person));
        if(s.Report=="Policies"&&(s.TeamId!=null||filter.Filter.Person.Length!=0))throw new ArgumentException("Proje kuralı kişi veya ekibe ait değildir; ekip ve kişi filtresini temizleyin.");
        if(s.PendingOnly&&s.Report is "Assignments" or "Transfers")throw new ArgumentException("Bu raporda bekleyen işlem filtresi yok.");
        return s with{From=filter.Filter.From,To=filter.Filter.To,Person=filter.Filter.Person};
    }
    private async Task AuthorizeAsync(IUnitOfWork w,Wire.ReportCenterScope s,CancellationToken ct,bool export=false)
    {
        await ProjectAuthorization.DemandAsync(w,user,s.ProjectId,s.Report=="Assignments"?ProjectAccess.Read:ProjectAccess.Costs,ct);
        if(export)await ProjectAuthorization.DemandAsync(w,user,s.ProjectId,ProjectAccess.Export,ct);
        if(s.TeamId is{} team){using var c=w.Command("SELECT COUNT(*) FROM dbo.ProjectTeams WHERE ProjectId=@project AND TeamId=@team");Id(c,"@project",s.ProjectId);Id(c,"@team",team);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))!=1)throw new ArgumentException("Ekip seçili projeye ait değil.");}
    }
    internal async Task<Wire.ReportCenterOptions> OptionsAsync(Guid project,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.ReportCenterOptions>($"report-center/{project:D}/options",null,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        using var teams=w.Command("SELECT TeamId,Name FROM dbo.ProjectTeams WHERE ProjectId=@project ORDER BY Name,TeamId");Id(teams,"@project",project);var t=await teams.TableAsync(ct);
        using var presets=w.Command("SELECT PresetId,Name,DefinitionJson FROM dbo.ReportPresets WHERE OwnerAccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND ProjectId=@project ORDER BY Name,PresetId");Id(presets,"@project",project);using var p=await presets.TableAsync(ct);
        var result=p.AsEnumerable().Select(r=>new Wire.ReportPreset(r.Field<Guid>("PresetId"),r.Field<string>("Name")!,JsonSerializer.Deserialize<Wire.ReportCenterScope>(r.Field<string>("DefinitionJson")!)!)).ToArray();await w.CommitAsync(ct);return new(RemoteApi.Encode(t),result);
    }
    internal async Task SavePresetAsync(Wire.ReportPreset input,CancellationToken ct)
    {
        input=input with{Reason=AuditContext.ValidateReason(string.IsNullOrEmpty(input.Reason)?AuditContext.Reason??"":input.Reason)};using var reason=AuditContext.UseReason(input.Reason);
        var scope=Validate(input.Scope);var name=(input.Name??"").Trim();if(input.PresetId==Guid.Empty||name.Length is <1 or >80||name.Any(char.IsControl))throw new ArgumentException("Görünüm adı 1–80 karakter olmalı.");
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("report-center/presets/save",input with{Name=name,Scope=scope},ct);return;}
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,scope,ct);string json=JsonSerializer.Serialize(scope);
        using var existing=w.Command("SELECT Name,DefinitionJson,ProjectId FROM dbo.ReportPresets WITH(UPDLOCK,HOLDLOCK) WHERE PresetId=@id AND OwnerAccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");Id(existing,"@id",input.PresetId);using var old=await existing.TableAsync(ct);
        if(old.Rows.Count>0){var row=old.Rows[0];if(row.Field<Guid>("ProjectId")!=scope.ProjectId||row.Field<string>("Name")!=name||row.Field<string>("DefinitionJson")!=json)throw new InvalidOperationException("Görünüm kimliği başka bir tanımla kayıtlı; yeni görünüm kaydedin.");await w.CommitAsync(ct);return;}
        using var count=w.Command("SELECT COUNT(*) FROM dbo.ReportPresets WITH(UPDLOCK,HOLDLOCK) WHERE OwnerAccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND ProjectId=@project");Id(count,"@project",scope.ProjectId);if(Convert.ToInt32(await count.ExecuteScalarAsync(ct))>=50)throw new InvalidOperationException("Proje başına en fazla 50 özel görünüm kaydedilebilir.");
        using var c=w.Command("INSERT dbo.ReportPresets(PresetId,ProjectId,OwnerAccountId,Name,DefinitionJson) VALUES(@id,@project,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),@name,@definition)");Id(c,"@id",input.PresetId);Id(c,"@project",scope.ProjectId);c.Text("@name",name,80);c.Text("@definition",json,2000);await c.ExecuteNonQueryAsync(ct);await AuditAsync(w,scope.ProjectId,"Report.Preset.Create",new{input.PresetId,Name=name,Scope=scope},ct);await w.CommitAsync(ct);
    }
    internal async Task DeletePresetAsync(Wire.ReportPresetDelete input,CancellationToken ct)
    {
        input=input with{Reason=AuditContext.ValidateReason(string.IsNullOrEmpty(input.Reason)?AuditContext.Reason??"":input.Reason)};using var reason=AuditContext.UseReason(input.Reason);
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("report-center/presets/delete",input,ct);return;}
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.Read,ct);
        using var c=w.Command("DELETE dbo.ReportPresets OUTPUT deleted.Name,deleted.DefinitionJson WHERE PresetId=@id AND ProjectId=@project AND OwnerAccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");Id(c,"@id",input.PresetId);Id(c,"@project",input.ProjectId);using var rows=await c.TableAsync(ct);if(rows.Rows.Count!=0)await AuditAsync(w,input.ProjectId,"Report.Preset.Delete",new{input.PresetId,Name=rows.Rows[0]["Name"],Definition=rows.Rows[0]["DefinitionJson"]},ct);await w.CommitAsync(ct);
    }
    internal async Task<Wire.ReportCenterResult> BrowseAsync(Wire.ReportCenterBrowse input,CancellationToken ct)
    {
        var s=Validate(input.Scope);FinancialQueryScope.Page(input.Index,input.Size);
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.ReportCenterResult>("report-center/browse",input with{Scope=s},ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,s,ct);
        var page=await FinancialQueryScope.ReadPageAsync(w,Sql(s),c=>Bind(c,s),input.Index,input.Size,ct);var now=DateTime.UtcNow;await w.CommitAsync(ct);return new(new(RemoteApi.Encode(page.Rows),page.Info.Total,page.Info.Index,page.Info.Size),now,Describe(s));
    }
    private static void Id(SqlCommand c,string name,Guid? value)=>c.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=(object?)value??DBNull.Value;
    private static void Bind(SqlCommand c,Wire.ReportCenterScope s)
    {
        Id(c,"@project",s.ProjectId);Id(c,"@team",s.TeamId);new FinancialQueryScope(new(s.From,s.To,s.Person)).Bind(c);
        c.Parameters.Add("@pending",SqlDbType.Bit).Value=s.PendingOnly||s.Report=="Tasks";
    }
    internal static string Describe(Wire.ReportCenterScope s)=>$"{Reports[s.Report]} · Proje {s.ProjectId:D} · {new FinancialQueryScope(new(s.From,s.To,s.Person)).Description} · Ekip: {s.TeamId?.ToString("D")??"tümü"} · Bekleyen: {s.PendingOnly||s.Report=="Tasks"}. Ekip üyeliği atama tarih aralığına göre seçimdir; maliyeti ekiplere paylaştırmaz. Tarihler atama/talep/kural/aktarım kayıt zamanını süzer.";
    private async Task AuditAsync(IUnitOfWork w,Guid project,string action,object details,CancellationToken ct)
    {
        _=AuditContext.ValidateReason(AuditContext.Reason??"");using var c=w.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,ActorAccountId,CorrelationId,SqlPrincipal,Source)
 VALUES(@actor,@action,N'Project',@project,N'Report',CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',@details,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application')");c.Text("@actor",user.Username);c.Text("@action",action,100);c.Text("@project",project.ToString("D"),100);c.Text("@details",JsonSerializer.Serialize(details),-1);await c.ExecuteNonQueryAsync(ct);
    }
}
