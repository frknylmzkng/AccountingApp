using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class PersonnelService(string connectionString, UserSession user) : IPaymentGateway
{
    public async Task<DataTable> ListAsync(PersonnelGroup? group, CancellationToken token = default, string tc = "", string first = "", string last = "")
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        const string all = "SELECT TC,AD,SOYAD,IBAN,PARA,N'Genel' GRUP,RecordVersion FROM dbo.Calisan UNION ALL SELECT TC,AD,SOYAD,IBAN,PARA,N'Joker',RecordVersion FROM dbo.Joker";
        using var cmd = work.Command($@"SELECT P.*,I.IZINBASLANGIC,I.IZINBITIS,I.IzinSuresi,I.LeaveVersion FROM ({all}) P
            LEFT JOIN dbo.izinler I ON P.TC=I.TC AND P.GRUP=N'Genel'
            WHERE (@group=N'' OR P.GRUP=@group) AND (@tc=N'' OR CONVERT(nvarchar(20),P.TC)=@tc)
            AND P.AD LIKE @first ESCAPE '~' AND P.SOYAD LIKE @last ESCAPE '~' ORDER BY P.TC,I.IZINBASLANGIC");
        cmd.Text("@group", group == null ? "" : group == PersonnelGroup.General ? "Genel" : "Joker"); cmd.Text("@tc", tc);
        static string Like(string text) => "%" + text.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
        cmd.Text("@first", Like(first), 210); cmd.Text("@last", Like(last), 210);
        var result = await cmd.TableAsync(token).ConfigureAwait(false); result.Columns["TC"]!.ReadOnly = true; result.Columns[PersonnelConcurrency.ColumnName]!.ReadOnly = true;
        result.Columns[LeaveTarget.ColumnName]!.ReadOnly = true;
        await work.CommitAsync(token).ConfigureAwait(false); return result;
    }
    public async Task SaveAsync(PersonnelGroup group, PersonnelDetails person, bool insert, CancellationToken token = default, byte[]? expectedVersion = null)
    {
        user.DemandEdit();
        byte[]? version = insert ? null : PersonnelConcurrency.RequireVersion(expectedVersion);
        if (!PersonnelInput.TryCreate(person.Tc.ToString(), person.FirstName, person.LastName, person.Iban, MoneyInput.Format(person.Amount), out _, out string error)) throw new ArgumentException(error);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work); decimal previous = 0;
        if (insert)
        {
            using var check = work.Command("SELECT (SELECT COUNT(*) FROM dbo.Calisan WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc)+(SELECT COUNT(*) FROM dbo.Joker WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc)"); check.Tc(person.Tc);
            if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0) throw new InvalidOperationException("Bu TC zaten kayıtlı.");
        }
        else
        {
            previous = await repo.LockPersonAsync(group, person.Tc, token).ConfigureAwait(false);
        }
        await repo.WriteAsync(group, person, insert, token, version).ConfigureAwait(false);
        await LedgerAsync(work, group, person.Tc, person.Amount - previous, "Allocation", null, token).ConfigureAwait(false);
        await AuditAsync(work, insert ? "Person.Add" : "Person.Update", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event(insert ? "Person.Add" : "Person.Update");
    }
    public async Task DeleteAsync(PersonnelGroup group, long tc, byte[]? expectedVersion, CancellationToken token = default)
    {
        user.DemandEdit(); byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        // Validate and lock the selected version before touching its leaves or ledger.
        decimal balance = await new PersonnelRepository(work).LockPersonAsync(group, tc, token, version).ConfigureAwait(false);
        if (group == PersonnelGroup.General) { using var leaves = work.Command("DELETE FROM dbo.izinler WHERE TC=@tc"); leaves.Tc(tc); await leaves.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        using var cmd = work.Command($"DELETE FROM {PersonnelRepository.Table(group)} WHERE TC=@tc AND RecordVersion=@version"); cmd.Tc(tc); cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        await LedgerAsync(work, group, tc, -balance, "Closure", null, token).ConfigureAwait(false);
        await AuditAsync(work, "Person.Delete", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Person.Delete");
    }
    public async Task SaveLeavesAsync(IReadOnlyList<(LeaveTarget Target, LeavePeriod Leave)> plan, CancellationToken token = default)
    {
        user.DemandEdit();
        var snapshots = plan.Select(p => (Target: LeaveTarget.RequireSnapshot(p.Target), p.Leave)).ToArray();
        if (snapshots.Length == 0 || snapshots.Select(p => p.Target.Tc).Distinct().Count() != snapshots.Length) throw new ArgumentException("İzin planı boş veya aynı personel birden fazla kez seçilmiş.");
        foreach (var p in snapshots) _ = Calculations.LeaveDays(p.Leave.Start, p.Leave.End);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work);
        foreach (var p in snapshots.OrderBy(p => p.Target.Tc)) await repo.SaveLeaveAsync(p.Target, p.Leave, token).ConfigureAwait(false);
        await AuditAsync(work, "Leave.SavePlan", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Leave.SavePlan");
    }
    public async Task<Guid> PayAsync(PersonnelGroup group, long tc, decimal amount, Guid requestId, CancellationToken token = default)
    {
        user.DemandEdit(); if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount || requestId == Guid.Empty) throw new ArgumentException("Pozitif, iki ondalıklı ödeme tutarı ve işlem kimliği gerekiyor.");
        var outcome = await PayCoreAsync(group, tc, amount, requestId, null, token).ConfigureAwait(false);
        if (outcome.Status == PaymentStatus.Rejected) throw new InvalidOperationException(outcome.Reason);
        return requestId;
    }
    public async Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, CancellationToken token)
    {
        user.DemandEdit();
        var request = new PaymentRequest(Guid.NewGuid(), group, tc, amount, 0); request.Validate();
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        decimal balance = await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false);
        if (amount > balance) throw new InvalidOperationException("Ödeme tutarı kalan bakiyeyi aşıyor.");
        long closure = await ClosureIdAsync(work, group, tc, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
        return request with { ClosureId = closure };
    }
    public async Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token)
    {
        request.Validate();
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        using var cmd = work.Command("SELECT TC,PersonnelGroup,Delta,Kind,ReversedUtc FROM dbo.PaymentLedger WHERE RequestId=@id");
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = request.RequestId;
        using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return new(PaymentStatus.Unconfirmed);
        return ReadPaymentOutcome(reader, request.Group, request.Tc, request.Amount);
    }
    public Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token)
    {
        user.DemandEdit(); request.Validate();
        return PayCoreAsync(request.Group, request.Tc, request.Amount, request.RequestId, request.ClosureId, token);
    }
    private static PaymentOutcome ReadPaymentOutcome(SqlDataReader reader, PersonnelGroup group, long tc, decimal amount)
    {
        if (reader.GetInt64(0) != tc || reader.GetInt32(1) != (int)group || reader.GetDecimal(2) != -amount || reader.GetString(3) != "Payment")
            throw new InvalidOperationException("İşlem kimliği farklı bir ödeme için kullanılmış. Yeni ödeme başlatılmadı.");
        return new(reader.IsDBNull(4) ? PaymentStatus.Recorded : PaymentStatus.Reversed);
    }
    private static async Task<long> ClosureIdAsync(IUnitOfWork work, PersonnelGroup group, long tc, CancellationToken token)
    {
        using var cmd = work.Command("SELECT COALESCE(MAX(LedgerId),0) FROM dbo.PaymentLedger WHERE TC=@tc AND PersonnelGroup=@group AND Kind=N'Closure'");
        cmd.Tc(tc); cmd.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false));
    }
    private async Task<PaymentOutcome> PayCoreAsync(PersonnelGroup group, long tc, decimal amount, Guid requestId, long? expectedClosure, CancellationToken token)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        // Same lock order for retries: person first, then request key. The unique request ID makes retries idempotent.
        try { await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false); }
        catch (InvalidOperationException ex) { return new(PaymentStatus.Rejected, ex.Message); }
        using (var check = work.Command("SELECT TC,PersonnelGroup,Delta,Kind,ReversedUtc FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id"))
        {
            check.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            using var reader = await check.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                return ReadPaymentOutcome(reader, group, tc, amount);
            }
        }
        if (expectedClosure.HasValue && expectedClosure.Value != await ClosureIdAsync(work, group, tc, token).ConfigureAwait(false))
            return new(PaymentStatus.Rejected, "Personel kaydı bu ödeme isteğinden sonra kapatılmış. Yeni kaydın bakiyesi korunmuştur.");
        using var cmd = work.Command($"UPDATE {PersonnelRepository.Table(group)} WITH(UPDLOCK,ROWLOCK) SET PARA=PARA-@amount WHERE TC=@tc AND PARA>=@amount"); cmd.Tc(tc); cmd.Money("@amount", amount);
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) return new(PaymentStatus.Rejected, "Ödeme kaydedilemedi: tutar kalan bakiyeyi aşıyor.");
        await LedgerAsync(work, group, tc, -amount, "Payment", requestId, token).ConfigureAwait(false);
        await AuditAsync(work, "Payment.Record", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Payment.Record"); return new(PaymentStatus.Recorded);
    }
    public async Task ReversePaymentAsync(Guid requestId, CancellationToken token = default)
    {
        user.DemandAdmin(); await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        long tc; PersonnelGroup group; decimal amount;
        // Read immutable payment identity without locks, then use the standard person -> ledger lock order.
        using (var find = work.Command("SELECT TC,PersonnelGroup,-Delta FROM dbo.PaymentLedger WITH(READUNCOMMITTED) WHERE RequestId=@id AND Kind=N'Payment'"))
        {
            find.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            using var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Ödeme bulunamadı.");
            tc = r.GetInt64(0); group = (PersonnelGroup)r.GetInt32(1); amount = r.GetDecimal(2);
        }
        await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false);
        using var check = work.Command("SELECT ReversedUtc FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id AND Kind=N'Payment'"); check.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
        object? reversed = await check.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (reversed == null || reversed != DBNull.Value) throw new InvalidOperationException("Ödeme daha önce iptal edilmiş veya bulunamadı.");
        // Deletes record a Closure even for a zero balance. The person lock serializes
        // this check with deletion/recreation, so an old payment cannot credit a new record.
        using (var closed = work.Command(@"SELECT COUNT(*) FROM dbo.PaymentLedger C
            INNER JOIN dbo.PaymentLedger P ON P.RequestId=@id AND P.Kind=N'Payment'
            WHERE C.TC=P.TC AND C.PersonnelGroup=P.PersonnelGroup
                AND C.Kind=N'Closure' AND C.LedgerId>P.LedgerId"))
        {
            closed.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            if (Convert.ToInt32(await closed.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0)
                throw new InvalidOperationException("Bu ödeme kapanmış önceki personel kaydına ait. Yeni kaydın bakiyesini etkilememesi için iptal edilemez.");
        }
        using var update = work.Command($"UPDATE {PersonnelRepository.Table(group)} SET PARA=PARA+@amount WHERE TC=@tc"); update.Tc(tc); update.Money("@amount", amount); await update.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        using var mark = work.Command("UPDATE dbo.PaymentLedger SET ReversedUtc=SYSUTCDATETIME() WHERE RequestId=@id"); mark.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId; await mark.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await LedgerAsync(work, group, tc, amount, "Reversal", null, token).ConfigureAwait(false); await AuditAsync(work, "Payment.Reverse", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Payment.Reverse");
    }
    private async Task LedgerAsync(IUnitOfWork work, PersonnelGroup group, long tc, decimal delta, string kind, Guid? requestId, CancellationToken token)
    {
        using var cmd = work.Command("INSERT INTO dbo.PaymentLedger(TC,PersonnelGroup,Delta,Kind,Actor,RequestId) VALUES(@tc,@group,@delta,@kind,@actor,@id)");
        cmd.Tc(tc); cmd.Parameters.Add("@group", SqlDbType.Int).Value = (int)group; cmd.Money("@delta", delta); cmd.Text("@kind", kind, 20); cmd.Text("@actor", user.Username);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = (object?)requestId ?? DBNull.Value; await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    internal async Task AuditAsync(IUnitOfWork work, string action, CancellationToken token)
    {
        using var cmd = work.Command("INSERT INTO dbo.AuditLog(Actor,Action) VALUES(@actor,@action)"); cmd.Text("@actor", user.Username); cmd.Text("@action", action, 64); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    public async Task<DataTable> LedgerAsync(long? tc = null, CancellationToken token = default)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        using var cmd = work.Command(@"SELECT LedgerId,TC,PersonnelGroup,CreatedUtc,Kind,Delta,
            SUM(Delta) OVER(PARTITION BY TC,PersonnelGroup ORDER BY LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,Actor,RequestId,ReversedUtc
            FROM dbo.PaymentLedger WHERE (@tc IS NULL OR TC=@tc) ORDER BY LedgerId");
        cmd.Parameters.Add("@tc", SqlDbType.BigInt).Value = (object?)tc ?? DBNull.Value;
        var result = await cmd.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return result;
    }
}
