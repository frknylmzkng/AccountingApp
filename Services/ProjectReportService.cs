using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class ProjectReportService(string connectionString, UserSession user)
{
    // The caller supplies only a project and a fixed report name. Data is built
    // by the server; arbitrary client tables are never accepted for scoped users.
    internal async Task DeliverCsvAsync(Guid project, string report, Func<byte[], CancellationToken, Task> deliver, CancellationToken ct,AccountingApp.Contracts.FinancialFilter? filter=null)
    {
        if(report.StartsWith("Fiscal.",StringComparison.Ordinal)){await DeliverFiscalCsvAsync(project,report[7..],deliver,ct,filter);return;}
        if(report.StartsWith("Budget.",StringComparison.Ordinal)){await DeliverBudgetCsvAsync(project,report[7..],deliver,ct,filter);return;}
        if(report.StartsWith("Workforce.",StringComparison.Ordinal)){await DeliverWorkforceCsvAsync(project,report[10..],deliver,ct,filter);return;}
        if(report.StartsWith("Reconciliation.",StringComparison.Ordinal)){await DeliverReconciliationCsvAsync(project,report[15..],deliver,ct,filter);return;}
        _ = AuditContext.ValidateReason(AuditContext.Reason ?? "");
        if (report is not ("Assignments" or "Costs")) throw new ArgumentException("Rapor türü geçersiz.");
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct);
        await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Export, ct);
        if (report == "Costs") await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Costs, ct);
        string sql = report == "Assignments" ? @"SELECT TOP(10001) P.Code ProjeKodu,P.Name ProjeAdi,COALESCE(C.AD,J.AD) Ad,COALESCE(C.SOYAD,J.SOYAD) Soyad,
 T.Name Ekip,CASE WHEN A.IsSupplementary=1 THEN N'Gün içi ek' ELSE N'Asıl' END AtamaTuru,A.StartDate Baslangic,A.EndDate Bitis,CASE WHEN A.CancelledUtc IS NULL THEN N'Geçerli' ELSE N'İptal' END Durum
 FROM dbo.PersonnelProjectAssignments A JOIN dbo.FinanceProjects P ON P.ProjectId=A.ProjectId
 LEFT JOIN dbo.Calisan C ON C.PersonId=A.PersonId AND A.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=A.PersonId AND A.PersonnelGroup=1
 LEFT JOIN dbo.ProjectTeams T ON T.TeamId=A.TeamId WHERE A.ProjectId=@id ORDER BY A.StartDate,A.AssignmentId"
 : @"SELECT TOP(10001) P.Code ProjeKodu,P.Name ProjeAdi,F.Kind Tur,F.Reference BelgeNo,F.PeriodStart Baslangic,F.PeriodEnd Bitis,F.Amount IlkTutarTL,F.AdjustmentAmount DuzeltmeTL,F.EffectiveAmount TutarTL,F.Description Aciklama,
 CASE WHEN F.ReversedUtc IS NULL THEN N'Geçerli' ELSE N'İptal' END Durum FROM dbo.FinanceEffectiveEntries F JOIN dbo.FinanceProjects P ON P.ProjectId=F.ProjectId WHERE F.ProjectId=@id ORDER BY F.CreatedUtc,F.EntryId";
        using var c = w.Command(sql); c.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = project;
        using var data = await c.TableAsync(ct);
        if (data.Rows.Count > 10000) throw new InvalidOperationException("Rapor 10.000 satırı aşıyor; daha küçük rapor kapsamı için merkez yöneticisine başvurun.");
        data.ExtendedProperties["Export.ReportTitle"] = report == "Assignments" ? "Seçili projenin atama geçmişi" : "Seçili projenin hakediş/gider belgeleri (iptaller dahil)";
        string path = Path.Combine(Path.GetTempPath(), "AccountingApp-project-" + Guid.NewGuid().ToString("N") + ".csv");
        try
        {
            await Task.Run(() => ReportExport.Csv(data, path), ct);
            using var audit = w.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,ActorAccountId,CorrelationId,SqlPrincipal,Source)
 VALUES(@actor,N'Project.Export',N'Project',@id,N'Export',CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',@details,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application')");
            audit.Text("@actor", user.Username); audit.Text("@id", project.ToString("D"), 100); audit.Text("@details", System.Text.Json.JsonSerializer.Serialize(new { Report = report, Rows = data.Rows.Count, Format = "CSV" }), -1); await audit.ExecuteNonQueryAsync(ct);
            byte[] bytes = await File.ReadAllBytesAsync(path, ct);
            if (bytes.Length > 32 * 1024 * 1024) throw new InvalidOperationException("Rapor boyut sınırını aşıyor.");
            // Keep account and project grant read leases until authorized bytes
            // leave the server (or the local file is atomically published).
            await deliver(bytes, ct); await w.CommitAsync(ct);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    internal async Task SaveCsvAsync(Guid project, string report, string destination, CancellationToken ct,AccountingApp.Contracts.FinancialFilter? filter=null)
    {
        string path = Path.GetFullPath(destination);
        if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("CSV dosyası seçin.");
        async Task Publish(byte[] bytes, CancellationToken token)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".AccountingApp-project-" + Guid.NewGuid().ToString("N") + ".csv");
            try { await File.WriteAllBytesAsync(temporary, bytes, token); token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        if (DesktopConnection.IsRemote(connectionString))
            await Publish(await RemoteApi.For(connectionString, user).ProjectCsvAsync(new(project, report, RemoteApi.Reason,filter), ct), ct);
        else await DeliverCsvAsync(project, report, Publish, ct,filter);
    }
}
