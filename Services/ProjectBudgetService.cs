using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record ProjectBudgetData(DataTable Summary,(DataTable Rows,Wire.PageInfo Info) History,(DataTable Rows,Wire.PageInfo Info) Costs,DateTime ReadUtc,string Caption,string ReadBy)
{
    internal Wire.ProjectBudgetBoard Encode()=>new(RemoteApi.Encode(Summary),new(RemoteApi.Encode(History.Rows),History.Info.Total,History.Info.Index,History.Info.Size),new(RemoteApi.Encode(Costs.Rows),Costs.Info.Total,Costs.Info.Index,Costs.Info.Size),ReadUtc,Caption,ReadBy);
}
internal sealed class ProjectBudgetService(string connectionString,UserSession user)
{
    private static void Id(SqlCommand c,string name,Guid? value)=>c.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=(object?)value??DBNull.Value;
    private static byte[] Hash(object value)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value,value.GetType()));
    private static async Task AuthorizeAsync(IUnitOfWork w,UserSession user,Guid project,bool write,CancellationToken ct)
    {
        await TimesheetService.AllocationGateAsync(w,ct);await AccountAuthorization.DemandAsync(w,user,write?AccountAccess.Admin:AccountAccess.Read,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Costs,ct);
    }
    internal async Task SaveAsync(Wire.ProjectBudgetSave input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("project-budgets/save",input,ct);return;}
        if(input.BudgetId==Guid.Empty||input.LaborBudget<0||input.OtherBudget<0||input.LaborBudget>9999999999999999.99m||input.OtherBudget>9999999999999999.99m||input.LaborBudget+input.OtherBudget>9999999999999999.99m||decimal.Round(input.LaborBudget,2)!=input.LaborBudget||decimal.Round(input.OtherBudget,2)!=input.OtherBudget)throw new ArgumentException("İşçilik ve diğer gider bütçeleri sıfır veya pozitif, iki ondalıklı olmalı; toplam tutar sınırını aşamaz.");
        string explanation=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(explanation);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,user,input.ProjectId,true,ct);
        Guid actor;using(var c=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))"))actor=(Guid)(await c.ExecuteScalarAsync(ct)??throw new SessionRevokedException());byte[] hash=Hash(input);
        using(var prior=w.Command("SELECT CreatedByAccountId,PayloadHash FROM dbo.ProjectBudgetRevisions WHERE BudgetId=@id")){Id(prior,"@id",input.BudgetId);var rows=await prior.TableAsync(ct);if(rows.Rows.Count>0){var r=rows.Rows[0];if(r.Field<Guid>("CreatedByAccountId")!=actor||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Bütçe işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        using(var current=w.Command("SELECT RecordVersion,IsActive FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project")){Id(current,"@project",input.ProjectId);var rows=await current.TableAsync(ct);if(rows.Rows.Count!=1||!rows.Rows[0].Field<bool>("IsActive")||!((byte[])rows.Rows[0]["RecordVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.ProjectVersion)))throw new InvalidOperationException("Proje seçimi değişmiş veya proje pasif; yenileyin.");}
        Guid? previous;using(var latest=w.Command("SELECT TOP(1) BudgetId FROM dbo.ProjectBudgetRevisions WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project ORDER BY [Sequence] DESC")){Id(latest,"@project",input.ProjectId);previous=await latest.ExecuteScalarAsync(ct) is Guid id?id:null;}if(previous!=input.PreviousBudgetId)throw new InvalidOperationException("Başka kullanıcı bütçeyi revize etmiş; güncel bütçeyi inceleyip yeniden hazırlayın.");
        using var insert=w.Command("INSERT dbo.ProjectBudgetRevisions(BudgetId,ProjectId,PreviousBudgetId,LaborBudget,OtherBudget,Reason,CreatedByAccountId,CreatedBy,PayloadHash) VALUES(@id,@project,@previous,@labor,@other,@reason,@actor,@name,@hash)");Id(insert,"@id",input.BudgetId);Id(insert,"@project",input.ProjectId);Id(insert,"@previous",input.PreviousBudgetId);Id(insert,"@actor",actor);insert.Money("@labor",input.LaborBudget);insert.Money("@other",input.OtherBudget);insert.Text("@reason",explanation,500);insert.Text("@name",user.Username);insert.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await insert.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    internal async Task<Wire.ProjectBudgetBoard> ReadAsync(Wire.ProjectBudgetBrowse input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.ProjectBudgetBoard>("project-budgets/browse",input,ct);
        for(int attempt=0;;attempt++)try{await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);var report=await BuildAsync(w,user,input,ct);await w.CommitAsync(ct);return report.Encode();}catch(SqlException ex)when(ex.Number==1205&&attempt<2){await Task.Delay(150*(attempt+1),ct);}
    }
    internal static async Task<ProjectBudgetData> BuildAsync(IUnitOfWork w,UserSession user,Wire.ProjectBudgetBrowse input,CancellationToken ct,string? exportSection=null)
    {
        await AuthorizeAsync(w,user,input.ProjectId,false,ct);
        void Bind(SqlCommand c)=>Id(c,"@project",input.ProjectId);
        using var summaryQuery=w.Command(@"WITH A AS(SELECT COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN Kind=N'Accrual' THEN EffectiveAmount ELSE 0 END)),0) ActualLabor,
 COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN Kind=N'Expense' THEN EffectiveAmount ELSE 0 END)),0) ActualOther,
 COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN Kind=N'Accrual' THEN AdjustmentAmount ELSE 0 END)),0) LaborCorrections
 FROM dbo.FinanceEffectiveEntries WHERE ProjectId=@project AND ReversedUtc IS NULL)
 SELECT P.Code,P.Name,P.IsActive,P.RecordVersion ProjectVersion,B.BudgetId,B.CreatedUtc BudgetUtc,B.CreatedBy BudgetBy,B.LaborBudget,B.OtherBudget,B.LaborBudget+B.OtherBudget TotalBudget,
 A.ActualLabor,A.ActualOther,A.LaborCorrections,A.ActualLabor+A.ActualOther ActualTotal,
 A.ActualLabor-B.LaborBudget LaborVariance,A.ActualOther-B.OtherBudget OtherVariance,A.ActualLabor+A.ActualOther-(B.LaborBudget+B.OtherBudget) CostVariance,
 B.LaborBudget+B.OtherBudget-(A.ActualLabor+A.ActualOther) RemainingBudget
 FROM dbo.FinanceProjects P CROSS JOIN A OUTER APPLY(SELECT TOP(1) R.* FROM dbo.ProjectBudgetRevisions R WHERE R.ProjectId=P.ProjectId ORDER BY R.[Sequence] DESC) B WHERE P.ProjectId=@project");Bind(summaryQuery);var summary=await summaryQuery.TableAsync(ct);if(summary.Rows.Count!=1)throw new InvalidOperationException("Proje bulunamadı.");
        summary.Columns.Add("UsagePercent",typeof(decimal));summary.Columns.Add("BudgetStatus",typeof(string));var head=summary.Rows[0];
        if(head.IsNull("BudgetId"))head["BudgetStatus"]="Bütçe tanımlı değil; sapma/yüzde hesaplanmaz";
        else if(head.Field<decimal>("TotalBudget")==0)head["BudgetStatus"]="Açıkça sıfır bütçe; yüzde hesaplanmaz";
        else{try{head["UsagePercent"]=decimal.Round(head.Field<decimal>("ActualTotal")/head.Field<decimal>("TotalBudget")*100m,2,MidpointRounding.AwayFromZero);head["BudgetStatus"]="Güncel bütçe revizyonuyla karşılaştırma";}catch(OverflowException){head["BudgetStatus"]="Yüzde gösterim sınırı aşıldı; tutarları inceleyin";}}
        const string historySql=@"SELECT TOP(10001) CONVERT(nvarchar(36),BudgetId) BudgetReference,CONVERT(nvarchar(36),PreviousBudgetId) PreviousBudgetReference,[Sequence],LaborBudget,OtherBudget,LaborBudget+OtherBudget TotalBudget,Currency,Reason,CreatedBy,CreatedUtc FROM dbo.ProjectBudgetRevisions WHERE ProjectId=@project ORDER BY [Sequence] DESC";
        const string costsSql=@"SELECT TOP(10001) CONVERT(nvarchar(36),EntryId) FinanceReferenceId,Reference FinanceReference,Kind,PeriodStart,PeriodEnd,Amount OriginalAmount,AdjustmentAmount,EffectiveAmount,
 CASE WHEN ReversedUtc IS NULL THEN EffectiveAmount ELSE 0 END ActiveCost,CreatedBy,CreatedUtc,ReversedUtc,ReversedBy FROM dbo.FinanceEffectiveEntries WHERE ProjectId=@project ORDER BY CreatedUtc DESC,EntryId DESC";
        var hp=input.Pages?.GetValueOrDefault("History")??new();var cp=input.Pages?.GetValueOrDefault("Costs")??new();var history=await FinancialQueryScope.ReadPageAsync(w,historySql,Bind,hp.Index,hp.Size,ct,exportSection=="History");var costs=await FinancialQueryScope.ReadPageAsync(w,costsSql,Bind,cp.Index,cp.Size,ct,exportSection=="Costs");
        DateTime readUtc;using(var c=w.Command("SELECT SYSUTCDATETIME()"))readUtc=DateTime.SpecifyKind((DateTime)(await c.ExecuteScalarAsync(ct))!,DateTimeKind.Utc);
        return new(summary,history,costs,readUtc,head["Code"]+" · "+head["Name"],user.Username);
    }
}
