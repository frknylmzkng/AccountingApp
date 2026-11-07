using System.Data;
using System.Text.Json;
namespace WinFormsApp2;

internal sealed partial class ProjectReportService
{
    internal static readonly IReadOnlyDictionary<string,string> ReconciliationSections=new Dictionary<string,string>{["Summary"]="Proje özeti",["People"]="Kişi / çalışma dönemi",["Accruals"]="Hakediş belgeleri",["Payments"]="Ödeme ve avans",["Allocations"]="Tahsis geçmişi",["Issues"]="İstisnalar"};
    internal static string ReconciliationTitle(string column)=>ReconciliationTitles.TryGetValue(column,out var title)?title:column;
    private static readonly Dictionary<string,string> ReconciliationTitles=new()
    {
        ["FromOutsideScope"]="Kapsam dışı ödemeden tahsis TL",["ToOutsideScope"]="Kapsam dışı hakedişe tahsis TL",["UnexplainedDifference"]="Kapsam geçişi sonrası açıklanamayan fark TL",["AllocatedAdvance"]="Avanstan aktif tahsis TL",["AllocatedSettlement"]="Hakediş ödemesinden aktif tahsis TL",["OpenAdvance"]="Tahsis edilmemiş avans TL",["OpenSettlement"]="Tahsis edilmemiş hakediş ödemesi TL",["OriginalAccrual"]="İlk hakediş TL",["CorrectionAmount"]="Aktarılmış düzeltme TL",["AccrualAmount"]="Net hakediş TL",["PaymentAmount"]="Ödeme çıkışı TL",["AdvanceAmount"]="Avans çıkışı TL",["SettlementAmount"]="Hakediş ödeme çıkışı TL",["OtherPaymentAmount"]="Sınıflandırılmamış ödeme TL",["AllocatedAccrual"]="Hakedişe aktif tahsis TL",["AllocatedPayment"]="Ödemeden aktif tahsis TL",["OpenAccrual"]="Tahsis edilmemiş hakediş TL",["OpenPayment"]="Tahsis edilmemiş ödeme TL",["NetPosition"]="Hakediş eksi ödeme TL",["ReconciliationDifference"]="Tahsis kapsamları farkı TL",
        ["PersonReference"]="Kalıcı kişi no",["EmploymentReference"]="Çalışma dönemi no",["PersonCaption"]="Kaynak hesaptaki ad",["PersonnelGroup"]="Personel grubu",["StartDate"]="İşe giriş",["EndDate"]="Ayrılış",["IsClosed"]="Çalışma dönemi kapalı",["FinanceReferenceId"]="Mali belge kimliği",["FinanceReference"]="Belge no",["PeriodStart"]="Kaynak başlangıç",["PeriodEnd"]="Kaynak bitiş",["CreatedUtc"]="Kayıt UTC",["CreatedBy"]="Kaydeden",["Actor"]="Ödemeyi kaydeden",["ReversedUtc"]="İptal UTC",["ReversedBy"]="İptal eden",["PaymentLedgerId"]="Ödeme defter no",["PaymentReference"]="Ödeme işlem no",["PaymentPurpose"]="Ödeme türü",["AllocationEligible"]="Tahsis kapsamına uygun",["AllocationReference"]="Tahsis no",["AllocatedAmount"]="Tahsis tutarı TL",["Reason"]="Tahsis gerekçesi",["ReleasedUtc"]="Serbest bırakma UTC",["ReleasedBy"]="Serbest bırakan",["ReleaseReason"]="Serbest bırakma gerekçesi",["IsActive"]="Tahsis aktif",["SourceValid"]="Kaynaklar halen geçerli",["Issue"]="İstisna",["RecordReference"]="Kaynak kayıt no",["ActiveAccrualAmount"]="Özete giren hakediş TL",["ActivePaymentAmount"]="Özete giren ödeme TL"
    };
    private async Task DeliverReconciliationCsvAsync(Guid project,string section,Func<byte[],CancellationToken,Task> deliver,CancellationToken ct,AccountingApp.Contracts.FinancialFilter? filter=null)
    {
        if(!ReconciliationSections.TryGetValue(section,out var title))throw new ArgumentException("Mutabakat bölümü geçersiz.");_ = AuditContext.ValidateReason(AuditContext.Reason??"");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        // Keep the same outer lock order as report preview and financial commands.
        var report=await ProjectReconciliationService.BuildAsync(w,user,project,ct,new(filter??new(),ExportSection:section));
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Export,ct);
        var source=section switch{"Summary"=>report.Summary,"People"=>report.People,"Accruals"=>report.Accruals,"Payments"=>report.Payments,"Allocations"=>report.Allocations,_=>report.Issues};
        using var data=new DataTable();data.Columns.Add("Proje",typeof(string));data.Columns.Add("Proje kimliği",typeof(string));data.Columns.Add("Veri zamanı UTC",typeof(string));data.Columns.Add("Raporu oluşturan",typeof(string));data.Columns.Add("Kapsam",typeof(string));data.Columns.Add("Bölüm",typeof(string));
        foreach(DataColumn c in source.Columns)data.Columns.Add(ReconciliationTitle(c.ColumnName),c.ColumnName=="PersonnelGroup"?typeof(string):c.DataType);
        void Add(DataRow? row){var values=new object[source.Columns.Count+6];values[0]=report.Caption;values[1]=project.ToString("D");values[2]=report.ReadUtc.ToString("O");values[3]=report.ReadBy;values[4]=report.Scope+"; seçili bölümün filtreye uyan tüm satırları";values[5]=title;for(int i=0;i<source.Columns.Count;i++){var value=row?[i]??DBNull.Value;if(source.Columns[i].ColumnName=="PaymentPurpose")value=value.ToString() switch{"Advance"=>"Avans","Settlement"=>"Hakediş ödemesi",_=>value};if(source.Columns[i].ColumnName=="PersonnelGroup"&&value is int group)value=group==0?"Genel":"Joker";values[i+6]=value;}data.Rows.Add(values);}
        foreach(DataRow row in source.Rows)Add(row);if(source.Rows.Count==0)Add(null);
        string path=Path.Combine(Path.GetTempPath(),"AccountingApp-reconciliation-"+Guid.NewGuid().ToString("N")+".csv");
        try
        {
            await Task.Run(()=>ReportExport.Csv(data,path),ct);byte[] bytes=await File.ReadAllBytesAsync(path,ct);if(bytes.Length>32*1024*1024)throw new InvalidOperationException("Rapor dosyası 32 MB sınırını aşıyor.");
            using var audit=w.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,ActorAccountId,CorrelationId,SqlPrincipal,Source)
 VALUES(@actor,N'Project.Reconciliation.Export',N'Project',@id,N'Export',CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',@details,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application')");audit.Text("@actor",user.Username);audit.Text("@id",project.ToString("D"),100);audit.Text("@details",JsonSerializer.Serialize(new{Section=section,Rows=source.Rows.Count,ReadUtc=report.ReadUtc,Scope=report.Scope}),-1);await audit.ExecuteNonQueryAsync(ct);await deliver(bytes,ct);await w.CommitAsync(ct);
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
}
