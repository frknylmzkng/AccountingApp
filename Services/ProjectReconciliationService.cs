using System.Data;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record ProjectReconciliationData(DataTable Summary,DataTable People,DataTable Accruals,DataTable Payments,DataTable Allocations,DataTable Issues,DateTime ReadUtc,string Caption,string ReadBy)
{
    internal Dictionary<string,Wire.PageInfo> Pages{get;init;}=new();
    internal string Scope{get;init;}="";
    internal Wire.ProjectReconciliation Encode()=>new(RemoteApi.Encode(Summary),RemoteApi.Encode(People),RemoteApi.Encode(Accruals),RemoteApi.Encode(Payments),RemoteApi.Encode(Allocations),RemoteApi.Encode(Issues),ReadUtc,Caption,ReadBy,Pages,Scope);
}
internal sealed class ProjectReconciliationService(string connectionString,UserSession user)
{
    // Aggregate each source before combining it. Joining raw payments/allocations to accruals would multiply money.
    private const string Sources=@"WITH F AS(
 SELECT F.*,L.EmploymentId FROM dbo.FinanceEffectiveEntries F LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId)
 WHERE F.ProjectId=@project AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL),
 P AS(SELECT P.*,L.EmploymentId FROM dbo.PaymentLedger P LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 WHERE P.ProjectId=@project AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL),
 FA AS(SELECT A.FinanceEntryId,CONVERT(decimal(28,2),SUM(A.Amount)) Amount FROM dbo.ActivePaymentAllocations A JOIN F ON F.EntryId=A.FinanceEntryId GROUP BY A.FinanceEntryId),
 PA AS(SELECT A.PaymentLedgerId,CONVERT(decimal(28,2),SUM(A.Amount)) Amount FROM dbo.ActivePaymentAllocations A JOIN P ON P.LedgerId=A.PaymentLedgerId GROUP BY A.PaymentLedgerId) ";
    private static async Task<DataTable> QueryAsync(IUnitOfWork w,Guid project,string sql,CancellationToken ct,ReconciliationQuery query,string section)
    {
        var scope=new FinancialQueryScope(query.Filter);
        sql=sql.Replace("F.ProjectId=@project AND F.Kind=N'Accrual'","F.ProjectId=@project AND ("+scope.Predicate("F")+") AND F.Kind=N'Accrual'")
            .Replace("P.ProjectId=@project AND P.Kind=N'Payment'","P.ProjectId=@project AND ("+scope.Predicate("P")+") AND P.Kind=N'Payment'")
            .Replace("WHERE P.ProjectId=@project AND F.ProjectId=@project ORDER BY","WHERE P.ProjectId=@project AND F.ProjectId=@project AND (("+scope.Predicate("P")+") OR ("+scope.Predicate("F")+")) ORDER BY")
            .Replace("(P.ProjectId=@project OR F.ProjectId=@project) AND (","((P.ProjectId=@project AND ("+scope.Predicate("P")+")) OR (F.ProjectId=@project AND ("+scope.Predicate("F")+"))) AND (");
        void Bind(SqlCommand c){c.Parameters.Add("@project",SqlDbType.UniqueIdentifier).Value=project;scope.Bind(c);}
        if(section=="Summary"){using var c=w.Command(sql);Bind(c);var table=await c.TableAsync(ct);query.Results[section]=new(table.Rows.Count,0,1);return table;}
        var page=query.Page(section);var result=await FinancialQueryScope.ReadPageAsync(w,sql,Bind,page.Index,page.Size,ct,query.ExportSection==section);query.Results[section]=result.Info;return result.Rows;
    }
    internal async Task<Wire.ProjectReconciliation> ReadAsync(Guid project,CancellationToken ct,ReconciliationQuery? query=null)
    {
        query??=new(new());_ = new FinancialQueryScope(query.Filter);
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.ProjectReconciliation>("project-reconciliation/browse",new Wire.ReconciliationBrowse(project,query.Filter,query.Pages),ct);
        for(int attempt=0;;attempt++){try{await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);var report=await BuildAsync(w,user,project,ct,query);await w.CommitAsync(ct);return report.Encode();}catch(SqlException ex)when(ex.Number==1205&&attempt<2){await Task.Delay(150*(attempt+1),ct);}}
    }
    internal static async Task<ProjectReconciliationData> BuildAsync(IUnitOfWork w,UserSession user,Guid project,CancellationToken ct,ReconciliationQuery? query=null)
    {
        query??=new(new());query.Results.Clear();var scope=new FinancialQueryScope(query.Filter);
        await TimesheetService.AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Costs,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Payments,ct);
        string caption;using(var p=w.Command("SELECT Code+N' · '+Name FROM dbo.FinanceProjects WHERE ProjectId=@project")){p.Parameters.Add("@project",SqlDbType.UniqueIdentifier).Value=project;caption=(string)(await p.ExecuteScalarAsync(ct)??throw new InvalidOperationException("Proje bulunamadı."));}
        var summary=await QueryAsync(w,project,Sources+@", FT AS(SELECT COALESCE(CONVERT(decimal(28,2),SUM(Amount)),0) OriginalAccrual,COALESCE(CONVERT(decimal(28,2),SUM(AdjustmentAmount)),0) CorrectionAmount,COALESCE(CONVERT(decimal(28,2),SUM(EffectiveAmount)),0) AccrualAmount FROM F),
 PT AS(SELECT COALESCE(CONVERT(decimal(28,2),SUM(-Delta)),0) PaymentAmount,
 COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN PaymentPurpose=N'Advance' THEN -Delta ELSE 0 END)),0) AdvanceAmount,
 COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN PaymentPurpose=N'Settlement' THEN -Delta ELSE 0 END)),0) SettlementAmount,
 COALESCE(CONVERT(decimal(28,2),SUM(CASE WHEN PaymentPurpose NOT IN(N'Advance',N'Settlement') THEN -Delta ELSE 0 END)),0) OtherPaymentAmount FROM P),
 AT AS(SELECT COALESCE((SELECT CONVERT(decimal(28,2),SUM(Amount)) FROM FA),0) AllocatedAccrual,COALESCE((SELECT CONVERT(decimal(28,2),SUM(Amount)) FROM PA),0) AllocatedPayment,
 COALESCE((SELECT CONVERT(decimal(28,2),SUM(A.Amount)) FROM dbo.ActivePaymentAllocations A JOIN P ON P.LedgerId=A.PaymentLedgerId WHERE P.PaymentPurpose=N'Advance'),0) AllocatedAdvance,
 COALESCE((SELECT CONVERT(decimal(28,2),SUM(A.Amount)) FROM dbo.ActivePaymentAllocations A JOIN P ON P.LedgerId=A.PaymentLedgerId WHERE P.PaymentPurpose=N'Settlement'),0) AllocatedSettlement,
 COALESCE((SELECT CONVERT(decimal(28,2),SUM(A.Amount)) FROM dbo.ActivePaymentAllocations A JOIN F ON F.EntryId=A.FinanceEntryId JOIN dbo.PaymentLedger B ON B.LedgerId=A.PaymentLedgerId WHERE B.ProjectId=@project AND B.PersonId=F.PersonId AND B.PersonnelGroup=F.PersonnelGroup AND B.ReversedUtc IS NULL AND NOT EXISTS(SELECT 1 FROM P WHERE P.LedgerId=B.LedgerId)),0) FromOutsideScope,
 COALESCE((SELECT CONVERT(decimal(28,2),SUM(A.Amount)) FROM dbo.ActivePaymentAllocations A JOIN P ON P.LedgerId=A.PaymentLedgerId JOIN dbo.FinanceEntries B ON B.EntryId=A.FinanceEntryId WHERE B.ProjectId=@project AND B.PersonId=P.PersonId AND B.PersonnelGroup=P.PersonnelGroup AND B.ReversedUtc IS NULL AND NOT EXISTS(SELECT 1 FROM F WHERE F.EntryId=B.EntryId)),0) ToOutsideScope)
 SELECT FT.*,PT.*,AT.*,FT.AccrualAmount-AT.AllocatedAccrual OpenAccrual,PT.PaymentAmount-AT.AllocatedPayment OpenPayment,
 PT.AdvanceAmount-AT.AllocatedAdvance OpenAdvance,PT.SettlementAmount-AT.AllocatedSettlement OpenSettlement,FT.AccrualAmount-PT.PaymentAmount NetPosition,AT.AllocatedPayment-AT.AllocatedAccrual ReconciliationDifference,AT.AllocatedPayment-AT.AllocatedAccrual-(AT.ToOutsideScope-AT.FromOutsideScope) UnexplainedDifference FROM FT CROSS JOIN PT CROSS JOIN AT",ct,query,"Summary");
        var people=await QueryAsync(w,project,Sources+@", M AS(
 SELECT F.PersonId,F.EmploymentId,F.PersonnelGroup,CONVERT(decimal(28,2),F.Amount) OriginalAccrual,CONVERT(decimal(28,2),F.AdjustmentAmount) CorrectionAmount,CONVERT(decimal(28,2),F.EffectiveAmount) AccrualAmount,
 CONVERT(decimal(28,2),0) PaymentAmount,CONVERT(decimal(28,2),0) AdvanceAmount,CONVERT(decimal(28,2),0) SettlementAmount,CONVERT(decimal(28,2),0) OtherPaymentAmount,COALESCE(FA.Amount,0) AllocatedAccrual,CONVERT(decimal(28,2),0) AllocatedPayment
 FROM F LEFT JOIN FA ON FA.FinanceEntryId=F.EntryId
 UNION ALL SELECT P.PersonId,P.EmploymentId,P.PersonnelGroup,0,0,0,-P.Delta,CASE WHEN P.PaymentPurpose=N'Advance' THEN -P.Delta ELSE 0 END,CASE WHEN P.PaymentPurpose=N'Settlement' THEN -P.Delta ELSE 0 END,CASE WHEN P.PaymentPurpose NOT IN(N'Advance',N'Settlement') THEN -P.Delta ELSE 0 END,0,COALESCE(PA.Amount,0) FROM P LEFT JOIN PA ON PA.PaymentLedgerId=P.LedgerId),
 G AS(SELECT PersonId,EmploymentId,PersonnelGroup,CONVERT(decimal(28,2),SUM(OriginalAccrual)) OriginalAccrual,CONVERT(decimal(28,2),SUM(CorrectionAmount)) CorrectionAmount,CONVERT(decimal(28,2),SUM(AccrualAmount)) AccrualAmount,
 CONVERT(decimal(28,2),SUM(PaymentAmount)) PaymentAmount,CONVERT(decimal(28,2),SUM(AdvanceAmount)) AdvanceAmount,CONVERT(decimal(28,2),SUM(SettlementAmount)) SettlementAmount,CONVERT(decimal(28,2),SUM(OtherPaymentAmount)) OtherPaymentAmount,CONVERT(decimal(28,2),SUM(AllocatedAccrual)) AllocatedAccrual,CONVERT(decimal(28,2),SUM(AllocatedPayment)) AllocatedPayment FROM M GROUP BY PersonId,EmploymentId,PersonnelGroup)
 SELECT TOP(10001) CONVERT(nvarchar(36),G.PersonId) PersonReference,CONVERT(nvarchar(36),G.EmploymentId) EmploymentReference,
 COALESCE((SELECT TOP(1) L.PersonCaption FROM dbo.AccrualCalculationLines L JOIN dbo.AccrualCalculations B ON B.CalculationId=L.CalculationId WHERE B.ProjectId=@project AND L.PersonId=G.PersonId AND L.EmploymentId=G.EmploymentId ORDER BY L.WorkDate DESC,L.LineId),N'Ad snapshotı yok; kişi no ile eşleyin') PersonCaption,G.PersonnelGroup,E.StartDate,E.EndDate,E.IsClosed,
 G.OriginalAccrual,G.CorrectionAmount,G.AccrualAmount,G.SettlementAmount,G.AdvanceAmount,G.OtherPaymentAmount,G.PaymentAmount,G.AllocatedAccrual,G.AllocatedPayment,
 G.AccrualAmount-G.AllocatedAccrual OpenAccrual,G.PaymentAmount-G.AllocatedPayment OpenPayment,G.AccrualAmount-G.PaymentAmount NetPosition,G.AllocatedPayment-G.AllocatedAccrual ReconciliationDifference
 FROM G LEFT JOIN dbo.EmploymentPeriods E ON E.EmploymentId=G.EmploymentId ORDER BY PersonCaption,G.PersonId,G.EmploymentId,G.PersonnelGroup",ct,query,"People");
        var accruals=await QueryAsync(w,project,@"SELECT TOP(10001) CONVERT(nvarchar(36),F.EntryId) FinanceReferenceId,F.Reference FinanceReference,CONVERT(nvarchar(36),F.PersonId) PersonReference,CONVERT(nvarchar(36),L.EmploymentId) EmploymentReference,F.PersonnelGroup,F.PeriodStart,F.PeriodEnd,F.Amount OriginalAccrual,F.AdjustmentAmount CorrectionAmount,F.EffectiveAmount AccrualAmount,
 COALESCE(A.Amount,0) AllocatedAccrual,CASE WHEN F.ReversedUtc IS NULL THEN F.EffectiveAmount ELSE 0 END ActiveAccrualAmount,CASE WHEN F.ReversedUtc IS NULL THEN F.EffectiveAmount-COALESCE(A.Amount,0) ELSE 0 END OpenAccrual,F.CreatedUtc,F.CreatedBy,F.ReversedUtc,F.ReversedBy
 FROM dbo.FinanceEffectiveEntries F LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId)
 OUTER APPLY(SELECT CONVERT(decimal(28,2),SUM(Amount)) Amount FROM dbo.ActivePaymentAllocations WHERE FinanceEntryId=F.EntryId) A WHERE F.ProjectId=@project AND F.Kind=N'Accrual' ORDER BY F.CreatedUtc,F.EntryId",ct,query,"Accruals");
        var payments=await QueryAsync(w,project,@"SELECT TOP(10001) P.LedgerId PaymentLedgerId,CONVERT(nvarchar(36),P.RequestId) PaymentReference,CONVERT(nvarchar(36),P.PersonId) PersonReference,CONVERT(nvarchar(36),L.EmploymentId) EmploymentReference,P.PersonnelGroup,P.PaymentPurpose,-P.Delta PaymentAmount,
 COALESCE(A.Amount,0) AllocatedPayment,CASE WHEN P.ReversedUtc IS NULL THEN -P.Delta ELSE 0 END ActivePaymentAmount,CASE WHEN P.ReversedUtc IS NULL THEN -P.Delta-COALESCE(A.Amount,0) ELSE 0 END OpenPayment,P.CreatedUtc,P.Actor,P.ReversedUtc,
 CAST(CASE WHEN P.RequestId IS NOT NULL AND P.Delta<0 AND P.PersonId IS NOT NULL AND L.EmploymentId IS NOT NULL AND P.ReversedUtc IS NULL AND P.PaymentPurpose IN(N'Advance',N'Settlement') THEN 1 ELSE 0 END AS bit) AllocationEligible
 FROM dbo.PaymentLedger P LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 OUTER APPLY(SELECT CONVERT(decimal(28,2),SUM(Amount)) Amount FROM dbo.ActivePaymentAllocations WHERE PaymentLedgerId=P.LedgerId) A WHERE P.ProjectId=@project AND P.Kind=N'Payment' ORDER BY P.LedgerId",ct,query,"Payments");
        var allocations=await QueryAsync(w,project,@"SELECT TOP(10001) CONVERT(nvarchar(36),A.AllocationId) AllocationReference,A.PaymentLedgerId,F.Reference FinanceReference,CONVERT(nvarchar(36),P.PersonId) PersonReference,CONVERT(nvarchar(36),A.EmploymentId) EmploymentReference,P.PaymentPurpose,A.Amount AllocatedAmount,A.Reason,A.CreatedBy,A.CreatedUtc,R.ReleasedUtc,R.ReleasedBy,R.Reason ReleaseReason,
 CAST(CASE WHEN R.ReleaseId IS NULL THEN 1 ELSE 0 END AS bit) IsActive,
 CAST(CASE WHEN P.ProjectId=F.ProjectId AND P.PersonId=F.PersonId AND P.PersonnelGroup=F.PersonnelGroup AND P.ReversedUtc IS NULL AND F.ReversedUtc IS NULL AND P.Kind=N'Payment' AND F.Kind=N'Accrual'
 AND EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks L WHERE L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId) AND L.EmploymentId=A.EmploymentId)
 AND EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks L WHERE L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) AND L.EmploymentId=A.EmploymentId) THEN 1 ELSE 0 END AS bit) SourceValid
 FROM dbo.PaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId JOIN dbo.FinanceEntries F ON F.EntryId=A.FinanceEntryId LEFT JOIN dbo.PaymentAllocationReleases R ON R.AllocationId=A.AllocationId WHERE P.ProjectId=@project AND F.ProjectId=@project ORDER BY A.CreatedUtc,A.AllocationId",ct,query,"Allocations");
        // Issues carry only identifiers inside the authorized project; never expose the other project's identity or values.
        var issues=await QueryAsync(w,project,Sources+@"SELECT TOP(10001) Issue,RecordReference FROM(
 SELECT N'Hakedişte kişi/çalışma dönemi bağlantısı eksik' Issue,CONVERT(nvarchar(64),F.EntryId) RecordReference FROM F WHERE F.PersonId IS NULL OR F.EmploymentId IS NULL
 UNION ALL SELECT N'Ödemede kişi/çalışma dönemi bağlantısı eksik',CONVERT(nvarchar(64),P.LedgerId) FROM P WHERE P.PersonId IS NULL OR P.EmploymentId IS NULL
 UNION ALL SELECT N'Ödeme tahsis kapsamı dışında: tür, işlem kimliği veya tutar',CONVERT(nvarchar(64),P.LedgerId) FROM P WHERE P.PaymentPurpose NOT IN(N'Advance',N'Settlement') OR P.RequestId IS NULL OR P.Delta>=0
 UNION ALL SELECT N'Hakedişte aktif tahsis tutarı aşılıyor',CONVERT(nvarchar(64),F.EntryId) FROM F JOIN FA ON FA.FinanceEntryId=F.EntryId WHERE FA.Amount>F.EffectiveAmount
 UNION ALL SELECT N'Ödemede aktif tahsis tutarı aşılıyor',CONVERT(nvarchar(64),P.LedgerId) FROM P JOIN PA ON PA.PaymentLedgerId=P.LedgerId WHERE PA.Amount>-P.Delta
 UNION ALL SELECT N'Aktif tahsisin kaynak kapsamı veya durumu geçersiz',CONVERT(nvarchar(64),A.AllocationId) FROM dbo.ActivePaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId JOIN dbo.FinanceEntries F ON F.EntryId=A.FinanceEntryId WHERE (P.ProjectId=@project OR F.ProjectId=@project) AND (P.ProjectId<>F.ProjectId OR P.ProjectId IS NULL OR F.PersonId<>P.PersonId OR P.PersonnelGroup<>F.PersonnelGroup OR P.ReversedUtc IS NOT NULL OR F.ReversedUtc IS NOT NULL OR P.Kind<>N'Payment' OR F.Kind<>N'Accrual' OR P.PersonId IS NULL OR F.PersonId IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks L WHERE L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId) AND L.EmploymentId=A.EmploymentId) OR NOT EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks L WHERE L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) AND L.EmploymentId=A.EmploymentId))) X ORDER BY Issue,RecordReference",ct,query,"Issues");
        DateTime readUtc;using(var clock=w.Command("SELECT SYSUTCDATETIME()"))readUtc=DateTime.SpecifyKind((DateTime)(await clock.ExecuteScalarAsync(ct))!,DateTimeKind.Utc);
        return new(summary,people,accruals,payments,allocations,issues,readUtc,caption,user.Username){Pages=query.Results,Scope=scope.Description};
    }
}
