using System.Data;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed partial class PaymentAllocationService
{
    private const string PaymentsPageSql=@"SELECT TOP(500) P.LedgerId PaymentLedgerId,CONVERT(nvarchar(36),P.RequestId) PaymentReference,P.PersonId,P.PersonnelGroup,L.EmploymentId,P.PaymentPurpose,P.CreatedUtc,-P.Delta PaymentAmount,
 -P.Delta-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.PaymentLedgerId=P.LedgerId)),0) PaymentAvailable,
 COALESCE(C.AD,J.AD)+N' '+COALESCE(C.SOYAD,J.SOYAD) PersonCaption
 FROM dbo.PaymentLedger P JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 LEFT JOIN dbo.Calisan C ON C.PersonId=P.PersonId AND P.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=P.PersonId AND P.PersonnelGroup=1
 WHERE P.ProjectId=@project AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL AND P.RequestId IS NOT NULL AND P.Delta<0 AND P.PaymentPurpose IN(N'Advance',N'Settlement') ORDER BY P.LedgerId DESC";
    private const string AccrualsPageSql=@"SELECT TOP(500) F.EntryId FinanceEntryId,F.Reference FinanceReference,F.PersonId,F.PersonnelGroup,L.EmploymentId,F.PeriodStart,F.PeriodEnd,F.Amount OriginalAmount,F.AdjustmentAmount,F.EffectiveAmount AccrualAmount,
 F.EffectiveAmount-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.FinanceEntryId=F.EntryId)),0) AccrualAvailable
 FROM dbo.FinanceEffectiveEntries F JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) WHERE F.ProjectId=@project AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL ORDER BY F.CreatedUtc DESC,F.EntryId DESC";
    private const string AllocationsPageSql=@"SELECT TOP(500) A.AllocationId,A.PaymentLedgerId,F.Reference FinanceReference,P.PaymentPurpose,A.Amount,A.Reason,A.CreatedBy,A.CreatedUtc,R.ReleaseId,R.Reason ReleaseReason,R.ReleasedBy,R.ReleasedUtc
 FROM dbo.PaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId JOIN dbo.FinanceEntries F ON F.EntryId=A.FinanceEntryId LEFT JOIN dbo.PaymentAllocationReleases R ON R.AllocationId=A.AllocationId WHERE P.ProjectId=@project ORDER BY A.CreatedUtc DESC,A.AllocationId DESC";
    internal async Task<Wire.Page> PageAsync(Wire.AllocationBrowse input,CancellationToken ct)
    {
        var scope=new FinancialQueryScope(input.Filter);FinancialQueryScope.Page(input.Index,input.Size);
        string sql=input.Section switch{"Payments"=>PaymentsPageSql,"Accruals"=>AccrualsPageSql,"Allocations"=>AllocationsPageSql,_=>throw new ArgumentException("Tahsis liste bölümü geçersiz.")};
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.Page>("payment-allocations/browse",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,false,ct);
        if(input.Section=="Payments")sql=sql.Replace("P.ProjectId=@project AND P.Kind", "P.ProjectId=@project AND ("+scope.Predicate("P")+") AND P.Kind");
        else if(input.Section=="Accruals")sql=sql.Replace("F.ProjectId=@project AND F.Kind", "F.ProjectId=@project AND ("+scope.Predicate("F")+") AND @selected IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.PaymentLedger SP JOIN dbo.EmploymentRecordLinks SL ON SL.RecordKind=N'Ledger' AND SL.RecordKey=CONVERT(nvarchar(64),SP.LedgerId) WHERE SP.LedgerId=@selected AND SP.ProjectId=@project AND SP.PersonId=F.PersonId AND SP.PersonnelGroup=F.PersonnelGroup AND SL.EmploymentId=L.EmploymentId AND SP.Kind=N'Payment' AND SP.ReversedUtc IS NULL AND ("+scope.Predicate("SP")+")) AND F.Kind");
        else sql=sql.Replace("WHERE P.ProjectId=@project ORDER BY", "WHERE P.ProjectId=@project AND F.ProjectId=@project AND ("+scope.Predicate("P","A")+") ORDER BY");
        void Bind(SqlCommand c){c.Parameters.Add("@project",SqlDbType.UniqueIdentifier).Value=input.ProjectId;scope.Bind(c);c.Parameters.Add("@selected",SqlDbType.BigInt).Value=(object?)input.PaymentLedgerId??DBNull.Value;}
        var result=await FinancialQueryScope.ReadPageAsync(w,sql,Bind,input.Index,input.Size,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(result.Rows),result.Info.Total,result.Info.Index,result.Info.Size);
    }
}
