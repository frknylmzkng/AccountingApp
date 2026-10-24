using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class PersonnelService(string connectionString, UserSession user)
{
    public async Task<DataTable> ListAsync(PersonnelGroup? group, CancellationToken token = default, string tc = "", string first = "", string last = "")
    {
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(token).ConfigureAwait(false);
        const string all = "SELECT TC,AD,SOYAD,IBAN,PARA,N'Genel' GRUP FROM dbo.Calisan UNION ALL SELECT TC,AD,SOYAD,IBAN,PARA,N'Joker' FROM dbo.Joker";
        using var cmd = new SqlCommand($@"SELECT P.*,I.IZINBASLANGIC,I.IZINBITIS,I.IzinSuresi FROM ({all}) P
            LEFT JOIN dbo.izinler I ON P.TC=I.TC AND P.GRUP=N'Genel'
            WHERE (@group=N'' OR P.GRUP=@group) AND (@tc=N'' OR CONVERT(nvarchar(20),P.TC)=@tc)
            AND P.AD LIKE @first ESCAPE '~' AND P.SOYAD LIKE @last ESCAPE '~' ORDER BY P.TC,I.IZINBASLANGIC", connection);
        cmd.Text("@group", group == null ? "" : group == PersonnelGroup.General ? "Genel" : "Joker"); cmd.Text("@tc", tc);
        static string Like(string text) => "%" + text.Replace("~", "~~").Replace("%", "~%").Replace("_", "~_").Replace("[", "~[") + "%";
        cmd.Text("@first", Like(first), 210); cmd.Text("@last", Like(last), 210);
        var result = await cmd.TableAsync(token).ConfigureAwait(false); result.Columns["TC"]!.ReadOnly = true; return result;
    }
    public async Task SaveAsync(PersonnelGroup group, PersonnelDetails person, bool insert, CancellationToken token = default, decimal? expectedAmount = null)
    {
        user.DemandEdit();
        if (!PersonnelInput.TryCreate(person.Tc.ToString(), person.FirstName, person.LastName, person.Iban, MoneyInput.Format(person.Amount), out _, out string error)) throw new ArgumentException(error);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work); decimal previous = 0;
        if (insert)
        {
            using var check = work.Command("SELECT (SELECT COUNT(*) FROM dbo.Calisan WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc)+(SELECT COUNT(*) FROM dbo.Joker WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc)"); check.Tc(person.Tc);
            if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0) throw new InvalidOperationException("Bu TC zaten kayıtlı.");
        }
        else
        {
            previous = await repo.LockPersonAsync(group, person.Tc, token).ConfigureAwait(false);
            if (expectedAmount == null || previous != expectedAmount.Value) throw new InvalidOperationException("Personelin kalan tutarı değişmiş olabilir. Listeyi yenileyip kaydı yeniden seçin; güncel bakiye korunmuştur.");
        }
        await repo.WriteAsync(group, person, insert, token).ConfigureAwait(false);
        await LedgerAsync(work, group, person.Tc, person.Amount - previous, "Allocation", null, token).ConfigureAwait(false);
        await AuditAsync(work, insert ? "Person.Add" : "Person.Update", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event(insert ? "Person.Add" : "Person.Update");
    }
    public async Task DeleteAsync(PersonnelGroup group, long tc, CancellationToken token = default)
    {
        user.DemandEdit(); await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        decimal balance = await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false);
        if (group == PersonnelGroup.General) { using var leaves = work.Command("DELETE FROM dbo.izinler WHERE TC=@tc"); leaves.Tc(tc); await leaves.ExecuteNonQueryAsync(token).ConfigureAwait(false); }
        using var cmd = work.Command($"DELETE FROM {PersonnelRepository.Table(group)} WHERE TC=@tc"); cmd.Tc(tc); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await LedgerAsync(work, group, tc, -balance, "Closure", null, token).ConfigureAwait(false);
        await AuditAsync(work, "Person.Delete", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Person.Delete");
    }
    public async Task SaveLeavesAsync(IReadOnlyList<(long Tc, LeavePeriod Leave)> plan, CancellationToken token = default)
    {
        user.DemandEdit(); if (plan.Count == 0 || plan.Select(p => p.Tc).Distinct().Count() != plan.Count) throw new ArgumentException("İzin planı boş veya aynı personel birden fazla kez seçilmiş.");
        foreach (var p in plan) _ = Calculations.LeaveDays(p.Leave.Start, p.Leave.End);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work);
        foreach (var p in plan.OrderBy(p => p.Tc)) await repo.SaveLeaveAsync(p.Tc, p.Leave, token).ConfigureAwait(false);
        await AuditAsync(work, "Leave.SavePlan", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Leave.SavePlan");
    }
    public async Task<Guid> PayAsync(PersonnelGroup group, long tc, decimal amount, Guid requestId, CancellationToken token = default)
    {
        user.DemandEdit(); if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount || requestId == Guid.Empty) throw new ArgumentException("Pozitif, iki ondalıklı ödeme tutarı ve işlem kimliği gerekiyor.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        // Same lock order for retries: person first, then request key. The unique request ID makes retries idempotent.
        await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false);
        using (var check = work.Command("SELECT TC,PersonnelGroup,Delta FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id"))
        {
            check.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            using var reader = await check.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                if (reader.GetInt64(0) != tc || reader.GetInt32(1) != (int)group || reader.GetDecimal(2) != -amount) throw new InvalidOperationException("İşlem kimliği farklı bir ödeme için kullanılmış.");
                return requestId;
            }
        }
        using var cmd = work.Command($"UPDATE {PersonnelRepository.Table(group)} WITH(UPDLOCK,ROWLOCK) SET PARA=PARA-@amount WHERE TC=@tc AND PARA>=@amount"); cmd.Tc(tc); cmd.Money("@amount", amount);
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Ödeme kaydedilemedi: tutar kalan bakiyeyi aşıyor.");
        await LedgerAsync(work, group, tc, -amount, "Payment", requestId, token).ConfigureAwait(false);
        await AuditAsync(work, "Payment.Record", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Payment.Record"); return requestId;
    }
    public async Task ReversePaymentAsync(Guid requestId, CancellationToken token = default)
    {
        user.DemandAdmin(); await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
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
        await using var c = new SqlConnection(connectionString); await c.OpenAsync(token).ConfigureAwait(false);
        using var cmd = new SqlCommand(@"SELECT LedgerId,TC,PersonnelGroup,CreatedUtc,Kind,Delta,
            SUM(Delta) OVER(PARTITION BY TC,PersonnelGroup ORDER BY LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,Actor,RequestId,ReversedUtc
            FROM dbo.PaymentLedger WHERE (@tc IS NULL OR TC=@tc) ORDER BY LedgerId", c);
        cmd.Parameters.Add("@tc", SqlDbType.BigInt).Value = (object?)tc ?? DBNull.Value; return await cmd.TableAsync(token).ConfigureAwait(false);
    }
}
