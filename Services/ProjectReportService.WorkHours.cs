using System.Data;
using System.Text.Json;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed partial class ProjectReportService
{
    internal static readonly IReadOnlyDictionary<string,string> WorkforceSections=new Dictionary<string,string>{{"Summary","Proje özeti"}}.Concat(ProjectWorkHoursService.SectionNames).ToDictionary(p=>p.Key,p=>p.Value);
    internal static string WorkforceTitle(string name)=>WorkforceTitles.TryGetValue(name,out var title)?title:ReconciliationTitle(name);
    private static readonly Dictionary<string,string> WorkforceTitles=new()
    {
        ["TimeZoneId"]="Kaynak saat dilimi",["PolicyVersion"]="Kaynak süre ilkesi",["WorkDate"]="İş günü",["PlannedMinutes"]="Atanmış plan dk",["ApprovedMinutes"]="Proje onaylı net dk",["PendingMinutes"]="Onay bekleyen net dk",["DraftMinutes"]="Taslak/iade net dk",["ReadyPayableMinutes"]="Tam onaylı ücret dk",["UnscheduledMinutes"]="Vardiyasız onaylı net dk",["ForeignPlanMinutes"]="Başka proje planından onaylı net dk",["VarianceMinutes"]="Onaylı net − atanmış plan dk",
        ["PlannedHours"]="Atanmış plan kişi-saat",["ApprovedHours"]="Proje onaylı net kişi-saat",["PendingHours"]="Onay bekleyen kişi-saat",["DraftHours"]="Taslak/iade kişi-saat",["ReadyPayableHours"]="Tam onaylı ücret kişi-saat",["UnscheduledHours"]="Vardiyasız onaylı kişi-saat",["ForeignPlanHours"]="Başka proje planından onaylı kişi-saat",["VarianceHours"]="Onaylı net − atanmış plan kişi-saat",
        ["RequiredMinutes"]="Proje kadro ihtiyacı dk",["RequiredHours"]="Proje kadro ihtiyacı kişi-saat",["PublishedShiftCount"]="Projedeki yayımlanmış vardiya",["AssignedSlots"]="Seçili kişi-vardiya ataması",["DistinctPeople"]="Plan/kaynakta görülen farklı kişi",["UnfilledSlots"]="Atanmamış kişi-vardiya kontenjanı",["UnfilledMinutes"]="Atanmamış ihtiyaç dk",["MissingTimesheet"]="Etkin puantajı olmayan kişi-plan",["IncompleteCount"]="Süresi eksik/geçersiz kaynak",["ShiftReference"]="Plan kimliği",["Name"]="Vardiya adı",["Site"]="Saha",["Status"]="Plan durumu",["LocalStart"]="Yerel başlangıç",["LocalEnd"]="Yerel bitiş",["BreakMinutes"]="Mola dk",["RequiredCount"]="Gerekli kişi",["NetPlanMinutes"]="Plan kişi başı net dk",["PerPersonPlannedHours"]="Plan kişi başı net saat",["AllAssignedCount"]="Tüm aktif kişi ataması",["SelectedAssignedCount"]="Seçili kişi ataması",
        ["SourceKind"]="Gerçekleşme kaynak türü",["SourceReference"]="Kaynak kimliği",["TimesheetReference"]="Ana puantaj kimliği",["ParentState"]="Ana puantaj durumu",["EffectiveState"]="Proje onay durumu",["NetMinutes"]="Kaynak net dk",["PayableMinutes"]="Kaynak ücret dk",["ForeignPlan"]="Başka proje planına bağlı",["IsUnscheduled"]="Vardiyasız kaynak",["PersonCaption"]="Güncel karttaki ad"
    };
    internal static string WorkforceState(object? value)=>value?.ToString() switch{"Whole"=>"Ana puantaj","Segment"=>"Proje zaman parçası","Published"=>"Yayımlanmış","Draft"=>"Taslak","Submitted"=>"Gönderilmiş","Approved"=>"Onaylı","Pending"=>"Proje onayı bekliyor","Returned"=>"İade","Cancelled"=>"İptal","Unresolved"=>"Çözümlenmemiş onay",var x=>x??""};
    private async Task DeliverWorkforceCsvAsync(Guid project,string section,Func<byte[],CancellationToken,Task> deliver,CancellationToken ct,Wire.FinancialFilter? filter)
    {
        if(!WorkforceSections.TryGetValue(section,out var title))throw new ArgumentException("Kişi-saat rapor bölümü geçersiz.");_ =AuditContext.ValidateReason(AuditContext.Reason??"");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);var report=await ProjectWorkHoursService.BuildAsync(w,user,new(project,filter??new()),ct,section);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Export,ct);
        var source=section=="Summary"?report.Summary:report.Sections[section].Rows;
        using var data=new DataTable();foreach(var name in new[]{"Proje","Proje kimliği","Veri zamanı UTC","Raporu oluşturan","Kapsam","Bölüm","Hesap ilkesi"})data.Columns.Add(name,typeof(string));
        foreach(DataColumn c in source.Columns)data.Columns.Add(WorkforceTitle(c.ColumnName),c.ColumnName=="PersonnelGroup"?typeof(string):c.DataType);
        void Add(DataRow? row){var values=new object[source.Columns.Count+7];values[0]=report.Caption;values[1]=project.ToString("D");values[2]=report.ReadUtc.ToString("O");values[3]=report.ReadBy;values[4]=report.Scope+"; tüm filtre sonuçları";values[5]=title;values[6]="Plan: yerel net dakika; gerçekleşme: UTC net dakika; saat toplam dakikadan; proje onayı ve tam onay ayrı";for(int i=0;i<source.Columns.Count;i++){object value=row?[i]??DBNull.Value;string name=source.Columns[i].ColumnName;if(name=="PersonnelGroup"&&value is int g)value=g==0?"Genel":"Joker";if(name is "Status" or "SourceKind" or "ParentState" or "EffectiveState"&&value!=DBNull.Value)value=WorkforceState(value);values[i+7]=value;}data.Rows.Add(values);}
        foreach(DataRow row in source.Rows)Add(row);if(source.Rows.Count==0)Add(null);
        string path=Path.Combine(Path.GetTempPath(),"AccountingApp-work-hours-"+Guid.NewGuid().ToString("N")+".csv");
        try
        {
            await Task.Run(()=>ReportExport.Csv(data,path),ct);byte[] bytes=await File.ReadAllBytesAsync(path,ct);if(bytes.Length>32*1024*1024)throw new InvalidOperationException("CSV dosyası 32 MB sınırını aşıyor.");
            using var audit=w.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,ActorAccountId,CorrelationId,SqlPrincipal,Source)
 VALUES(@actor,N'Project.WorkHours.Export',N'Project',@id,N'Export',CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',@details,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application')");audit.Text("@actor",user.Username);audit.Text("@id",project.ToString("D"),100);audit.Text("@details",JsonSerializer.Serialize(new{Section=section,Rows=source.Rows.Count,Scope=report.Scope,ReadUtc=report.ReadUtc}),-1);await audit.ExecuteNonQueryAsync(ct);await deliver(bytes,ct);await w.CommitAsync(ct);
        }
        finally{if(File.Exists(path))File.Delete(path);}
    }
}
