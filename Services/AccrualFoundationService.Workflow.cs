using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class AccrualFoundationService
{
    private static byte[] Payload(object input)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input,input.GetType()));
    private static async Task<DataRow> CalculationAsync(IUnitOfWork w,Guid project,Guid calculation,CancellationToken ct)
    {
        using var c=w.Command(@"SELECT C.*,A.ApprovalId,A.RecordVersion ApprovalVersion,A.ApprovedByAccountId,A.ApprovedBy,A.ApprovedUtc FROM dbo.AccrualCalculations C WITH(UPDLOCK,HOLDLOCK) LEFT JOIN dbo.AccrualApprovals A WITH(HOLDLOCK) ON A.CalculationId=C.CalculationId WHERE C.ProjectId=@project AND C.CalculationId=@id AND C.IsSealed=1");Id(c,"@project",project);Id(c,"@id",calculation);var rows=await c.TableAsync(ct);return rows.Rows.Count==1?rows.Rows[0]:throw new InvalidOperationException("Bu projede mühürlü dönem hesabı bulunamadı.");
    }
    private static async Task IndependentAsync(IUnitOfWork w,Guid calculation,Guid approver,CancellationToken ct)
    {
        using var c=w.Command(@"SELECT COUNT(*) FROM dbo.AccrualCalculations C WHERE C.CalculationId=@id AND (dbo.SameAccountPerson(@actor,C.CreatedByAccountId)=1 OR EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND dbo.AccountRepresentsPerson(@actor,L.PersonId)=1))");Id(c,"@id",calculation);Id(c,"@actor",approver);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Hazırlayan veya hak sahibiyle aynı kişi kendi dönem hesabına mali onay veremez; bağımsız yetkili hesap gerekir.");
    }
    internal async Task ApproveAsync(Wire.AccrualApprove input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("accrual/approve",input,ct);return;}
        if(input.ApprovalId==Guid.Empty)throw new ArgumentException("Onay işlem kimliği gerekiyor.");using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);var header=await CalculationAsync(w,input.ProjectId,input.CalculationId,ct);byte[] hash=Payload(input);
        if(!header.IsNull("ApprovalId"))
        {
            using var old=w.Command("SELECT PayloadHash FROM dbo.AccrualApprovals WHERE ApprovalId=@id AND CalculationId=@calculation AND ApprovedByAccountId=@actor");Id(old,"@id",input.ApprovalId);Id(old,"@calculation",input.CalculationId);Id(old,"@actor",actor);
            if(await old.ExecuteScalarAsync(ct) is byte[] bytes&&bytes.SequenceEqual(hash)){await w.CommitAsync(ct);return;}throw new InvalidOperationException("Bu hesap zaten mali onay almış; güncel geçmişi kontrol edin.");
        }
        if(!((byte[])header["RecordVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.CalculationVersion)))throw new InvalidOperationException("Dönem hesabı seçimi değişmiş; yenileyin.");await IndependentAsync(w,input.CalculationId,actor,ct);
        using var c=w.Command("INSERT dbo.AccrualApprovals(ApprovalId,CalculationId,ApprovedByAccountId,ApprovedBy,PayloadHash) VALUES(@id,@calculation,@actor,@name,@hash)");Id(c,"@id",input.ApprovalId);Id(c,"@calculation",input.CalculationId);Id(c,"@actor",actor);c.Text("@name",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    private sealed record PostingPreview(DataRow Header,DataTable Targets,string Fingerprint,string[] Issues);
    private static async Task<PostingPreview> PostingPreviewAsync(IUnitOfWork w,Guid project,Guid calculation,CancellationToken ct)
    {
        var h=await CalculationAsync(w,project,calculation,ct);var issues=new List<string>();if(h.IsNull("ApprovalId"))issues.Add("Önce bağımsız mali onay gerekir.");else await IndependentAsync(w,calculation,h.Field<Guid>("ApprovedByAccountId"),ct);
        using(var active=w.Command("SELECT IsActive FROM dbo.FinanceProjects WHERE ProjectId=@id")){Id(active,"@id",project);if(await active.ExecuteScalarAsync(ct) is not true)issues.Add("Proje pasif; aktarım için etkinleştirin.");}
        using(var old=w.Command("SELECT COUNT(*) FROM dbo.AccrualPostings WHERE CalculationId=@id")){Id(old,"@id",calculation);if(Convert.ToInt32(await old.ExecuteScalarAsync(ct))>0)issues.Add("Hesap daha önce aktarılmış; yeni aktarım yapılmaz.");}
        using(var people=w.Command("SELECT DISTINCT PersonId FROM dbo.AccrualCalculationLines WHERE CalculationId=@id ORDER BY PersonId"))
        {Id(people,"@id",calculation);var persons=await people.TableAsync(ct);foreach(DataRow row in persons.Rows)await SchedulingRules.LockPersonAsync(w,row.Field<Guid>("PersonId"),ct);}
        using var c=w.Command(@"WITH G AS(SELECT PersonId,EmploymentId,MIN(PersonCaption) PersonCaption,SUM(Amount) Amount,MIN(WorkDate) WorkStart,MAX(WorkDate) WorkEnd FROM dbo.AccrualCalculationLines WHERE CalculationId=@id GROUP BY PersonId,EmploymentId)
 SELECT G.*,E.PersonnelGroup,E.RecordVersion EmploymentVersion,E.StartDate,E.EndDate,E.IsClosed,C.CurrentEmploymentId,C.RecordVersion PersonVersion,C.PARA Balance,CAST(CASE WHEN C.ArchivedUtc IS NOT NULL THEN 1 ELSE 0 END AS bit) IsArchived
 FROM G JOIN dbo.EmploymentPeriods E WITH(UPDLOCK,HOLDLOCK) ON E.EmploymentId=G.EmploymentId AND E.PersonId=G.PersonId
 LEFT JOIN(SELECT PersonId,CurrentEmploymentId,RecordVersion,PARA,ArchivedUtc,0 PersonnelGroup FROM dbo.Calisan WITH(UPDLOCK,HOLDLOCK) UNION ALL SELECT PersonId,CurrentEmploymentId,RecordVersion,PARA,ArchivedUtc,1 FROM dbo.Joker WITH(UPDLOCK,HOLDLOCK)) C ON C.PersonId=G.PersonId AND C.PersonnelGroup=E.PersonnelGroup ORDER BY G.PersonId,G.EmploymentId");Id(c,"@id",calculation);var targets=await c.TableAsync(ct);targets.Columns.Add("AfterBalance",typeof(decimal));targets.Columns.Add("Issue",typeof(string));
        foreach(DataRow r in targets.Rows)
        {
            string issue=r.IsNull("PersonVersion")||r.Field<Guid?>("CurrentEmploymentId")!=r.Field<Guid>("EmploymentId")||r.Field<bool>("IsArchived")||r.Field<bool>("IsClosed")?"Personel arşivde veya güncel çalışma dönemi farklı":(!r.IsNull("StartDate")&&r.Field<DateTime>("WorkStart")<r.Field<DateTime>("StartDate"))||(!r.IsNull("EndDate")&&r.Field<DateTime>("WorkEnd")>r.Field<DateTime>("EndDate"))?"Kaynak çalışma günleri dönem tarihleri dışında":"";
            if(issue.Length==0){decimal after=r.Field<decimal>("Balance")+r.Field<decimal>("Amount");if(after is < -9999999999999999.99m or >9999999999999999.99m)issue="Bakiye sınırı aşılır";else r["AfterBalance"]=after;}
            r["Issue"]=issue;if(issue.Length>0)issues.Add(issue);
        }
        if(targets.Rows.Count==0||targets.AsEnumerable().Sum(r=>r.Field<decimal>("Amount"))!=h.Field<decimal>("TotalAmount"))issues.Add("Dönem tutarı ve hedef toplamları uzlaşmıyor.");targets.AcceptChanges();
        string fingerprint=Convert.ToHexString(Payload(new{Project=project,Calculation=calculation,Version=h["RecordVersion"],Approval=h["ApprovalId"],ApprovalVersion=h["ApprovalVersion"],Targets=RemoteApi.Encode(targets),Issues=issues.ToArray()}));return new(h,targets,fingerprint,issues.Distinct().ToArray());
    }
    internal async Task<Wire.AccrualPostingQuote> PostingQuoteAsync(Guid project,Guid calculation,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.AccrualPostingQuote>($"accrual/{project:D}/{calculation:D}/posting-quote",null,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,true,ct);var p=await PostingPreviewAsync(w,project,calculation,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(p.Targets),p.Fingerprint,p.Issues.Length==0,p.Issues);
    }
    internal async Task PostAsync(Wire.AccrualPost input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("accrual/post",input,ct);return;}
        if(input.PostingId==Guid.Empty||input.Fingerprint is not{Length:64})throw new ArgumentException("Güncel aktarım önizlemesi ve işlem kimliği gerekiyor.");using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=Payload(input);
        using(var old=w.Command("SELECT P.* FROM dbo.AccrualPostings P WITH(UPDLOCK,HOLDLOCK) JOIN dbo.AccrualCalculations C ON C.CalculationId=P.CalculationId WHERE P.PostingId=@id AND C.ProjectId=@project"))
        {
            Id(old,"@id",input.PostingId);Id(old,"@project",input.ProjectId);var rows=await old.TableAsync(ct);if(rows.Rows.Count>0){var r=rows.Rows[0];if(r.Field<Guid>("CalculationId")!=input.CalculationId||r.Field<Guid>("PostedByAccountId")!=actor||!r.Field<bool>("IsSealed")||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Aktarım işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}
        }
        var p=await PostingPreviewAsync(w,input.ProjectId,input.CalculationId,ct);if(p.Issues.Length>0)throw new InvalidOperationException(string.Join("\n",p.Issues));if(p.Fingerprint!=input.Fingerprint)throw new InvalidOperationException("Önizleme sonrası personel/bakiye/çalışma dönemi değişmiş; yeni önizlemeyi inceleyin.");
        using(var c=w.Command("INSERT dbo.AccrualPostings(PostingId,CalculationId,ApprovalId,TotalAmount,ItemCount,PayloadHash,PostedByAccountId,PostedBy) VALUES(@id,@calculation,@approval,@amount,@count,@hash,@actor,@name)"))
        {Id(c,"@id",input.PostingId);Id(c,"@calculation",input.CalculationId);Id(c,"@approval",p.Header.Field<Guid>("ApprovalId"));Id(c,"@actor",actor);c.Text("@name",user.Username);c.Money("@amount",p.Header.Field<decimal>("TotalAmount"));c.Parameters.Add("@count",SqlDbType.Int).Value=p.Targets.Rows.Count;c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);}
        int ordinal=0;
        foreach(DataRow r in p.Targets.Rows)
        {
            ordinal++;Guid person=r.Field<Guid>("PersonId"),employment=r.Field<Guid>("EmploymentId");var group=(PersonnelGroup)r.Field<int>("PersonnelGroup");decimal amount=r.Field<decimal>("Amount");Guid? entry=null;long? ledger=null;
            await SchedulingRules.LockPersonAsync(w,person,ct);
            if(amount>0)
            {
                using(var change=w.Command($"UPDATE {PersonnelRepository.Table(group)} SET PARA=PARA+@amount WHERE PersonId=@person AND CurrentEmploymentId=@employment AND RecordVersion=@version AND ArchivedUtc IS NULL AND PARA+@amount BETWEEN -9999999999999999.99 AND 9999999999999999.99"))
                {Id(change,"@person",person);Id(change,"@employment",employment);change.Money("@amount",amount);change.Parameters.Add("@version",SqlDbType.Binary,8).Value=r["PersonVersion"];if(await change.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Aktarım sırasında personel/bakiye değişmiş; paketin tamamı geri alındı.");}
                entry=Guid.NewGuid();
                using(var finance=w.Command($@"INSERT dbo.FinanceEntries(EntryId,ProjectId,Kind,Reference,PersonId,TC,PersonnelGroup,PeriodStart,PeriodEnd,Amount,Description,CreatedBy,AccrualCalculationId,AccrualEmploymentId)
 SELECT @entry,@project,N'Accrual',@reference,PersonId,TC,@group,@first,@last,@amount,N'Onaylı dönem hakedişi aktarımı',@name,@calculation,@employment FROM {PersonnelRepository.Table(group)} WHERE PersonId=@person AND CurrentEmploymentId=@employment AND ArchivedUtc IS NULL"))
                {Id(finance,"@entry",entry);Id(finance,"@project",input.ProjectId);Id(finance,"@calculation",input.CalculationId);Id(finance,"@employment",employment);Id(finance,"@person",person);finance.Parameters.Add("@group",SqlDbType.Int).Value=(int)group;Day(finance,"@first",r.Field<DateTime>("WorkStart"));Day(finance,"@last",r.Field<DateTime>("WorkEnd"));finance.Money("@amount",amount);finance.Text("@name",user.Username);finance.Text("@reference","ACR-"+input.CalculationId.ToString("N")+"-"+ordinal,50);if(await finance.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Hakediş belgesi oluşturulamadı.");}
                // OUTPUT INTO works with the existing audit/employment-link triggers.
                using var post=w.Command($@"DECLARE @ids TABLE(Id bigint);INSERT dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor,ProjectId,FinanceEntryId,AccrualPostingId) OUTPUT inserted.LedgerId INTO @ids
 SELECT PersonId,TC,@group,@amount,N'Allocation',@name,@project,@entry,@posting FROM {PersonnelRepository.Table(group)} WHERE PersonId=@person AND CurrentEmploymentId=@employment AND ArchivedUtc IS NULL;SELECT Id FROM @ids;");Id(post,"@person",person);Id(post,"@employment",employment);Id(post,"@project",input.ProjectId);Id(post,"@entry",entry);Id(post,"@posting",input.PostingId);post.Parameters.Add("@group",SqlDbType.Int).Value=(int)group;post.Money("@amount",amount);post.Text("@name",user.Username);ledger=await post.ExecuteScalarAsync(ct) is long id?id:throw new InvalidOperationException("Hakediş defter satırı oluşturulamadı.");
            }
            using var item=w.Command("INSERT dbo.AccrualPostingItems(ItemId,PostingId,PersonId,EmploymentId,PersonnelGroup,Amount,BeforeBalance,AfterBalance,FinanceEntryId,LedgerId) VALUES(NEWID(),@posting,@person,@employment,@group,@amount,@before,@after,@entry,@ledger)");Id(item,"@posting",input.PostingId);Id(item,"@person",person);Id(item,"@employment",employment);Id(item,"@entry",entry);item.Parameters.Add("@ledger",SqlDbType.BigInt).Value=(object?)ledger??DBNull.Value;item.Parameters.Add("@group",SqlDbType.Int).Value=(int)group;item.Money("@amount",amount);item.Money("@before",r.Field<decimal>("Balance"));item.Money("@after",r.Field<decimal>("AfterBalance"));await item.ExecuteNonQueryAsync(ct);
        }
        using(var seal=w.Command("UPDATE dbo.AccrualPostings SET IsSealed=1 WHERE PostingId=@id")){Id(seal,"@id",input.PostingId);await seal.ExecuteNonQueryAsync(ct);}await w.CommitAsync(ct);
    }
    internal async Task<DataTable> PostingItemsAsync(Guid project,Guid calculation,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"accrual/{project:D}/{calculation:D}/posting-items",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);using var c=w.Command("SELECT "+(user.Role==UserRole.ProjectUser?"I.ItemId,I.PostingId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.Amount,I.EffectiveAmount,I.LastCorrectionId,I.FinanceEntryId,I.LedgerId":"I.*")+",F.Reference FinanceReference,(SELECT MIN(L.PersonCaption) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=P.CalculationId AND L.EmploymentId=I.EmploymentId) PersonCaption FROM dbo.AccrualItemBalances I JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId JOIN dbo.AccrualCalculations C ON C.CalculationId=P.CalculationId LEFT JOIN dbo.FinanceEntries F ON F.EntryId=I.FinanceEntryId WHERE C.ProjectId=@project AND C.CalculationId=@id AND P.IsSealed=1 ORDER BY I.PersonId,I.EmploymentId");Id(c,"@project",project);Id(c,"@id",calculation);var rows=await c.TableAsync(ct);await w.CommitAsync(ct);return rows;
    }
}
