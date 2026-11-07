using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class WageRuleService
{
    internal async Task ActAsync(Wire.WageClaimAction input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("wage-rules/claim/action",input,ct);return;}
        if(input.ActionId==Guid.Empty||input.Kind is not("Approve" or "Reject" or "Cancel" or "RequestReverse" or "ApproveReverse"))throw new ArgumentException("Talep kararı geçersiz.");using var reason=AuditContext.UseReason(input.Reason);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);var (h,e)=await ClaimAsync(w,input.ProjectId,input.ClaimId,ct);byte[] hash=Hash(input);
        using(var old=w.Command("SELECT ClaimId,ActorAccountId,PayloadHash FROM dbo.WageClaimActions WHERE ActionId=@id")){Id(old,"@id",input.ActionId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){var r=t.Rows[0];if(r.Field<Guid>("ClaimId")!=input.ClaimId||r.Field<Guid>("ActorAccountId")!=actor||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Talep karar kimliği başka içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        if(!((byte[])h["RecordVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.Version))||!h.Field<bool>("IsReserved"))throw new InvalidOperationException("Talep durumu/sürümü değişmiş; geçmişi yenileyin.");
        using var actions=w.Command("SELECT Kind,ActorAccountId FROM dbo.WageClaimActions WHERE ClaimId=@id");Id(actions,"@id",input.ClaimId);var a=await actions.TableAsync(ct);bool Has(string kind)=>a.AsEnumerable().Any(r=>r.Field<string>("Kind")==kind);
        using var transfers=w.Command("SELECT Kind FROM dbo.WageClaimTransfers WHERE ClaimId=@id");Id(transfers,"@id",input.ClaimId);var tfs=await transfers.TableAsync(ct);bool posted=tfs.AsEnumerable().Any(r=>r.Field<string>("Kind")=="Post"),reversed=tfs.AsEnumerable().Any(r=>r.Field<string>("Kind")=="Reverse");
        if(Has(input.Kind)||Has("Reject")||Has("Cancel")||reversed)throw new InvalidOperationException("Talep için bu işlem artık uygulanamaz.");
        switch(input.Kind)
        {
            case "Approve":case "Reject":
                if(posted||Has("Approve"))throw new InvalidOperationException("Talep zaten onaylanmış/aktarılmış.");await IndependentAsync(w,actor,h.Field<Guid>("CreatedByAccountId"),e.PersonId,ct);
                if(input.Kind=="Approve"){var current=await BuildAsync(w,e.Scope,ct,input.ClaimId);if(current.Fingerprint!=e.Fingerprint)throw new InvalidOperationException("Kural/kaynak değişmiş; talebi iptal edip güncel önizlemeyle hazırlayın.");}break;
            case "Cancel":if(posted)throw new InvalidOperationException("Aktarılmış kayıt iptal edilemez; bağımsız onaylı tam ters aktarım gerekir.");break;
            case "RequestReverse":if(!posted)throw new InvalidOperationException("Ters aktarım yalnız aktarılmış talebe hazırlanır.");break;
            case "ApproveReverse":
                if(!posted||!Has("RequestReverse"))throw new InvalidOperationException("Önce tam ters aktarım isteği gerekiyor.");await IndependentAsync(w,actor,a.AsEnumerable().Single(r=>r.Field<string>("Kind")=="RequestReverse").Field<Guid>("ActorAccountId"),e.PersonId,ct);break;
        }
        using var c=w.Command("INSERT dbo.WageClaimActions(ActionId,ClaimId,Kind,ActorAccountId,Actor,Reason,PayloadHash) VALUES(@id,@claim,@kind,@actor,@user,@reason,@hash)");Id(c,"@id",input.ActionId);Id(c,"@claim",input.ClaimId);Id(c,"@actor",actor);c.Text("@kind",input.Kind,20);c.Text("@user",user.Username);c.Text("@reason",AuditContext.ValidateReason(input.Reason),500);Binary(c,"@hash",hash);await c.ExecuteNonQueryAsync(ct);
        if(input.Kind is "Cancel" or "Reject"){using var release=w.Command("UPDATE dbo.WageClaims SET IsReserved=0,ReleasedUtc=SYSUTCDATETIME() WHERE ClaimId=@id AND IsReserved=1");Id(release,"@id",input.ClaimId);if(await release.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Talep rezervasyonu kaldırılamadı.");}await w.CommitAsync(ct);
    }
    private sealed record TransferPreview(DataRow Claim,WageEvidence Evidence,DataTable Targets,Guid? Entry,string Fingerprint);
    private static async Task<TransferPreview> TransferPreviewAsync(IUnitOfWork w,Guid project,Guid claim,string kind,CancellationToken ct)
    {
        if(kind is not("Post" or "Reverse"))throw new ArgumentException("Aktarım türü geçersiz.");var (h,e)=await ClaimAsync(w,project,claim,ct);if(!h.Field<bool>("IsReserved"))throw new InvalidOperationException("Talep iptal/ret veya ters aktarım ile sonuçlandırılmış.");
        using var action=w.Command("SELECT ActionId FROM dbo.WageClaimActions WHERE ClaimId=@id AND Kind=@kind");Id(action,"@id",claim);action.Text("@kind",kind=="Post"?"Approve":"ApproveReverse",20);if(await action.ExecuteScalarAsync(ct) is not Guid approval)throw new InvalidOperationException("Aktarım için bağımsız mali onay gerekiyor.");
        using var done=w.Command("SELECT Kind,FinanceEntryId FROM dbo.WageClaimTransfers WHERE ClaimId=@id");Id(done,"@id",claim);var transfers=await done.TableAsync(ct);if(transfers.AsEnumerable().Any(r=>r.Field<string>("Kind")==kind)||(kind=="Reverse"&&!transfers.AsEnumerable().Any(r=>r.Field<string>("Kind")=="Post")))throw new InvalidOperationException("Aktarım daha önce yapılmış veya asıl aktarım yok.");
        if(kind=="Post"){var current=await BuildAsync(w,e.Scope,ct,claim);if(current.Fingerprint!=e.Fingerprint)throw new InvalidOperationException("Onaylı talebin kaynakları değişmiş; iptal ve güncel talep gerekir.");}
        await ProjectFinanceService.DemandProjectAsync(w,project,ct);Guid? entry=null;byte[]? entryVersion=null;
        if(kind=="Reverse")
        {
            entry=transfers.AsEnumerable().Single(r=>r.Field<string>("Kind")=="Post").Field<Guid>("FinanceEntryId");await PaymentAllocationService.DemandFinanceFreeAsync(w,entry.Value,ct);
            using var f=w.Command("SELECT RecordVersion FROM dbo.FinanceEntries WITH(UPDLOCK,HOLDLOCK) WHERE EntryId=@id AND ProjectId=@project AND WageClaimId=@claim AND ReversedUtc IS NULL");Id(f,"@id",entry);Id(f,"@project",project);Id(f,"@claim",claim);entryVersion=await f.ExecuteScalarAsync(ct) as byte[]??throw new InvalidOperationException("Asıl mali belge bulunamadı veya iptal edilmiş.");
        }
        var targets=new DataTable();targets.Columns.Add("PersonId",typeof(Guid));targets.Columns.Add("EmploymentId",typeof(Guid));targets.Columns.Add("PersonVersion",typeof(byte[]));targets.Columns.Add("TC",typeof(long));targets.Columns.Add("PersonnelGroup",typeof(int));targets.Columns.Add("Balance",typeof(decimal));targets.Columns.Add("AfterBalance",typeof(decimal));targets.Columns.Add("Amount",typeof(decimal));targets.Columns.Add("FinanceEntryId",typeof(Guid));targets.Columns.Add("FinanceVersion",typeof(byte[]));targets.Columns.Add("Kind");
        decimal delta=kind=="Post"?e.Amount:-e.Amount;
        if(e.PersonId.HasValue)
        {
            await SchedulingRules.LockPersonAsync(w,e.PersonId.Value,ct);
            using var p=w.Command(@"SELECT C.TC,C.RecordVersion,C.PARA,E.PersonnelGroup FROM dbo.EmploymentPeriods E WITH(UPDLOCK,HOLDLOCK) JOIN(SELECT PersonId,TC,CurrentEmploymentId,RecordVersion,COALESCE(PARA,0) PARA,ArchivedUtc,0 PersonnelGroup FROM dbo.Calisan WITH(UPDLOCK,HOLDLOCK) UNION ALL SELECT PersonId,TC,CurrentEmploymentId,RecordVersion,COALESCE(PARA,0),ArchivedUtc,1 FROM dbo.Joker WITH(UPDLOCK,HOLDLOCK)) C ON C.PersonId=E.PersonId AND C.CurrentEmploymentId=E.EmploymentId AND C.PersonnelGroup=E.PersonnelGroup WHERE E.PersonId=@person AND E.EmploymentId=@employment AND E.IsClosed=0 AND C.ArchivedUtc IS NULL AND(E.StartDate IS NULL OR E.StartDate<=@from) AND(E.EndDate IS NULL OR E.EndDate>=@to)");Id(p,"@person",e.PersonId);Id(p,"@employment",e.Scope.EmploymentId);Day(p,"@from",e.Scope.From);Day(p,"@to",e.Scope.To);var rows=await p.TableAsync(ct);if(rows.Rows.Count!=1)throw new InvalidOperationException("Personel arşivlenmiş veya güncel çalışma dönemi farklı; yeni kaydın bakiyesi değiştirilmedi.");var r=rows.Rows[0];decimal before=r.Field<decimal>("PARA"),after=before+delta;if(after is < -9999999999999999.99m or >9999999999999999.99m)throw new InvalidOperationException("Bakiye para sınırı aşılır.");targets.Rows.Add(e.PersonId,e.Scope.EmploymentId,r["RecordVersion"],r["TC"],r["PersonnelGroup"],before,after,delta,(object?)entry??DBNull.Value,(object?)entryVersion??DBNull.Value,kind);
        }
        else targets.Rows.Add(DBNull.Value,DBNull.Value,DBNull.Value,DBNull.Value,DBNull.Value,DBNull.Value,DBNull.Value,delta,(object?)entry??DBNull.Value,(object?)entryVersion??DBNull.Value,kind);
        string fingerprint=Convert.ToHexString(Hash(new{Project=project,Claim=claim,Kind=kind,ClaimVersion=h["RecordVersion"],Approval=approval,Targets=RemoteApi.Encode(targets)}));return new(h,e,targets,entry,fingerprint);
    }
    internal async Task<Wire.AccrualPostingQuote> TransferQuoteAsync(Wire.WageTransferQuoteRequest input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.AccrualPostingQuote>("wage-rules/claim/transfer-quote",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);var p=await TransferPreviewAsync(w,input.ProjectId,input.ClaimId,input.Kind,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(p.Targets),p.Fingerprint,true,[]);
    }
    internal async Task TransferAsync(Wire.WageClaimTransfer input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("wage-rules/claim/transfer",input,ct);return;}
        if(input.TransferId==Guid.Empty||input.Fingerprint is not{Length:64}||input.Kind is not("Post" or "Reverse"))throw new ArgumentException("Güncel aktarım önizlemesi gerekiyor.");using var reason=AuditContext.UseReason(input.Reason);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=Hash(input);
        using(var old=w.Command("SELECT T.ClaimId,T.ActorAccountId,T.PayloadHash FROM dbo.WageClaimTransfers T JOIN dbo.WageClaims C ON C.ClaimId=T.ClaimId WHERE T.TransferId=@id AND C.ProjectId=@project")){Id(old,"@id",input.TransferId);Id(old,"@project",input.ProjectId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){var r=t.Rows[0];if(r.Field<Guid>("ClaimId")!=input.ClaimId||r.Field<Guid>("ActorAccountId")!=actor||!((byte[])r["PayloadHash"]).SequenceEqual(hash))throw new InvalidOperationException("Aktarım işlem kimliği başka içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        var p=await TransferPreviewAsync(w,input.ProjectId,input.ClaimId,input.Kind,ct);if(p.Fingerprint!=input.Fingerprint)throw new InvalidOperationException("Bakiye, personel veya mali belge değişmiş; yeni aktarım önizlemesi gerekiyor.");var e=p.Evidence;var target=p.Targets.Rows[0];bool personal=e.PersonId.HasValue;decimal delta=target.Field<decimal>("Amount");Guid entry=p.Entry??Guid.NewGuid();long? ledger=null;
        if(personal)
        {
            var group=(PersonnelGroup)target.Field<int>("PersonnelGroup");using var change=w.Command($"UPDATE {PersonnelRepository.Table(group)} SET PARA=COALESCE(PARA,0)+@delta WHERE PersonId=@person AND CurrentEmploymentId=@employment AND RecordVersion=@version AND ArchivedUtc IS NULL AND COALESCE(PARA,0)+@delta BETWEEN -9999999999999999.99 AND 9999999999999999.99");Id(change,"@person",e.PersonId);Id(change,"@employment",e.Scope.EmploymentId);Binary(change,"@version",(byte[])target["PersonVersion"]);change.Money("@delta",delta);if(await change.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        }
        if(input.Kind=="Post")
        {
            using var f=w.Command("INSERT dbo.FinanceEntries(EntryId,ProjectId,Kind,Reference,PersonId,TC,PersonnelGroup,PeriodStart,PeriodEnd,Amount,Description,CreatedBy,WageClaimId) VALUES(@entry,@project,@kind,@reference,@person,@tc,@group,@from,@to,@amount,@description,@user,@claim)");Id(f,"@entry",entry);Id(f,"@project",input.ProjectId);Id(f,"@person",e.PersonId);Id(f,"@claim",input.ClaimId);f.Text("@kind",personal?"Accrual":"Expense",12);f.Text("@reference","WAGE-"+input.ClaimId.ToString("N"),50);f.Parameters.Add("@tc",SqlDbType.BigInt).Value=target["TC"];f.Parameters.Add("@group",SqlDbType.Int).Value=target["PersonnelGroup"];Day(f,"@from",e.Scope.From);Day(f,"@to",e.Scope.To);f.Money("@amount",e.Amount);f.Text("@description","Onaylı ücret/gider kuralı: "+e.Kind,200);f.Text("@user",user.Username);await f.ExecuteNonQueryAsync(ct);
        }
        else
        {
            using var f=w.Command("UPDATE dbo.FinanceEntries SET ReversedUtc=SYSUTCDATETIME(),ReversedBy=@user WHERE EntryId=@entry AND WageClaimId=@claim AND RecordVersion=@version AND ReversedUtc IS NULL");Id(f,"@entry",entry);Id(f,"@claim",input.ClaimId);f.Text("@user",user.Username);Binary(f,"@version",(byte[])target["FinanceVersion"]);if(await f.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Mali belge değişmiş; ters aktarımın tamamı geri alındı.");
        }
        if(personal)
        {
            using var l=w.Command("DECLARE @ids TABLE(Id bigint);INSERT dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor,ProjectId,FinanceEntryId,WageClaimId,WageTransferKind) OUTPUT inserted.LedgerId INTO @ids VALUES(@person,@tc,@group,@delta,N'Allocation',@user,@project,@entry,@claim,@kind);SELECT Id FROM @ids;");Id(l,"@person",e.PersonId);Id(l,"@project",input.ProjectId);Id(l,"@entry",entry);Id(l,"@claim",input.ClaimId);l.Parameters.Add("@tc",SqlDbType.BigInt).Value=target["TC"];l.Parameters.Add("@group",SqlDbType.Int).Value=target["PersonnelGroup"];l.Money("@delta",delta);l.Text("@user",user.Username);l.Text("@kind",input.Kind,10);ledger=await l.ExecuteScalarAsync(ct) as long? ??throw new InvalidOperationException("Ücret defter kaydı oluşturulamadı.");
        }
        using(var t=w.Command("INSERT dbo.WageClaimTransfers(TransferId,ClaimId,Kind,FinanceEntryId,LedgerId,Amount,BeforeBalance,AfterBalance,ActorAccountId,Actor,Reason,PayloadHash) VALUES(@id,@claim,@kind,@entry,@ledger,@amount,@before,@after,@actor,@user,@reason,@hash)"))
        {Id(t,"@id",input.TransferId);Id(t,"@claim",input.ClaimId);Id(t,"@entry",entry);Id(t,"@actor",actor);t.Text("@kind",input.Kind,10);t.Parameters.Add("@ledger",SqlDbType.BigInt).Value=(object?)ledger??DBNull.Value;t.Money("@amount",delta);foreach(var pair in new[]{("@before","Balance"),("@after","AfterBalance")}){var money=t.Parameters.Add(pair.Item1,SqlDbType.Decimal);money.Precision=18;money.Scale=2;money.Value=target[pair.Item2];}t.Text("@user",user.Username);t.Text("@reason",AuditContext.ValidateReason(input.Reason),500);Binary(t,"@hash",hash);await t.ExecuteNonQueryAsync(ct);}
        if(input.Kind=="Reverse"){using var release=w.Command("UPDATE dbo.WageClaims SET IsReserved=0,ReleasedUtc=SYSUTCDATETIME() WHERE ClaimId=@id AND IsReserved=1");Id(release,"@id",input.ClaimId);if(await release.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Ters aktarım rezervasyonu kaldırılamadı.");}await w.CommitAsync(ct);
    }
}
