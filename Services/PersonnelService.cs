using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class PersonnelService(string connectionString, UserSession user) : IPaymentGateway, IAdvancePaymentGateway
{
    // Compatibility API for legacy callers; live screens use bounded PageAsync.
    public async Task<DataTable> ListAsync(PersonnelGroup? group, CancellationToken token = default, string tc = "", string first = "", string last = "", bool includeArchived = false)
        => (await ReadPageAsync(group, 0, null, token, tc, first, last, includeArchived, false, true).ConfigureAwait(false)).Rows;
    public async Task SaveAsync(PersonnelGroup group, PersonnelDetails person, bool insert, CancellationToken token = default, byte[]? expectedVersion = null, DateTime? employmentStart = null)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("personnel/save", new AccountingApp.Contracts.PersonWrite((AccountingApp.Contracts.PersonnelGroup)group, person.Tc, person.FirstName, person.LastName, person.Iban, person.Amount, insert, expectedVersion, RemoteApi.Reason, employmentStart), token).ConfigureAwait(false); return; }
        user.DemandEdit();
        user.DemandViewIban();
        if (!insert) user.DemandViewSalary();
        if (insert && !user.CanViewSalary && person.Amount != 0) throw new UnauthorizedAccessException("Ücret görüntüleme kapalıyken yalnız sıfır açılış bakiyesi kullanılabilir.");
        byte[]? version = insert ? null : PersonnelConcurrency.RequireVersion(expectedVersion);
        person = PersonnelInput.Normalize(person);
        if (insert && !EmploymentService.Day(employmentStart).HasValue) throw new ArgumentException("İşe giriş tarihi gerekiyor.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work); decimal previous = 0;
        if (insert)
        {
            using var check = work.Command("SELECT COUNT(*) FROM dbo.PersonnelIdentities WITH(UPDLOCK,HOLDLOCK) WHERE TC=@tc"); check.Tc(person.Tc);
            if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0) throw new InvalidOperationException("Bu TC etkin veya arşivlenmiş bir personel kartında kayıtlı. Arşiv ve geçmiş ekranından aynı kartı tekrar etkinleştirin; ikinci kart açılamaz.");
        }
        else
        {
            previous = await repo.LockPersonAsync(group, person.Tc, token, version).ConfigureAwait(false);
        }
        if (person.Amount != previous)
            await AccountAuthorization.DemandAsync(work, user, AccountAccess.Pay, token).ConfigureAwait(false);
        await repo.WriteAsync(group, person, insert, token, version).ConfigureAwait(false);
        if (insert) await EmploymentService.InitialDateAsync(work, group, person.Tc, employmentStart!.Value, token).ConfigureAwait(false);
        await LedgerAsync(work, group, person.Tc, person.Amount - previous, "Allocation", null, token).ConfigureAwait(false);
        await AuditAsync(work, insert ? "Person.Add" : "Person.Update", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event(insert ? "Person.Add" : "Person.Update");
    }
    // Kept for callers compiled against the old API; the operation now archives.
    public Task DeleteAsync(PersonnelGroup group, long tc, byte[]? expectedVersion, CancellationToken token = default, DateTime? employmentEnd = null) => ArchiveAsync(group, tc, expectedVersion, token, employmentEnd);
    public async Task SaveLeavesAsync(IReadOnlyList<(LeaveTarget Target, LeavePeriod Leave)> plan, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("leaves/plan", new AccountingApp.Contracts.LeavePlan(plan.Select(p => new AccountingApp.Contracts.LeaveItem(p.Target.Tc, p.Target.PersonVersion, p.Target.LeaveVersion, DateTime.SpecifyKind(p.Leave.Start.Date, DateTimeKind.Unspecified), DateTime.SpecifyKind(p.Leave.End.Date, DateTimeKind.Unspecified))).ToArray(), RemoteApi.Reason), token).ConfigureAwait(false); return; }
        user.DemandEdit();
        var snapshots = plan.Select(p => (Target: LeaveTarget.RequireSnapshot(p.Target), p.Leave)).ToArray();
        if (snapshots.Length == 0 || snapshots.Select(p => p.Target.Tc).Distinct().Count() != snapshots.Length) throw new ArgumentException("İzin planı boş veya aynı personel birden fazla kez seçilmiş.");
        foreach (var p in snapshots) _ = Calculations.LeaveDays(p.Leave.Start, p.Leave.End);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work);
        foreach (var p in snapshots.OrderBy(p => p.Target.Tc)) await repo.SaveLeaveAsync(p.Target, p.Leave, token, user.Username).ConfigureAwait(false);
        await AuditAsync(work, "Leave.SavePlan", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Leave.SavePlan");
    }
    public async Task<Guid> PayAsync(PersonnelGroup group, long tc, decimal amount, Guid requestId, CancellationToken token = default, byte[]? expectedVersion = null)
    {
        user.DemandPay(); if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount || requestId == Guid.Empty) throw new ArgumentException("Pozitif, iki ondalıklı ödeme tutarı ve işlem kimliği gerekiyor.");
        var outcome = await PayCoreAsync(group, tc, amount, requestId, null, expectedVersion?.ToArray(), token).ConfigureAwait(false);
        if (outcome.Status == PaymentStatus.Rejected) throw new InvalidOperationException(outcome.Reason);
        return requestId;
    }
    public async Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, byte[] expectedVersion, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Payment(await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.PaymentIntent>("payments/prepare", new AccountingApp.Contracts.PaymentPrepare((AccountingApp.Contracts.PersonnelGroup)group, tc, amount, expectedVersion, AccountingApp.Contracts.PaymentPurpose.Settlement, null, RemoteApi.Reason), token).ConfigureAwait(false));
        user.DemandPay();
        var selected = PaymentTarget.RequireSnapshot(new(group, tc, expectedVersion));
        if (amount <= 0 || amount > 9999999999999999.99m || decimal.Round(amount, 2) != amount) throw new ArgumentException("Pozitif, iki ondalıklı ödeme tutarı gerekiyor.");
        var request = new PaymentRequest(Guid.NewGuid(), group, tc, amount, 0) { FormatVersion = 3, PersonVersion = selected.PersonVersion, AuditReason = AuditContext.Reason };
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Pay, token).ConfigureAwait(false);
        decimal balance;
        try { balance = await new PersonnelRepository(work).LockPersonAsync(group, tc, token, selected.PersonVersion).ConfigureAwait(false); }
        catch (InvalidOperationException) { throw new InvalidOperationException(PaymentTarget.ConflictMessage); }
        if (amount > balance) throw new InvalidOperationException("Ödeme tutarı kalan bakiyeyi aşıyor.");
        long closure = await ClosureIdAsync(work, group, tc, token).ConfigureAwait(false);
        Guid personId = await new PersonnelRepository(work).IdentityAsync(group, tc, token).ConfigureAwait(false);
        request = request with { ClosureId = closure, PersonId = personId }; request.Validate();
        await work.CommitAsync(token).ConfigureAwait(false);
        return request;
    }
    public async Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Payment(await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.PaymentResult>("payments/inspect", RemoteApi.Payment(request.Snapshot()), token).ConfigureAwait(false));
        user.DemandViewSalary();
        request.Validate();
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        using var cmd = work.Command("SELECT TC,PersonnelGroup,Delta,Kind,ReversedUtc,PersonId,PaymentPurpose,ProjectId FROM dbo.PaymentLedger WHERE RequestId=@id");
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = request.RequestId;
        using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return new(PaymentStatus.Unconfirmed);
        return ReadPaymentOutcome(reader, request.Group, request.Tc, request.Amount, request.PersonId, request.Purpose, request.ProjectId);
    }
    public async Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Payment(await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.PaymentResult>("payments/submit", RemoteApi.Payment(request.Snapshot()), token).ConfigureAwait(false));
        user.DemandPay(); request = request.Snapshot();
        using var scope = AuditContext.UseReason(request.AuditReason);
        return await PayCoreAsync(request.Group, request.Tc, request.Amount, request.RequestId, request.ClosureId, request.PersonVersion, token,
            request.FormatVersion != 3, request.PersonId, request.Purpose, request.ProjectId).ConfigureAwait(false);
    }
    private static PaymentOutcome ReadPaymentOutcome(SqlDataReader reader, PersonnelGroup group, long tc, decimal amount, Guid? personId = null,
        PaymentPurpose purpose = PaymentPurpose.Settlement, Guid? projectId = null)
    {
        if (reader.GetInt64(0) != tc || reader.GetInt32(1) != (int)group || reader.GetDecimal(2) != -amount || reader.GetString(3) != "Payment" ||
            (personId.HasValue && (reader.IsDBNull(5) || reader.GetGuid(5) != personId.Value)))
            throw new InvalidOperationException("İşlem kimliği farklı bir ödeme için kullanılmış. Yeni ödeme başlatılmadı.");
        string storedPurpose = reader.GetString(6);
        if ((storedPurpose != purpose.ToString() && !(storedPurpose == "Unclassified" && purpose == PaymentPurpose.Settlement)) ||
            (reader.IsDBNull(7) ? null : (Guid?)reader.GetGuid(7)) != projectId)
            throw new InvalidOperationException("İşlem kimliği farklı ödeme türüne/projeye ait; yeni ödeme yapılmadı.");
        return new(reader.IsDBNull(4) ? PaymentStatus.Recorded : PaymentStatus.Reversed);
    }
    private static async Task<long> ClosureIdAsync(IUnitOfWork work, PersonnelGroup group, long tc, CancellationToken token)
    {
        using var cmd = work.Command("SELECT COALESCE(MAX(LedgerId),0) FROM dbo.PaymentLedger WHERE TC=@tc AND PersonnelGroup=@group AND Kind=N'Closure'");
        cmd.Tc(tc); cmd.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false));
    }
    private async Task<PaymentOutcome> PayCoreAsync(PersonnelGroup group, long tc, decimal amount, Guid requestId, long? expectedClosure,
        byte[]? expectedVersion, CancellationToken token, bool legacyRequest = false, Guid? expectedPersonId = null,
        PaymentPurpose purpose = PaymentPurpose.Settlement, Guid? projectId = null)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Pay, token).ConfigureAwait(false);
        var result = await PayInWorkAsync(work, group, tc, amount, requestId, expectedClosure, expectedVersion, token, legacyRequest, expectedPersonId, purpose, projectId, false);
        if (result.Status is PaymentStatus.Recorded or PaymentStatus.Reversed) { await work.CommitAsync(token); AppLog.Event("Payment.Record"); }
        return result;
    }
    internal async Task<PaymentOutcome> RecordProjectPaymentAsync(IUnitOfWork work, PaymentRequest request, CancellationToken token)
    {
        request.Validate();
        await ProjectAuthorization.DemandAsync(work, user, request.ProjectId ?? throw new ArgumentException("Proje gerekiyor."), ProjectAccess.ApprovePayment, token);
        return await PayInWorkAsync(work, request.Group, request.Tc, request.Amount, request.RequestId, request.ClosureId, request.PersonVersion, token, false, request.PersonId, request.Purpose, request.ProjectId, true);
    }
    private async Task<PaymentOutcome> PayInWorkAsync(IUnitOfWork work, PersonnelGroup group, long tc, decimal amount, Guid requestId, long? expectedClosure,
        byte[]? expectedVersion, CancellationToken token, bool legacyRequest, Guid? expectedPersonId, PaymentPurpose purpose, Guid? projectId, bool projectApproval)
    {
        // Same lock order for retries: person first, then request key. The unique request ID makes retries idempotent.
        bool personExists = true;
        try { await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false); }
        catch (InvalidOperationException) { personExists = false; }
        using (var check = work.Command("SELECT TC,PersonnelGroup,Delta,Kind,ReversedUtc,PersonId,PaymentPurpose,ProjectId FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id"))
        {
            check.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            using var reader = await check.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                return ReadPaymentOutcome(reader, group, tc, amount, expectedPersonId, purpose, projectId);
            }
        }
        // An already recorded request is returned above even when the selected
        // row changed or disappeared. Only a new debit requires its exact version.
        if (projectId.HasValue && !projectApproval) return new(PaymentStatus.Rejected, "Proje ödemeleri proje hazırlama/onay akışından kaydedilmelidir.");
        using (var reserved = work.Command("SELECT COUNT(*) FROM dbo.ProjectPaymentProposals WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id"))
        { reserved.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value=requestId; if (!projectApproval && Convert.ToInt32(await reserved.ExecuteScalarAsync(token))!=0) throw new InvalidOperationException("İşlem kimliği bir proje ödeme talebine ayrılmış."); }
        if (legacyRequest) return new(PaymentStatus.Rejected, PaymentTarget.LegacyMessage);
        if (!personExists) return new(PaymentStatus.Rejected, PaymentTarget.ConflictMessage);
        byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
        try { await new PersonnelRepository(work).LockPersonAsync(group, tc, token, version).ConfigureAwait(false); }
        catch (InvalidOperationException) { return new(PaymentStatus.Rejected, PaymentTarget.ConflictMessage); }
        if (expectedPersonId.HasValue && await new PersonnelRepository(work).IdentityAsync(group, tc, token).ConfigureAwait(false) != expectedPersonId.Value)
            return new(PaymentStatus.Rejected, PaymentTarget.ConflictMessage);
        if (expectedClosure.HasValue && expectedClosure.Value != await ClosureIdAsync(work, group, tc, token).ConfigureAwait(false))
            return new(PaymentStatus.Rejected, "Personel kaydı bu ödeme isteğinden sonra kapatılmış. Yeni kaydın bakiyesi korunmuştur.");
        if (!Enum.IsDefined(purpose) || (purpose == PaymentPurpose.Advance && (!projectId.HasValue || projectId == Guid.Empty)))
            throw new ArgumentException("Ödeme türü/proje geçersiz.");
        if (projectId.HasValue) await ProjectFinanceService.DemandProjectAsync(work, projectId.Value, token).ConfigureAwait(false);
        using var cmd = work.Command($"UPDATE {PersonnelRepository.Table(group)} WITH(UPDLOCK,ROWLOCK) SET PARA=PARA-@amount WHERE TC=@tc AND ArchivedUtc IS NULL AND RecordVersion=@version AND PARA-@amount>=-9999999999999999.99 AND (@advance=1 OR PARA>=@amount)"); cmd.Tc(tc); cmd.Money("@amount", amount);
        cmd.Parameters.Add("@advance", SqlDbType.Bit).Value = purpose == PaymentPurpose.Advance;
        cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) return new(PaymentStatus.Rejected, "Ödeme kaydedilemedi: tutar kalan bakiyeyi aşıyor.");
        await LedgerAsync(work, group, tc, -amount, "Payment", requestId, token, purpose, projectId).ConfigureAwait(false);
        await AuditAsync(work, "Payment.Record", token).ConfigureAwait(false); return new(PaymentStatus.Recorded);
    }
    public async Task ReversePaymentAsync(Guid requestId, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("payments/reverse", new AccountingApp.Contracts.PaymentReverse(requestId, RemoteApi.Reason), token).ConfigureAwait(false); return; }
        user.DemandAdmin(); await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        long tc; PersonnelGroup group; decimal amount; Guid personId; long originalLedgerId;
        // Read immutable payment identity without locks, then use the standard person -> ledger lock order.
        using (var find = work.Command("SELECT TC,PersonnelGroup,-Delta,PersonId,LedgerId FROM dbo.PaymentLedger WITH(READUNCOMMITTED) WHERE RequestId=@id AND Kind=N'Payment'"))
        {
            find.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
            using var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Ödeme bulunamadı.");
            tc = r.GetInt64(0); group = (PersonnelGroup)r.GetInt32(1); amount = r.GetDecimal(2);
            if (r.IsDBNull(3)) throw new InvalidOperationException("Bu eski ödemenin personel kimliği doğrulanamıyor; otomatik iptal edilemez.");
            personId = r.GetGuid(3);
            originalLedgerId = r.GetInt64(4);
        }
        await new PersonnelRepository(work).LockPersonAsync(group, tc, token).ConfigureAwait(false);
        if (await new PersonnelRepository(work).IdentityAsync(group, tc, token).ConfigureAwait(false) != personId)
            throw new InvalidOperationException("Ödeme farklı bir personel kartına ait; iptal edilmedi.");
        using var check = work.Command("SELECT ReversedUtc FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE RequestId=@id AND Kind=N'Payment'"); check.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId;
        object? reversed = await check.ExecuteScalarAsync(token).ConfigureAwait(false);
        if (reversed == null || reversed != DBNull.Value) throw new InvalidOperationException("Ödeme daha önce iptal edilmiş veya bulunamadı.");
        await PaymentAllocationService.DemandPaymentFreeAsync(work,originalLedgerId,token).ConfigureAwait(false);
        await BankReconciliationService.DemandPaymentFreeAsync(work,originalLedgerId,token).ConfigureAwait(false);
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
        using var update = work.Command($"UPDATE {PersonnelRepository.Table(group)} SET PARA=PARA+@amount WHERE PersonId=@person AND ArchivedUtc IS NULL"); update.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId; update.Money("@amount", amount);
        if (await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        using var mark = work.Command("UPDATE dbo.PaymentLedger SET ReversedUtc=SYSUTCDATETIME() WHERE RequestId=@id"); mark.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = requestId; await mark.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await LedgerAsync(work, group, tc, amount, "Reversal", null, token, reversesLedgerId: originalLedgerId).ConfigureAwait(false); await AuditAsync(work, "Payment.Reverse", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Payment.Reverse");
    }
    private async Task LedgerAsync(IUnitOfWork work, PersonnelGroup group, long tc, decimal delta, string kind, Guid? requestId, CancellationToken token,
        PaymentPurpose purpose = PaymentPurpose.Settlement, Guid? projectId = null, long? reversesLedgerId = null)
    {
        using var cmd = work.Command($"INSERT INTO dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor,RequestId,PaymentPurpose,ProjectId,ReversesLedgerId) SELECT PersonId,@tc,@group,@delta,@kind,@actor,@id,@purpose,@project,@reverses FROM {PersonnelRepository.Table(group)} WHERE TC=@tc");
        cmd.Tc(tc); cmd.Parameters.Add("@group", SqlDbType.Int).Value = (int)group; cmd.Money("@delta", delta); cmd.Text("@kind", kind, 20); cmd.Text("@actor", user.Username);
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = (object?)requestId ?? DBNull.Value;
        cmd.Text("@purpose", kind == "Payment" ? purpose.ToString() : "Unclassified", 20);
        cmd.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = (object?)projectId ?? DBNull.Value;
        cmd.Parameters.Add("@reverses", SqlDbType.BigInt).Value = (object?)reversesLedgerId ?? DBNull.Value;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Ödeme defteri personel kimliğine bağlanamadı.");
    }
    internal async Task AuditAsync(IUnitOfWork work, string action, CancellationToken token)
    {
        using var cmd = work.Command(@"INSERT dbo.AuditLog(Actor,Action,Reason,ReasonSource,CorrelationId,SqlPrincipal,Source,ActorAccountId)
            VALUES(@actor,@action,CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
            CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
            TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),ORIGINAL_LOGIN(),N'Application',
            TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')))");
        cmd.Text("@actor", user.Username); cmd.Text("@action", action, 64); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    public async Task<DataTable> LedgerAsync(long? tc = null, CancellationToken token = default)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        using var cmd = work.Command(@"WITH PeriodLedger AS(SELECT P.*,L.EmploymentId FROM dbo.PaymentLedger P
            LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId))
            SELECT LedgerId,TC,PersonnelGroup,CreatedUtc,Kind,Delta,PersonId,EmploymentId,PaymentPurpose,ProjectId,FinanceEntryId,
            SUM(Delta) OVER(PARTITION BY TC,PersonnelGroup,PersonId,EmploymentId ORDER BY LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,Actor,RequestId,ReversedUtc
            FROM PeriodLedger WHERE (@tc IS NULL OR TC=@tc) ORDER BY LedgerId");
        cmd.Parameters.Add("@tc", SqlDbType.BigInt).Value = (object?)tc ?? DBNull.Value;
        var result = await cmd.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return SensitiveData.Mask(result, user);
    }
}
