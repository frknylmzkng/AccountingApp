using System.Data;
using System.Text.Json;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed partial class ProjectReportService
{
    internal static readonly IReadOnlyDictionary<string,string> BudgetSections=new Dictionary<string,string>{{"Summary","Bütçe / gerçekleşen özet"},{"History","Değişmez bütçe revizyonları"},{"Costs","Gerçekleşen maliyet belgeleri"}};
    internal static string BudgetTitle(string name)=>BudgetTitles.TryGetValue(name,out var title)?title:ReconciliationTitle(name);
    private static readonly Dictionary<string,string> BudgetTitles=new(){["Code"]="Proje kodu",["Name"]="Proje adı",["IsActive"]="Proje etkin",["LaborBudget"]="İşçilik bütçesi TL",["OtherBudget"]="Diğer gider bütçesi TL",["TotalBudget"]="Toplam bütçe TL",["ActualLabor"]="Gerçekleşen işçilik TL",["ActualOther"]="Gerçekleşen diğer gider TL",["LaborCorrections"]="İşçilik mali düzeltmesi TL",["ActualTotal"]="Gerçekleşen toplam TL",["LaborVariance"]="İşçilik aşımı (+) / altı (−) TL",["OtherVariance"]="Diğer gider aşımı (+) / altı (−) TL",["CostVariance"]="Toplam aşım (+) / altı (−) TL",["RemainingBudget"]="Kalan bütçe TL",["UsagePercent"]="Bütçe kullanımı %",["BudgetStatus"]="Bütçe bilgisi",["BudgetUtc"]="Güncel revizyon UTC",["BudgetBy"]="Güncel revizyonu kaydeden",["BudgetId"]="Bütçe kimliği",["BudgetReference"]="Bütçe revizyon no",["PreviousBudgetReference"]="Önceki bütçe revizyonu",["Sequence"]="Revizyon sırası",["Currency"]="Para birimi",["Reason"]="Bütçe gerekçesi",["Kind"]="Maliyet türü",["EffectiveAmount"]="Düzeltilmiş belge TL",["ActiveCost"]="Özete giren maliyet TL",["OriginalAmount"]="İlk belge TL"};
    private async Task DeliverBudgetCsvAsync(Guid project,string section,Func<byte[],CancellationToken,Task> deliver,CancellationToken ct,Wire.FinancialFilter? filter)
    {
        if(!BudgetSections.TryGetValue(section,out var title))throw new ArgumentException("Bütçe bölümü geçersiz.");if(filter!=null&&(filter.From.HasValue||filter.To.HasValue||!string.IsNullOrWhiteSpace(filter.Person)))throw new ArgumentException("Proje bütçesi tüm proje ömrü içindir; tarih/kişi filtresi kabul etmez.");_ =AuditContext.ValidateReason(AuditContext.Reason??"");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);var report=await ProjectBudgetService.BuildAsync(w,user,new(project),ct,section);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Export,ct);
        var source=section switch{"Summary"=>report.Summary,"History"=>report.History.Rows,_=>report.Costs.Rows};
        using var data=new DataTable();foreach(var name in new[]{"Proje","Proje kimliği","Veri zamanı UTC","Oluşturan","Kapsam","Bölüm"})data.Columns.Add(name,typeof(string));var columns=source.Columns.Cast<DataColumn>().Where(c=>c.DataType!=typeof(byte[])).ToArray();foreach(var c in columns)data.Columns.Add(BudgetTitle(c.ColumnName),c.DataType);
        void Add(DataRow? row){var values=new object[columns.Length+6];values[0]=report.Caption;values[1]=project.ToString("D");values[2]=report.ReadUtc.ToString("O");values[3]=report.ReadBy;values[4]="Tüm proje ömrü; güncel bütçe ve iptal edilmemiş hakediş/gider; nakit ödemeler tekrar eklenmez";values[5]=title;for(int i=0;i<columns.Length;i++)values[i+6]=row?[columns[i]]??DBNull.Value;data.Rows.Add(values);}
        foreach(DataRow row in source.Rows)Add(row);if(source.Rows.Count==0)Add(null);
        string path=Path.Combine(Path.GetTempPath(),"AccountingApp-budget-"+Guid.NewGuid().ToString("N")+".csv");try{await Task.Run(()=>ReportExport.Csv(data,path),ct);byte[] bytes=await File.ReadAllBytesAsync(path,ct);if(bytes.Length>32*1024*1024)throw new InvalidOperationException("CSV 32 MB sınırını aşıyor.");
            using var audit=w.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,ActorAccountId,CorrelationId,SqlPrincipal,Source)
 VALUES(@actor,N'ProjectBudget.Export',N'Project',@id,N'Export',CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',@details,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application')");audit.Text("@actor",user.Username);audit.Text("@id",project.ToString("D"),100);audit.Text("@details",JsonSerializer.Serialize(new{Section=section,Rows=source.Rows.Count,ReadUtc=report.ReadUtc,Scope="AllProjectLife"}),-1);await audit.ExecuteNonQueryAsync(ct);await deliver(bytes,ct);await w.CommitAsync(ct);
        }finally{if(File.Exists(path))File.Delete(path);}
    }
}
