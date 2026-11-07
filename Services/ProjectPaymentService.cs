using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class ProjectPaymentService(string connectionString, UserSession user)
{
    private static void Id(SqlCommand c, string name, Guid value) => c.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value;
    private static async Task<Guid> ActorAsync(IUnitOfWork w, CancellationToken ct)
    { using var c = w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))"); return (Guid)(await c.ExecuteScalarAsync(ct))!; }
    private static async Task<DataRow?> ProposalAsync(IUnitOfWork w, Guid id, CancellationToken ct)
    { using var c = w.Command("SELECT * FROM dbo.ProjectPaymentProposals WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id"); Id(c, "@id", id); var t = await c.TableAsync(ct); return t.Rows.Count == 1 ? t.Rows[0] : null; }
    private static async Task<DateTime> DayAsync(IUnitOfWork w, Guid project, CancellationToken ct)
    {
        using var c = w.Command("SELECT SYSUTCDATETIME(),StartDate,EndDate FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@id AND IsActive=1"); Id(c, "@id", project); using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException("Proje etkin değil.");
        var day = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc), DailyClosingService.BusinessZone).Date;
        if ((!r.IsDBNull(1) && day < r.GetDateTime(1)) || (!r.IsDBNull(2) && day > r.GetDateTime(2))) throw new InvalidOperationException("Proje bugün geçerli tarih aralığında değil."); return day;
    }
    private async Task<(long Tc, Guid Employment, long Closure)> TargetAsync(IUnitOfWork w, Guid project, Guid person, PersonnelGroup group, byte[] version, decimal amount, PaymentPurpose purpose, CancellationToken ct)
    {
        DateTime day = await DayAsync(w, project, ct); long tc; Guid employment; decimal balance;
        using (var c = w.Command($"SELECT TC,CurrentEmploymentId,COALESCE(PARA,0) FROM {PersonnelRepository.Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND RecordVersion=@v AND ArchivedUtc IS NULL"))
        { Id(c, "@person", person); c.Parameters.Add("@v", SqlDbType.Binary, 8).Value = version; using var r = await c.ExecuteReaderAsync(ct); if (!await r.ReadAsync(ct)) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage); tc = r.GetInt64(0); employment = r.GetGuid(1); balance = r.GetDecimal(2); }
        using (var c = w.Command(@"SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments A WITH(HOLDLOCK)
 JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 JOIN dbo.EmploymentPeriods E ON E.EmploymentId=L.EmploymentId
 WHERE A.ProjectId=@project AND A.PersonId=@person AND A.PersonnelGroup=@group AND L.EmploymentId=@employment AND E.IsClosed=0
 AND (E.StartDate IS NULL OR E.StartDate<=@day) AND (E.EndDate IS NULL OR E.EndDate>=@day)
 AND A.CancelledUtc IS NULL AND A.StartDate<=@day AND (A.EndDate IS NULL OR A.EndDate>=@day)"))
        { Id(c, "@project", project); Id(c, "@person", person); Id(c, "@employment", employment); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)group; c.Parameters.Add("@day", SqlDbType.Date).Value = day; if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) == 0) throw new InvalidOperationException("Personelin güncel döneminde bu projede bugün geçerli ataması yok."); }
        if (purpose == PaymentPurpose.Settlement)
        {
            using var c = w.Command(@"SELECT
 (SELECT COALESCE(SUM(F.EffectiveAmount),0) FROM dbo.FinanceEffectiveEntries F WITH(HOLDLOCK) JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId)
 WHERE F.PersonId=@person AND F.ProjectId=@project AND L.EmploymentId=@employment AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL)
 +(SELECT COALESCE(SUM(P.Delta),0) FROM dbo.PaymentLedger P WITH(HOLDLOCK) JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 WHERE P.PersonId=@person AND P.ProjectId=@project AND L.EmploymentId=@employment AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL)");
            Id(c, "@person", person); Id(c, "@project", project); Id(c, "@employment", employment);
            if (amount > balance || amount > Convert.ToDecimal(await c.ExecuteScalarAsync(ct))) throw new InvalidOperationException("Tutar kişinin bakiyesini veya bu proje/dönemin kalan hakedişini aşıyor.");
        }
        else if (balance - amount < -9999999999999999.99m) throw new InvalidOperationException("Avans sonrası bakiye sınırı aşılıyor.");
        using var closure = w.Command("SELECT COALESCE(MAX(LedgerId),0) FROM dbo.PaymentLedger WHERE TC=@tc AND PersonnelGroup=@group AND Kind=N'Closure'"); closure.Tc(tc); closure.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        return (tc, employment, Convert.ToInt64(await closure.ExecuteScalarAsync(ct)));
    }
    internal async Task PrepareAsync(Wire.ProjectPaymentPrepare input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("project-payments/prepare", input, ct); return; }
        if (!Enum.IsDefined(input.Group) || !Enum.IsDefined(input.Purpose) || input.RequestId == Guid.Empty || input.PersonId == Guid.Empty || input.ProjectId == Guid.Empty || input.Amount <= 0 || input.Amount > 9999999999999999.99m || decimal.Round(input.Amount, 2) != input.Amount) throw new ArgumentException("Geçerli proje, personel, tür ve iki ondalıklı pozitif tutar gerekiyor.");
        byte[] version = PersonnelConcurrency.RequireVersion(input.PersonVersion); string reason = AuditContext.ValidateReason(input.Reason ?? ""); using var context = AuditContext.UseReason(reason);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, input.ProjectId, ProjectAccess.PreparePayment, ct); Guid actor = await ActorAsync(w, ct);
        var prior = await ProposalAsync(w, input.RequestId, ct);
        if (prior != null)
        {
            if (prior.Field<Guid>("ProjectId") != input.ProjectId || prior.Field<Guid>("PersonId") != input.PersonId || prior.Field<Guid>("PreparedByAccountId") != actor || prior.Field<int>("PersonnelGroup") != (int)input.Group || prior.Field<decimal>("Amount") != input.Amount || prior.Field<string>("Purpose") != input.Purpose.ToString() || prior.Field<string>("PrepareReason") != reason || !((byte[])prior["PersonVersion"]).SequenceEqual(version)) throw new InvalidOperationException("İşlem kimliği farklı bir hazırlık için kullanılmış.");
            await w.CommitAsync(ct); return;
        }
        var target = await TargetAsync(w, input.ProjectId, input.PersonId, (PersonnelGroup)input.Group, version, input.Amount, (PaymentPurpose)input.Purpose, ct);
        using (var collision = w.Command("SELECT COUNT(*) FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id")) { Id(collision, "@id", input.RequestId); if (Convert.ToInt32(await collision.ExecuteScalarAsync(ct)) != 0) throw new InvalidOperationException("İşlem kimliği başka bir ödeme için kullanılmış."); }
        Guid? grant = null; if (user.Role != UserRole.Admin) { using var c = w.Command("SELECT GrantId FROM dbo.ProjectAccountAccess WITH(HOLDLOCK) WHERE ProjectId=@project AND AccountId=@actor AND IsActive=1 AND CanPreparePayments=1"); Id(c, "@project", input.ProjectId); Id(c, "@actor", actor); grant = (Guid)(await c.ExecuteScalarAsync(ct))!; }
        using (var c = w.Command(@"INSERT dbo.ProjectPaymentProposals(RequestId,ProjectId,PersonId,EmploymentId,PersonnelGroup,TC,Amount,Purpose,PersonVersion,ClosureId,PreparedByAccountId,PreparedBy,PreparedEpoch,PreparedGrantId,PrepareReason)
 VALUES(@id,@project,@person,@employment,@group,@tc,@amount,@purpose,@version,@closure,@actor,@name,@epoch,@grant,@reason)"))
        { Id(c, "@id", input.RequestId); Id(c, "@project", input.ProjectId); Id(c, "@person", input.PersonId); Id(c, "@employment", target.Employment); Id(c, "@actor", actor); Id(c, "@epoch", user.SecurityEpoch); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)input.Group; c.Tc(target.Tc); c.Money("@amount", input.Amount); c.Text("@purpose", input.Purpose.ToString(), 20); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version; c.Parameters.Add("@closure", SqlDbType.BigInt).Value = target.Closure; c.Text("@name", user.Username); c.Parameters.Add("@grant", SqlDbType.UniqueIdentifier).Value = (object?)grant ?? DBNull.Value; c.Text("@reason", reason, 500); await c.ExecuteNonQueryAsync(ct); }
        await w.CommitAsync(ct);
    }
    internal async Task DecideAsync(Wire.ProjectPaymentDecide input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("project-payments/decide", input, ct); return; }
        byte[] version = PersonnelConcurrency.RequireVersion(input.Version); string reason = AuditContext.ValidateReason(input.Reason ?? ""); using var context = AuditContext.UseReason(reason);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, input.ProjectId, ProjectAccess.ApprovePayment, ct); Guid actor = await ActorAsync(w, ct);
        var row = await ProposalAsync(w, input.RequestId, ct); if (row == null || row.Field<Guid>("ProjectId") != input.ProjectId) throw new UnauthorizedAccessException("Talep bu projede bulunamadı.");
        if (row.Field<Guid>("PreparedByAccountId") == actor) throw new UnauthorizedAccessException("Hazırlayan kendi talebini onaylayamaz veya reddedemez; başka yetkili hesap gerekir.");
        string status = input.Approve ? "Approved" : "Rejected";
        if (row.Field<string>("Status") != "Pending")
        {
            if (row.Field<string>("Status") != status || row.Field<Guid?>("DecidedByAccountId") != actor || row.Field<string>("DecisionReason") != reason || !((byte[])row["DecisionVersion"]).SequenceEqual(version)) throw new InvalidOperationException("Talep daha önce sonuçlanmış; yenileyin.");
            await w.CommitAsync(ct); return;
        }
        if (!((byte[])row["RecordVersion"]).SequenceEqual(version)) throw new InvalidOperationException("Talep değişmiş; yenileyin.");
        long? ledger = null;
        if (input.Approve)
        {
            using (var c = w.Command(@"SELECT COUNT(*) FROM dbo.admin A WITH(HOLDLOCK) WHERE A.AccountId=@actor AND A.IsActive=1 AND A.MustChangePassword=0 AND A.SessionEpoch=@epoch AND (A.RoleName=N'Admin' OR A.CanViewSalary=1)
 AND ((@grant IS NULL AND A.RoleName=N'Admin') OR (A.RoleName=N'ProjectUser' AND EXISTS(SELECT 1 FROM dbo.ProjectAccountAccess G WITH(HOLDLOCK) WHERE G.GrantId=@grant AND G.AccountId=A.AccountId AND G.ProjectId=@project AND G.IsActive=1 AND G.CanPreparePayments=1 AND G.CanViewCosts=1)))"))
            { Id(c, "@actor", row.Field<Guid>("PreparedByAccountId")); Id(c, "@epoch", row.Field<Guid>("PreparedEpoch")); Id(c, "@project", input.ProjectId); c.Parameters.Add("@grant", SqlDbType.UniqueIdentifier).Value = row["PreparedGrantId"]; if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Hazırlayanın hesabı/oturumu/yetkisi değişmiş. Talebi reddedip yeniden hazırlatın."); }
            var target = await TargetAsync(w, input.ProjectId, row.Field<Guid>("PersonId"), (PersonnelGroup)row.Field<int>("PersonnelGroup"), (byte[])row["PersonVersion"], row.Field<decimal>("Amount"), Enum.Parse<PaymentPurpose>(row.Field<string>("Purpose")!), ct);
            if (target.Employment != row.Field<Guid>("EmploymentId") || target.Closure != row.Field<long>("ClosureId")) throw new InvalidOperationException("Personelin dönemi veya kapanış sınırı değişmiş; yeniden hazırlayın.");
            var request = new PaymentRequest(input.RequestId, (PersonnelGroup)row.Field<int>("PersonnelGroup"), target.Tc, row.Field<decimal>("Amount"), target.Closure) { FormatVersion = 3, PersonVersion = ((byte[])row["PersonVersion"]).ToArray(), PersonId = row.Field<Guid>("PersonId"), Purpose = Enum.Parse<PaymentPurpose>(row.Field<string>("Purpose")!), ProjectId = input.ProjectId, AuditReason = reason };
            var outcome = await new PersonnelService(connectionString, user).RecordProjectPaymentAsync(w, request, ct); if (outcome.Status != PaymentStatus.Recorded) throw new InvalidOperationException(outcome.Reason ?? "Ödeme kaydedilemedi; talep beklemede kaldı.");
            using var ledgerQuery = w.Command("SELECT LedgerId FROM dbo.PaymentLedger WHERE RequestId=@id AND Kind=N'Payment'"); Id(ledgerQuery, "@id", input.RequestId); ledger = Convert.ToInt64(await ledgerQuery.ExecuteScalarAsync(ct));
        }
        using (var c = w.Command("UPDATE dbo.ProjectPaymentProposals SET Status=@status,DecidedByAccountId=@actor,DecidedBy=@name,DecidedUtc=SYSUTCDATETIME(),DecisionReason=@reason,DecisionVersion=@version,LedgerId=@ledger WHERE RequestId=@id AND RecordVersion=@version AND Status=N'Pending'"))
        { c.Text("@status", status, 12); Id(c, "@actor", actor); c.Text("@name", user.Username); c.Text("@reason", reason, 500); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version; c.Parameters.Add("@ledger", SqlDbType.BigInt).Value = (object?)ledger ?? DBNull.Value; Id(c, "@id", input.RequestId); if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Talep değişti; ödeme ve karar geri alındı."); }
        await w.CommitAsync(ct);
    }
    internal async Task<PersonnelPage> PageAsync(Guid project, long index, int size, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"project-payments/{project:D}?index={index}&size={size}", ct);
        if (index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz sayfa.");
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Payments, ct);
        using var count = w.Command("SELECT COUNT_BIG(*) FROM dbo.ProjectPaymentProposals WHERE ProjectId=@id"); Id(count, "@id", project); long total = Convert.ToInt64(await count.ExecuteScalarAsync(ct)); index = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var c = w.Command(@"SELECT P.RequestId,P.PersonId,P.PersonnelGroup,P.EmploymentId,P.Amount,P.Purpose,P.Status,P.PreparedByAccountId,P.PreparedBy,P.PreparedUtc,P.PrepareReason,P.DecidedBy,P.DecidedUtc,P.DecisionReason,P.LedgerId,P.RecordVersion,L.ReversedUtc,
 COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD FROM dbo.ProjectPaymentProposals P
 LEFT JOIN dbo.Calisan C ON C.PersonId=P.PersonId AND P.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=P.PersonId AND P.PersonnelGroup=1 LEFT JOIN dbo.PaymentLedger L ON L.LedgerId=P.LedgerId
 WHERE P.ProjectId=@id ORDER BY P.PreparedUtc DESC,P.RequestId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY"); Id(c, "@id", project); c.Parameters.Add("@skip", SqlDbType.BigInt).Value = index * size; c.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return new(data, total, index, size);
    }
}
