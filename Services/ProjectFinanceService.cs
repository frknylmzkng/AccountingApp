using System.Data;
using Microsoft.Data.SqlClient;
using System.Text.RegularExpressions;

namespace WinFormsApp2;

internal sealed record FinanceDocument(Guid ProjectId, string Kind, string Reference, Guid? PersonId,
    PersonnelGroup? Group, long? Tc, byte[]? PersonVersion, DateTime Start, DateTime End, decimal Amount, string Description);

internal sealed class ProjectFinanceService(string connectionString, UserSession user)
{
    internal async Task<Guid> CreateProjectAsync(string code, string name, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).IdAsync("projects", new AccountingApp.Contracts.ProjectCreate(code, name, RemoteApi.Reason), "projectId", token).ConfigureAwait(false);
        user.DemandAdmin(); code = Reference(code, 30); name = name.Trim();
        if (name.Length is < 1 or > 100 || name.Any(char.IsControl)) throw new ArgumentException("Proje adı 1–100 karakter olmalıdır.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        using var find = work.Command("SELECT ProjectId,Name FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE Code=@code");
        find.Text("@code", code, 30);
        using (var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false))
            if (await r.ReadAsync(token).ConfigureAwait(false))
            {
                if (r.GetString(1) != name) throw new InvalidOperationException("Proje kodu başka adla kayıtlı.");
                return r.GetGuid(0);
            }
        using var cmd = work.Command("INSERT dbo.FinanceProjects(Code,Name,CreatedBy) OUTPUT inserted.ProjectId VALUES(@code,@name,@actor)");
        cmd.Text("@code", code, 30); cmd.Text("@name", name, 100); cmd.Text("@actor", user.Username);
        Guid id = (Guid)(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))!;
        await work.CommitAsync(token).ConfigureAwait(false); return id;
    }
    internal static string Reference(string value, int length = 50)
    {
        value = value.Trim().ToUpperInvariant();
        if (value.Length < 1 || value.Length > length || !Regex.IsMatch(value, @"^[A-Z0-9][A-Z0-9._/-]*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            throw new ArgumentException("Kod/belge no yalnızca Latin harf, rakam ve . _ / - içermelidir.");
        return value;
    }
    internal static FinanceDocument Normalize(FinanceDocument document)
    {
        string description = document.Description.Trim();
        if (document.ProjectId == Guid.Empty || document.Kind is not ("Accrual" or "Expense") ||
            document.Amount <= 0 || document.Amount > 9999999999999999.99m || decimal.Round(document.Amount, 2) != document.Amount ||
            document.End.Date < document.Start.Date || description.Length is < 1 or > 200 || description.Any(char.IsControl))
            throw new ArgumentException("Proje, tür, tarih aralığı, pozitif iki ondalıklı tutar ve 1–200 karakter açıklama gerekiyor.");
        if (document.Kind == "Accrual")
        {
            if (document.PersonId is null || document.PersonId == Guid.Empty || document.Group == null || !Enum.IsDefined(document.Group.Value) || document.Tc == null)
                throw new ArgumentException("Hakediş için güncel personel seçin.");
            _ = PersonnelConcurrency.RequireVersion(document.PersonVersion);
        }
        else if (document.PersonId != null || document.Group != null || document.Tc != null || document.PersonVersion != null)
            throw new ArgumentException("Diğer proje gideri personel hakedişine bağlanamaz.");
        return document with { Reference = Reference(document.Reference), Description = description, Start = document.Start.Date, End = document.End.Date,
            PersonVersion = document.PersonVersion?.ToArray() };
    }
    internal static async Task DemandProjectAsync(IUnitOfWork work, Guid projectId, CancellationToken token)
    {
        using var cmd = work.Command("SELECT COUNT(*) FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND IsActive=1");
        cmd.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = projectId;
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false)) != 1) throw new InvalidOperationException("Proje bulunamadı veya pasif. Listeyi yenileyin.");
    }
    internal async Task<Guid> PostAsync(FinanceDocument input, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).IdAsync("finance/post", new AccountingApp.Contracts.FinancePost(input.ProjectId, input.Kind, input.Reference, input.PersonId, input.Group.HasValue ? (AccountingApp.Contracts.PersonnelGroup)input.Group.Value : null, input.Tc, input.PersonVersion, DateTime.SpecifyKind(input.Start.Date, DateTimeKind.Unspecified), DateTime.SpecifyKind(input.End.Date, DateTimeKind.Unspecified), input.Amount, input.Description, RemoteApi.Reason), "entryId", token).ConfigureAwait(false);
        user.DemandAdmin(); var d = Normalize(input);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        // The document key survives a restart/lost response. A reused reference
        // must match the entire immutable payload before returning the old result.
        using (var find = work.Command(@"SELECT EntryId,PersonId,PersonnelGroup,TC,PeriodStart,PeriodEnd,Amount,Description,ReversedUtc
            FROM dbo.FinanceEntries WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND Kind=@kind AND Reference=@reference"))
        {
            Key(find, d);
            using var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (await r.ReadAsync(token).ConfigureAwait(false))
            {
                if ((r.IsDBNull(1) ? null : (Guid?)r.GetGuid(1)) != d.PersonId ||
                    (r.IsDBNull(2) ? null : (PersonnelGroup?)r.GetInt32(2)) != d.Group ||
                    (r.IsDBNull(3) ? null : (long?)r.GetInt64(3)) != d.Tc ||
                    r.GetDateTime(4) != d.Start || r.GetDateTime(5) != d.End || r.GetDecimal(6) != d.Amount || r.GetString(7) != d.Description)
                    throw new InvalidOperationException("Bu belge numarası farklı bilgilerle kullanılmış. Sonucu kontrol edin; yeni kayıt oluşturulmadı.");
                if (!r.IsDBNull(8)) throw new InvalidOperationException("Bu belge iptal edilmiş; aynı numarayla yeniden kaydedilemez.");
                return r.GetGuid(0);
            }
        }
        if (d.Kind == "Accrual")
        {
            var repo = new PersonnelRepository(work);
            await repo.LockPersonAsync(d.Group!.Value, d.Tc!.Value, token, d.PersonVersion).ConfigureAwait(false);
            if (await repo.IdentityAsync(d.Group.Value, d.Tc.Value, token).ConfigureAwait(false) != d.PersonId) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            await EmploymentService.ValidateRangeAsync(work, d.Group.Value, d.PersonId!.Value, d.Start, d.End, token).ConfigureAwait(false);
        }
        await DemandProjectAsync(work, d.ProjectId, token).ConfigureAwait(false);
        using var cmd = work.Command(@"INSERT dbo.FinanceEntries(ProjectId,Kind,Reference,PersonId,TC,PersonnelGroup,PeriodStart,PeriodEnd,Amount,Description,CreatedBy)
            OUTPUT inserted.EntryId VALUES(@project,@kind,@reference,@person,@tc,@group,@start,@end,@amount,@description,@actor)");
        Key(cmd, d); cmd.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = (object?)d.PersonId ?? DBNull.Value;
        cmd.Parameters.Add("@tc", SqlDbType.BigInt).Value = (object?)d.Tc ?? DBNull.Value;
        cmd.Parameters.Add("@group", SqlDbType.Int).Value = d.Group.HasValue ? (int)d.Group.Value : DBNull.Value;
        cmd.Parameters.Add("@start", SqlDbType.Date).Value = d.Start; cmd.Parameters.Add("@end", SqlDbType.Date).Value = d.End;
        cmd.Money("@amount", d.Amount); cmd.Text("@description", d.Description, 200); cmd.Text("@actor", user.Username);
        Guid id = (Guid)(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false))!;
        if (d.Kind == "Accrual") await AdjustBalanceAsync(work, d.Group!.Value, d.PersonId!.Value, d.Amount, id, d.ProjectId, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); return id;
    }
    private static void Key(SqlCommand cmd, FinanceDocument d)
    { cmd.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = d.ProjectId; cmd.Text("@kind", d.Kind, 12); cmd.Text("@reference", d.Reference, 50); }
    private async Task AdjustBalanceAsync(IUnitOfWork work, PersonnelGroup group, Guid personId, decimal delta, Guid entryId, Guid projectId, CancellationToken token)
    {
        using var change = work.Command($@"UPDATE {PersonnelRepository.Table(group)} SET PARA=PARA+@delta
            WHERE PersonId=@person AND ArchivedUtc IS NULL AND PARA+@delta BETWEEN -9999999999999999.99 AND 9999999999999999.99");
        change.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId; change.Money("@delta", delta);
        if (await change.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Bakiye sınırı aşıldı veya personel arşivlenmiş.");
        using var ledger = work.Command($@"INSERT dbo.PaymentLedger(PersonId,TC,PersonnelGroup,Delta,Kind,Actor,ProjectId,FinanceEntryId)
            SELECT PersonId,TC,@group,@delta,N'Allocation',@actor,@project,@entry FROM {PersonnelRepository.Table(group)} WHERE PersonId=@person");
        ledger.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = personId; ledger.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        ledger.Money("@delta", delta); ledger.Text("@actor", user.Username);
        ledger.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = projectId; ledger.Parameters.Add("@entry", SqlDbType.UniqueIdentifier).Value = entryId;
        if (await ledger.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Hakediş deftere bağlanamadı.");
    }
    internal async Task ReverseAsync(Guid entryId, byte[] version, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("finance/reverse", new AccountingApp.Contracts.FinanceReverse(entryId, version, RemoteApi.Reason), token).ConfigureAwait(false); return; }
        user.DemandAdmin(); version = PersonnelConcurrency.RequireVersion(version);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        // Read immutable identity first; acquire the person lock before the entry update.
        using var find = work.Command("SELECT Kind,PersonId,PersonnelGroup,TC,Amount,ProjectId,AccrualCalculationId,WageClaimId FROM dbo.FinanceEntries WHERE EntryId=@id");
        find.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = entryId;
        string kind; Guid? person; PersonnelGroup? group; long? tc; decimal amount; Guid project;
        using (var r = await find.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Belge bulunamadı.");
            if(!r.IsDBNull(7))throw new InvalidOperationException("Bu belge ücret/gider kuralı aktarımına bağlı; Ek ücret/gider ekranındaki bağımsız onaylı tam ters aktarımı kullanın.");
            if(!r.IsDBNull(6))throw new InvalidOperationException("Bu belge onaylı dönem aktarımına bağlı. Tek belge iptali yapılamaz; bağlantılı mali düzeltme gerekir.");
            kind = r.GetString(0); person = r.IsDBNull(1) ? null : r.GetGuid(1); group = r.IsDBNull(2) ? null : (PersonnelGroup)r.GetInt32(2);
            tc = r.IsDBNull(3) ? null : r.GetInt64(3); amount = r.GetDecimal(4); project = r.GetGuid(5);
        }
        if (kind == "Accrual")
        {
            var repo = new PersonnelRepository(work); await repo.LockPersonAsync(group!.Value, tc!.Value, token).ConfigureAwait(false);
            if (await repo.IdentityAsync(group.Value, tc.Value, token).ConfigureAwait(false) != person) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
            await EmploymentService.EnsureCurrentRecordAsync(work, group.Value, person!.Value, "Finance", entryId.ToString("D"), token).ConfigureAwait(false);
        }
        await PaymentAllocationService.DemandFinanceFreeAsync(work,entryId,token).ConfigureAwait(false);
        using var update = work.Command("UPDATE dbo.FinanceEntries SET ReversedUtc=SYSUTCDATETIME(),ReversedBy=@actor WHERE EntryId=@id AND RecordVersion=@version AND ReversedUtc IS NULL");
        update.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = entryId; update.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version; update.Text("@actor", user.Username);
        if (await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Belge değişmiş veya daha önce iptal edilmiş. Yenileyin.");
        if (kind == "Accrual") await AdjustBalanceAsync(work, group!.Value, person!.Value, -amount, entryId, project, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }
    internal async Task<DataTable> ProjectsAsync(CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync("projects", token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        using var cmd = work.Command(@"SELECT P.ProjectId,P.Code,P.Name,P.IsActive,
            COALESCE(F.LaborCost,0) LaborCost,COALESCE(F.OtherCost,0) OtherCost,
            COALESCE(F.LaborCost,0)+COALESCE(F.OtherCost,0) TotalCost,COALESCE(C.AdvanceCash,0) AdvanceCash,
            COALESCE(C.SettlementCash,0) SettlementCash FROM dbo.FinanceProjects P
            LEFT JOIN (SELECT ProjectId,SUM(CASE WHEN Kind=N'Accrual' THEN EffectiveAmount ELSE 0 END) LaborCost,
                SUM(CASE WHEN Kind=N'Expense' THEN EffectiveAmount ELSE 0 END) OtherCost FROM dbo.FinanceEffectiveEntries WHERE ReversedUtc IS NULL GROUP BY ProjectId) F ON P.ProjectId=F.ProjectId
            LEFT JOIN (SELECT ProjectId,SUM(CASE WHEN PaymentPurpose=N'Advance' THEN -Delta ELSE 0 END) AdvanceCash,
                SUM(CASE WHEN PaymentPurpose=N'Settlement' THEN -Delta ELSE 0 END) SettlementCash
                FROM dbo.PaymentLedger WHERE Kind=N'Payment' AND ReversedUtc IS NULL GROUP BY ProjectId) C ON P.ProjectId=C.ProjectId WHERE " + ProjectAuthorization.Visible + " AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.ProjectAccountAccess G WHERE G.ProjectId=P.ProjectId AND G.AccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND G.IsActive=1 AND G.CanViewCosts=1)) ORDER BY P.Code");
        ProjectAuthorization.Bind(cmd,user);
        var rows = await cmd.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return SensitiveData.Mask(rows, user);
    }
    internal async Task<DataTable> EntriesAsync(Guid projectId, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"projects/{projectId:D}/entries", token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, token).ConfigureAwait(false);
        await ProjectAuthorization.DemandAsync(work,user,projectId,ProjectAccess.Costs,token);
        using var cmd = work.Command(@"SELECT TOP(500) EntryId,Kind,Reference,PersonId,CASE WHEN @scoped=1 THEN CAST(NULL AS bigint) ELSE TC END TC,
            (SELECT EmploymentId FROM dbo.EmploymentRecordLinks WHERE RecordKind=N'Finance' AND RecordKey=CONVERT(nvarchar(64),FinanceEntries.EntryId)) EmploymentId,PeriodStart,PeriodEnd,Amount,Description,
            CreatedUtc,CreatedBy,ReversedUtc,ReversedBy,RecordVersion,AccrualCalculationId,AdjustmentAmount,EffectiveAmount FROM dbo.FinanceEffectiveEntries FinanceEntries WHERE ProjectId=@project ORDER BY CreatedUtc DESC,EntryId DESC");
        ProjectAuthorization.Bind(cmd,user);cmd.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = projectId;
        var rows = await cmd.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return SensitiveData.Mask(rows, user);
    }
}
