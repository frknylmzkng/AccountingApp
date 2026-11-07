using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class PaymentAllocationService(string connectionString,UserSession user)
{
    private static void Id(SqlCommand c,string name,Guid id)=>c.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=id;
    private static byte[] Hash(object input)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input,input.GetType()));
    private async Task AuthorizeAsync(IUnitOfWork w,Guid project,bool write,CancellationToken ct)
    {
        // Same outer gate as accrual corrections: quote/write cannot race a cost reduction.
        await TimesheetService.AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,write?ProjectAccess.ApprovePayment:ProjectAccess.Payments,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Costs,ct);
    }
    private static async Task<Guid> ActorAsync(IUnitOfWork w,CancellationToken ct){using var c=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");return (Guid)(await c.ExecuteScalarAsync(ct)??throw new SessionRevokedException());}
    internal static async Task DemandPaymentFreeAsync(IUnitOfWork w,long ledger,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.ActivePaymentAllocations WHERE PaymentLedgerId=@id");c.Parameters.Add("@id",SqlDbType.BigInt).Value=ledger;if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ödemeyi iptal etmeden önce Ödeme/avans tahsisi ekranında aktif tahsislerini serbest bırakın.");}
    internal static async Task DemandFinanceFreeAsync(IUnitOfWork w,Guid finance,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.ActivePaymentAllocations WHERE FinanceEntryId=@id");Id(c,"@id",finance);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Hakediş belgesini iptal etmeden önce aktif ödeme/avans tahsislerini serbest bırakın.");}
    private static async Task<DataTable> QuoteRowsAsync(IUnitOfWork w,Guid project,long payment,Guid finance,CancellationToken ct)
    {
        using var c=w.Command(@"SELECT P.LedgerId PaymentLedgerId,P.RequestId,P.PaymentPurpose,P.PersonId,P.PersonnelGroup,L.EmploymentId,F.EntryId FinanceEntryId,F.Reference FinanceReference,F.RecordVersion FinanceVersion,
 -P.Delta PaymentAmount,F.EffectiveAmount AccrualAmount,
 -P.Delta-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.PaymentLedgerId=P.LedgerId)),0) PaymentAvailable,
 F.EffectiveAmount-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.FinanceEntryId=F.EntryId)),0) AccrualAvailable
 FROM dbo.PaymentLedger P WITH(UPDLOCK,HOLDLOCK) JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 JOIN dbo.FinanceEffectiveEntries F ON F.EntryId=@finance AND F.ProjectId=P.ProjectId AND F.PersonId=P.PersonId AND F.PersonnelGroup=P.PersonnelGroup
 JOIN dbo.EmploymentRecordLinks E ON E.RecordKind=N'Finance' AND E.RecordKey=CONVERT(nvarchar(64),F.EntryId) AND E.EmploymentId=L.EmploymentId
 WHERE P.ProjectId=@project AND P.LedgerId=@payment AND P.Kind=N'Payment' AND P.Delta<0 AND P.RequestId IS NOT NULL AND P.ReversedUtc IS NULL AND P.PaymentPurpose IN(N'Advance',N'Settlement') AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL");Id(c,"@project",project);Id(c,"@finance",finance);c.Parameters.Add("@payment",SqlDbType.BigInt).Value=payment;var rows=await c.TableAsync(ct);if(rows.Rows.Count!=1)throw new InvalidOperationException("İptal edilmemiş ödeme ve hakediş aynı kişi, proje, grup ve çalışma dönemine ait olmalı. Listeyi yenileyip yeniden seçin.");return rows;
    }
    private static string Fingerprint(Guid project,DataTable rows)=>Convert.ToHexString(Hash(new{Project=project,Rows=RemoteApi.Encode(rows)}));
    internal async Task<Wire.AccrualPostingQuote> QuoteAsync(Guid project,long payment,Guid finance,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.AccrualPostingQuote>($"payment-allocations/{project:D}/quote?payment={payment}&finance={finance:D}",null,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,true,ct);await ProjectFinanceService.DemandProjectAsync(w,project,ct);var rows=await QuoteRowsAsync(w,project,payment,finance,ct);var r=rows.Rows[0];string[] issues=r.Field<decimal>("PaymentAvailable")<=0||r.Field<decimal>("AccrualAvailable")<=0?["Ödeme veya hakedişin tahsis edilebilir tutarı kalmamış."]:[];await w.CommitAsync(ct);return new(RemoteApi.Encode(rows),Fingerprint(project,rows),issues.Length==0,issues);
    }
    internal async Task CreateAsync(Wire.PaymentAllocationCreate input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("payment-allocations/create",input,ct);return;}
        if(input.AllocationId==Guid.Empty||input.Amount<=0||input.Amount>9999999999999999.99m||decimal.Round(input.Amount,2)!=input.Amount||input.Fingerprint is not{Length:64})throw new ArgumentException("Pozitif iki ondalıklı tutar ve güncel tahsis önizlemesi gerekiyor.");string reasonText=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(reasonText);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=Hash(input);
        using(var old=w.Command("SELECT CreatedByAccountId,PayloadHash FROM dbo.PaymentAllocations WHERE AllocationId=@id")){Id(old,"@id",input.AllocationId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){var r=t.Rows[0];if(r.Field<Guid>("CreatedByAccountId")!=actor||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Tahsis işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        await ProjectFinanceService.DemandProjectAsync(w,input.ProjectId,ct);var rows=await QuoteRowsAsync(w,input.ProjectId,input.PaymentLedgerId,input.FinanceEntryId,ct);if(Fingerprint(input.ProjectId,rows)!=input.Fingerprint)throw new InvalidOperationException("Ödeme, hakediş veya tahsis tutarları değişmiş; yeni önizleme oluşturun.");var source=rows.Rows[0];if(input.Amount>source.Field<decimal>("PaymentAvailable")||input.Amount>source.Field<decimal>("AccrualAvailable"))throw new InvalidOperationException("Tutar ödeme veya hakedişin tahsis edilmemiş kısmını aşıyor.");
        using var c=w.Command("INSERT dbo.PaymentAllocations(AllocationId,PaymentLedgerId,FinanceEntryId,EmploymentId,Amount,Reason,CreatedByAccountId,CreatedBy,PayloadHash) VALUES(@id,@payment,@finance,@employment,@amount,@reason,@actor,@name,@hash)");Id(c,"@id",input.AllocationId);Id(c,"@finance",input.FinanceEntryId);Id(c,"@employment",source.Field<Guid>("EmploymentId"));Id(c,"@actor",actor);c.Parameters.Add("@payment",SqlDbType.BigInt).Value=input.PaymentLedgerId;c.Money("@amount",input.Amount);c.Text("@reason",reasonText,500);c.Text("@name",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    internal async Task ReleaseAsync(Wire.PaymentAllocationRelease input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("payment-allocations/release",input,ct);return;}
        if(input.ReleaseId==Guid.Empty)throw new ArgumentException("Serbest bırakma işlem kimliği gerekiyor.");string reasonText=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(reasonText);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=Hash(input);
        using(var source=w.Command("SELECT COUNT(*) FROM dbo.PaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId WHERE A.AllocationId=@id AND P.ProjectId=@project")){Id(source,"@id",input.AllocationId);Id(source,"@project",input.ProjectId);if(Convert.ToInt32(await source.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Tahsis bu projede bulunamadı.");}
        using(var old=w.Command("SELECT * FROM dbo.PaymentAllocationReleases WHERE AllocationId=@id")){Id(old,"@id",input.AllocationId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){var r=t.Rows[0];if(r.Field<Guid>("ReleaseId")!=input.ReleaseId||r.Field<Guid>("ReleasedByAccountId")!=actor||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Tahsis daha önce serbest bırakılmış; geçmişi kontrol edin.");await w.CommitAsync(ct);return;}}
        using var c=w.Command("INSERT dbo.PaymentAllocationReleases(ReleaseId,AllocationId,Reason,ReleasedByAccountId,ReleasedBy,PayloadHash) VALUES(@id,@allocation,@reason,@actor,@name,@hash)");Id(c,"@id",input.ReleaseId);Id(c,"@allocation",input.AllocationId);Id(c,"@actor",actor);c.Text("@reason",reasonText,500);c.Text("@name",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    internal async Task<Wire.PaymentAllocationBoard> BoardAsync(Guid project,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.PaymentAllocationBoard>($"payment-allocations/{project:D}",null,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);
        using var p=w.Command(@"SELECT TOP(500) P.LedgerId PaymentLedgerId,CONVERT(nvarchar(36),P.RequestId) PaymentReference,P.PersonId,P.PersonnelGroup,L.EmploymentId,P.PaymentPurpose,P.CreatedUtc,-P.Delta PaymentAmount,
 -P.Delta-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.PaymentLedgerId=P.LedgerId)),0) PaymentAvailable,
 COALESCE(C.AD,J.AD)+N' '+COALESCE(C.SOYAD,J.SOYAD) PersonCaption
 FROM dbo.PaymentLedger P JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 LEFT JOIN dbo.Calisan C ON C.PersonId=P.PersonId AND P.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=P.PersonId AND P.PersonnelGroup=1
 WHERE P.ProjectId=@project AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL AND P.RequestId IS NOT NULL AND P.Delta<0 AND P.PaymentPurpose IN(N'Advance',N'Settlement') ORDER BY P.LedgerId DESC");Id(p,"@project",project);var payments=await p.TableAsync(ct);
        using var f=w.Command(@"SELECT TOP(500) F.EntryId FinanceEntryId,F.Reference FinanceReference,F.PersonId,F.PersonnelGroup,L.EmploymentId,F.PeriodStart,F.PeriodEnd,F.Amount OriginalAmount,F.AdjustmentAmount,F.EffectiveAmount AccrualAmount,
 F.EffectiveAmount-COALESCE(CONVERT(decimal(18,2),(SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WHERE A.FinanceEntryId=F.EntryId)),0) AccrualAvailable
 FROM dbo.FinanceEffectiveEntries F JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId) WHERE F.ProjectId=@project AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL ORDER BY F.CreatedUtc DESC,F.EntryId DESC");Id(f,"@project",project);var accruals=await f.TableAsync(ct);
        using var a=w.Command(@"SELECT TOP(500) A.AllocationId,A.PaymentLedgerId,F.Reference FinanceReference,P.PaymentPurpose,A.Amount,A.Reason,A.CreatedBy,A.CreatedUtc,R.ReleaseId,R.Reason ReleaseReason,R.ReleasedBy,R.ReleasedUtc
 FROM dbo.PaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId JOIN dbo.FinanceEntries F ON F.EntryId=A.FinanceEntryId LEFT JOIN dbo.PaymentAllocationReleases R ON R.AllocationId=A.AllocationId WHERE P.ProjectId=@project ORDER BY A.CreatedUtc DESC,A.AllocationId DESC");Id(a,"@project",project);var allocations=await a.TableAsync(ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(payments),RemoteApi.Encode(accruals),RemoteApi.Encode(allocations));
    }
}
