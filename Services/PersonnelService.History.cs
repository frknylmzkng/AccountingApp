using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed record PersonnelHistory(DataTable Payments, DataTable Leaves, DataTable Events)
{
    internal DataTable LeaveChanges { get; init; } = new();
}

internal sealed partial class PersonnelService
{
    public async Task ArchiveAsync(PersonnelGroup group, long tc, byte[]? expectedVersion, CancellationToken token = default, DateTime? employmentEnd = null)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("personnel/archive", new AccountingApp.Contracts.PersonArchive((AccountingApp.Contracts.PersonnelGroup)group, tc, PersonnelConcurrency.RequireVersion(expectedVersion), RemoteApi.Reason, employmentEnd), token).ConfigureAwait(false); return; }
        user.DemandEdit(); byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        var repo = new PersonnelRepository(work);
        decimal balance = await repo.LockPersonAsync(group, tc, token, version).ConfigureAwait(false);
        Guid id = await repo.IdentityAsync(group, tc, token).ConfigureAwait(false);
        await EmploymentService.CloseAsync(work, group, id, employmentEnd, token).ConfigureAwait(false);
        using var archive = work.Command($"UPDATE {PersonnelRepository.Table(group)} SET ArchivedUtc=SYSUTCDATETIME(),ArchivedBy=@actor WHERE PersonId=@person AND RecordVersion=@version AND ArchivedUtc IS NULL");
        archive.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = id;
        archive.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version; archive.Text("@actor", user.Username);
        if (await archive.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        // An archive closes the active period, not the debt. Keep its balance and
        // all leaves/payments; a zero-delta boundary invalidates old payment intents.
        await LedgerAsync(work, group, tc, 0, "Closure", null, token).ConfigureAwait(false);
        await ArchiveEventAsync(work, id, group, "Archive", balance, token).ConfigureAwait(false);
        await AuditAsync(work, "Person.Archive", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }

    public async Task RestoreArchivedAsync(PersonnelGroup group, Guid personId, byte[]? expectedVersion, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("personnel/restore", new AccountingApp.Contracts.PersonRestore((AccountingApp.Contracts.PersonnelGroup)group, personId, PersonnelConcurrency.RequireVersion(expectedVersion), RemoteApi.Reason), token).ConfigureAwait(false); return; }
        user.DemandEdit(); byte[] version = PersonnelConcurrency.RequireVersion(expectedVersion);
        if (personId == Guid.Empty) throw new ArgumentException("Arşivden bir personel seçin.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Edit, token).ConfigureAwait(false);
        decimal balance;
        using (var find = work.Command($"SELECT PARA FROM {PersonnelRepository.Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND ArchivedUtc IS NOT NULL AND RecordVersion=@version"))
        {
            find.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId; find.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
            object? value = await find.ExecuteScalarAsync(token).ConfigureAwait(false);
            if (value == null) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            balance = value == DBNull.Value ? 0 : Convert.ToDecimal(value);
        }
        await EmploymentService.UndoArchiveAsync(work, group, personId, token).ConfigureAwait(false);
        using var restore = work.Command($"UPDATE {PersonnelRepository.Table(group)} SET ArchivedUtc=NULL,ArchivedBy=NULL WHERE PersonId=@person AND ArchivedUtc IS NOT NULL AND RecordVersion=@version");
        restore.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId; restore.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await restore.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        await ArchiveEventAsync(work, personId, group, "Restore", balance, token).ConfigureAwait(false);
        await AuditAsync(work, "Person.Restore", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }

    private async Task ArchiveEventAsync(IUnitOfWork work, Guid id, PersonnelGroup group, string action, decimal balance, CancellationToken token)
    {
        using var cmd = work.Command("INSERT dbo.PersonnelArchiveEvents(PersonId,PersonnelGroup,Action,Actor,Balance) VALUES(@person,@group,@action,@actor,@balance)");
        cmd.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = id; cmd.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        cmd.Text("@action", action, 16); cmd.Text("@actor", user.Username); cmd.Money("@balance", balance);
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }

    public async Task<PersonnelHistory> HistoryAsync(PersonnelGroup group, Guid personId, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString))
        {
            var h = await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.History>($"personnel/{group}/{personId:D}/history", null, token).ConfigureAwait(false);
            return new(RemoteApi.Decode(h.Payments), RemoteApi.Decode(h.Leaves), RemoteApi.Decode(h.Events)) { LeaveChanges = RemoteApi.Decode(h.LeaveChanges) };
        }
        if (personId == Guid.Empty) throw new ArgumentException("Personel kimliği eksik.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.GlobalRead, token).ConfigureAwait(false);
        using (var verify = work.Command($"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)} WHERE PersonId=@person"))
        {
            verify.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId;
            if (Convert.ToInt32(await verify.ExecuteScalarAsync(token).ConfigureAwait(false)) != 1) throw new InvalidOperationException("Personel kartı bulunamadı.");
        }
        async Task<DataTable> Read(string sql)
        {
            using var cmd = work.Command(sql); cmd.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId;
            return await cmd.TableAsync(token).ConfigureAwait(false);
        }
        var payments = await Read(@"WITH PeriodLedger AS(
            SELECT P.* ,L.EmploymentId FROM dbo.PaymentLedger P LEFT JOIN dbo.EmploymentRecordLinks L
                ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
            WHERE P.PersonId=@person)
            SELECT LedgerId,EmploymentId,CreatedUtc,Kind,Delta,PaymentPurpose,ProjectId,FinanceEntryId,
            SUM(Delta) OVER(PARTITION BY EmploymentId ORDER BY LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,Actor,RequestId,ReversedUtc
            FROM PeriodLedger ORDER BY LedgerId").ConfigureAwait(false);
        var leaves = group == PersonnelGroup.General
            ? await Read("SELECT IzinId,EmploymentId,IZINBASLANGIC,IZINBITIS,IzinSuresi FROM dbo.izinler WHERE PersonId=@person ORDER BY IZINBASLANGIC,IzinId").ConfigureAwait(false)
            : new DataTable();
        var events = await Read("SELECT CreatedUtc,Action,Actor,Balance FROM dbo.PersonnelArchiveEvents WHERE PersonId=@person ORDER BY EventId").ConfigureAwait(false);
        var changes = group == PersonnelGroup.General
            ? await Read(@"SELECT HistoryId,IzinId,
                (SELECT EmploymentId FROM dbo.EmploymentRecordLinks WHERE RecordKind=N'LeaveHistory' AND RecordKey=CONVERT(nvarchar(64),LeaveHistory.HistoryId)) EmploymentId,
                CASE Operation WHEN N'Baseline' THEN N'Aktarılan mevcut kayıt' WHEN N'Create' THEN N'Yeni izin'
                    WHEN N'Change' THEN N'Tarih değişikliği' ELSE N'Silinen izin' END Islem,
                OldStart OncekiBaslangic,OldEnd OncekiBitis,DATEDIFF(day,OldStart,OldEnd)+1 OncekiGun,
                NewStart YeniBaslangic,NewEnd YeniBitis,DATEDIFF(day,NewStart,NewEnd)+1 YeniGun,
                ChangedUtc KayitZamaniUtc,COALESCE(Actor,N'Bilinmiyor') Kaydeden,SqlPrincipal SqlHesabi
                FROM dbo.LeaveHistory WHERE PersonId=@person ORDER BY HistoryId DESC").ConfigureAwait(false)
            : new DataTable();
        await work.CommitAsync(token).ConfigureAwait(false); return new(SensitiveData.Mask(payments, user), leaves, SensitiveData.Mask(events, user)) { LeaveChanges = changes };
    }
}
