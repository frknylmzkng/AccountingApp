using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal enum PersonnelCardSection { Assignments, Leaves, LeaveChanges, Ledger, Finance, ArchiveEvents, Audit, GroupTransitions }
internal sealed record PersonnelCardData(DataTable Header, DataTable Periods, DataTable Totals, DateTime ReadUtc, bool CanViewAudit);
internal sealed class PersonnelCardService(string connectionString, UserSession user)
{
    internal async Task SaveDetailsAsync(Wire.PersonnelCardDetails input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("personnel-card/details", input, ct); return; }
        user.DemandEdit(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? ""));
        if (!Enum.IsDefined(input.Group) || input.PersonId == Guid.Empty) throw new ArgumentException("Personel kimliği/grubu geçersiz.");
        var group = (PersonnelGroup)input.Group; var version = PersonnelConcurrency.RequireVersion(input.Version);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Edit, ct);
        using (var find = w.Command($"SELECT 1 FROM {PersonnelRepository.Table(group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND RecordVersion=@version AND ArchivedUtc IS NULL"))
        {
            Bind(find, input.PersonId, group, null); find.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
            if (await find.ExecuteScalarAsync(ct) is null) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        }
        string first = input.FirstName.Trim(), last = input.LastName.Trim();
        if (first.Length is < 1 or > 50 || last.Length is < 1 or > 50 || first.Any(char.IsControl) || last.Any(char.IsControl)) throw new ArgumentException("Ad ve soyad 1–50 karakter olmalı.");
        string? iban = null;
        if (input.Iban != null)
        {
            user.DemandViewIban();
            if (!IbanInput.TryNormalize(input.Iban, out var normalized, out string error)) throw new ArgumentException(error);
            iban = normalized;
        }
        // Omitted IBAN is preserved at SQL level; no hidden value round trip.
        using var c = w.Command($"UPDATE {PersonnelRepository.Table(group)} SET AD=@first,SOYAD=@last" + (iban != null ? ",IBAN=@iban" : "") + " WHERE PersonId=@person AND RecordVersion=@version AND ArchivedUtc IS NULL"); Bind(c, input.PersonId, group, null);
        c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version; c.Text("@first", first); c.Text("@last", last); if (iban != null) c.Text("@iban", iban, 34);
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        await new PersonnelService(connectionString, user).AuditAsync(w, "Person.UpdateDetails", ct); await w.CommitAsync(ct);
    }
    private static void Bind(SqlCommand c, Guid person, PersonnelGroup group, Guid? employment)
    {
        c.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = person;
        c.Parameters.Add("@group", SqlDbType.Int).Value = (int)group;
        c.Parameters.Add("@employment", SqlDbType.UniqueIdentifier).Value = (object?)employment ?? DBNull.Value;
    }
    private static async Task VerifyAsync(IUnitOfWork w, PersonnelGroup group, Guid person, Guid? employment, CancellationToken ct)
    {
        if (person == Guid.Empty || employment == Guid.Empty) throw new ArgumentException("Personel/dönem kimliği geçersiz.");
        using var c = w.Command($@"SELECT COUNT(*) FROM {PersonnelRepository.Table(group)} C WHERE C.PersonId=@person AND
 (@employment IS NULL OR EXISTS(SELECT 1 FROM dbo.EmploymentPeriods E WHERE E.PersonId=C.PersonId AND E.EmploymentId=@employment))"); Bind(c, person, group, employment);
        if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Personel veya seçilen çalışma dönemi bulunamadı.");
    }
    internal async Task<PersonnelCardData> ReadAsync(PersonnelGroup group, Guid person, Guid? employment, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString))
        {
            var r = await RemoteApi.For(connectionString, user).SendAsync<Wire.PersonnelCard>($"personnel-card/{group}/{person:D}" + (employment.HasValue ? "?employment=" + employment.Value.ToString("D") : ""), null, ct);
            return new(RemoteApi.Decode(r.Header), RemoteApi.Decode(r.Periods), RemoteApi.Decode(r.Totals), r.ReadUtc, r.CanViewAudit);
        }
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.GlobalRead, ct);
        await VerifyAsync(w, group, person, employment, ct);
        string leave = group == PersonnelGroup.General ? @"(SELECT COUNT(*) FROM dbo.izinler I WHERE I.PersonId=C.PersonId AND I.EmploymentId=C.CurrentEmploymentId) CurrentLeaveCount,
 (SELECT TOP(1) LeaveVersion FROM dbo.izinler I WHERE I.PersonId=C.PersonId AND I.EmploymentId=C.CurrentEmploymentId ORDER BY IzinId) CurrentLeaveVersion,
 (SELECT TOP(1) IZINBASLANGIC FROM dbo.izinler I WHERE I.PersonId=C.PersonId AND I.EmploymentId=C.CurrentEmploymentId ORDER BY IzinId) CurrentLeaveStart,
 (SELECT TOP(1) IZINBITIS FROM dbo.izinler I WHERE I.PersonId=C.PersonId AND I.EmploymentId=C.CurrentEmploymentId ORDER BY IzinId) CurrentLeaveEnd"
            : "CAST(0 AS int) CurrentLeaveCount,CAST(NULL AS binary(8)) CurrentLeaveVersion,CAST(NULL AS date) CurrentLeaveStart,CAST(NULL AS date) CurrentLeaveEnd";
        using var header = w.Command($@"SELECT C.PersonId,C.TC,C.AD,C.SOYAD,C.IBAN,C.PARA,C.RecordVersion,C.CurrentEmploymentId,C.ArchivedUtc,C.ArchivedBy,
 E.RecordVersion CurrentEmploymentVersion,E.StartDate CurrentStart,E.EndDate CurrentEnd,E.IsLegacy,E.IsClosed,{leave} FROM {PersonnelRepository.Table(group)} C JOIN dbo.EmploymentPeriods E ON E.EmploymentId=C.CurrentEmploymentId WHERE C.PersonId=@person"); Bind(header, person, group, employment);
        var head = await header.TableAsync(ct);
        using var periods = w.Command("SELECT EmploymentId,PersonnelGroup,StartDate,EndDate,IsLegacy,IsClosed,CreatedUtc,CreatedBy,RecordVersion FROM dbo.EmploymentPeriods WHERE PersonId=@person ORDER BY CASE WHEN EmploymentId=(SELECT CurrentEmploymentId FROM " + PersonnelRepository.Table(group) + " WHERE PersonId=@person) THEN 0 ELSE 1 END,CreatedUtc DESC,EmploymentId"); Bind(periods, person, group, employment);
        var jobs = await periods.TableAsync(ct);
        using var totals = w.Command(@"SELECT
 (SELECT COALESCE(SUM(F.EffectiveAmount),0) FROM dbo.FinanceEffectiveEntries F JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId)
 WHERE F.PersonId=@person AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL AND (@employment IS NULL OR L.EmploymentId=@employment)) Accrual,
 COALESCE(SUM(CASE WHEN P.PaymentPurpose=N'Advance' THEN -P.Delta ELSE 0 END),0) Advance,
 COALESCE(SUM(CASE WHEN P.PaymentPurpose=N'Settlement' THEN -P.Delta ELSE 0 END),0) Settlement,
 COALESCE(SUM(CASE WHEN P.PaymentPurpose=N'Unclassified' THEN -P.Delta ELSE 0 END),0) Unclassified
 FROM dbo.PaymentLedger P LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 WHERE P.PersonId=@person AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL AND (@employment IS NULL OR L.EmploymentId=@employment)"); Bind(totals, person, group, employment);
        var money = await totals.TableAsync(ct); DateTime read = DateTime.UtcNow; await w.CommitAsync(ct);
        return new(SensitiveData.Mask(head, user), jobs, SensitiveData.Mask(money, user), read, user.Role == UserRole.Admin);
    }
    internal async Task<PersonnelPage> PageAsync(PersonnelGroup group, Guid person, Guid? employment, PersonnelCardSection section, long index, int size, CancellationToken ct)
    {
        if (!Enum.IsDefined(section) || index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz kart bölümü/sayfası.");
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"personnel-card/{group}/{person:D}/{section}?index={index}&size={size}" + (employment.HasValue ? "&employment=" + employment.Value.ToString("D") : ""), ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, section == PersonnelCardSection.Audit ? AccountAccess.Admin : AccountAccess.GlobalRead, ct);
        if (section is PersonnelCardSection.Finance or PersonnelCardSection.Ledger) user.DemandViewSalary();
        await VerifyAsync(w, group, person, employment, ct);
        // Each source has one row per immutable record. Never join leave rows to
        // financial sums; compute ledger running values before page slicing.
        string source = section switch
        {
            PersonnelCardSection.Assignments => @"SELECT A.AssignmentId,A.PersonnelGroup,L.EmploymentId,P.Code ProjectCode,P.Name ProjectName,T.Name TeamName,A.StartDate,A.EndDate,A.CancelledUtc,A.CancelledBy,A.CreatedUtc,A.CreatedBy
 FROM dbo.PersonnelProjectAssignments A JOIN dbo.FinanceProjects P ON P.ProjectId=A.ProjectId LEFT JOIN dbo.ProjectTeams T ON T.TeamId=A.TeamId
 LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 WHERE A.PersonId=@person AND (@employment IS NULL OR L.EmploymentId=@employment)",
            PersonnelCardSection.Leaves => "SELECT IzinId,EmploymentId,IZINBASLANGIC,IZINBITIS,IzinSuresi FROM dbo.izinler WHERE PersonId=@person AND (@employment IS NULL OR EmploymentId=@employment)",
            PersonnelCardSection.LeaveChanges => @"SELECT H.HistoryId,L.EmploymentId,H.IzinId,H.Operation,H.OldStart,H.OldEnd,H.NewStart,H.NewEnd,H.ChangedUtc,H.Actor
 FROM dbo.LeaveHistory H LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'LeaveHistory' AND L.RecordKey=CONVERT(nvarchar(64),H.HistoryId) WHERE H.PersonId=@person AND (@employment IS NULL OR L.EmploymentId=@employment)",
            PersonnelCardSection.Ledger => @"SELECT P.LedgerId,P.PersonnelGroup,L.EmploymentId,P.CreatedUtc,P.Kind,P.PaymentPurpose,P.Delta,
 SUM(P.Delta) OVER(PARTITION BY L.EmploymentId ORDER BY P.LedgerId ROWS UNBOUNDED PRECEDING) RunningBalance,
 F.Code ProjectCode,P.Actor,P.RequestId,P.ReversedUtc FROM dbo.PaymentLedger P LEFT JOIN dbo.FinanceProjects F ON F.ProjectId=P.ProjectId
 LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
 WHERE P.PersonId=@person AND (@employment IS NULL OR L.EmploymentId=@employment)",
            PersonnelCardSection.Finance => @"SELECT F.EntryId,F.PersonnelGroup,L.EmploymentId,P.Code ProjectCode,F.Reference,F.PeriodStart,F.PeriodEnd,F.Amount OriginalAmount,F.AdjustmentAmount,F.EffectiveAmount Amount,F.Description,F.CreatedUtc,F.CreatedBy,F.ReversedUtc,F.ReversedBy
 FROM dbo.FinanceEffectiveEntries F JOIN dbo.FinanceProjects P ON P.ProjectId=F.ProjectId LEFT JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Finance' AND L.RecordKey=CONVERT(nvarchar(64),F.EntryId)
 WHERE F.PersonId=@person AND F.Kind=N'Accrual' AND (@employment IS NULL OR L.EmploymentId=@employment)",
            PersonnelCardSection.ArchiveEvents => "SELECT EventId,PersonnelGroup,CreatedUtc,Action,Actor,Balance FROM dbo.PersonnelArchiveEvents WHERE PersonId=@person",
            PersonnelCardSection.GroupTransitions => "SELECT TransitionId,SourceGroup,TargetGroup,EffectiveDate,SourceEmploymentId,TargetEmploymentId,CreatedUtc,Actor,Reason FROM dbo.PersonnelGroupTransitions WHERE PersonId=@person",
            PersonnelCardSection.Audit => "SELECT AuditId,CreatedUtc,Actor,EntityType,RecordKey,Operation,Reason,OldValues,NewValues,ChangedFields,CorrelationId FROM dbo.AuditLogSafe WHERE PersonId=@person",
            _ => throw new ArgumentException("Kart bölümü geçersiz.")
        };
        string order = section switch { PersonnelCardSection.Assignments => "StartDate DESC,AssignmentId", PersonnelCardSection.Leaves => "IZINBASLANGIC DESC,IzinId", PersonnelCardSection.LeaveChanges => "HistoryId DESC", PersonnelCardSection.Ledger => "LedgerId DESC", PersonnelCardSection.Finance => "CreatedUtc DESC,EntryId", PersonnelCardSection.ArchiveEvents => "EventId DESC", PersonnelCardSection.GroupTransitions => "EffectiveDate DESC,TransitionId", _ => "AuditId DESC" };
        using var count = w.Command("WITH Records AS(" + source + ") SELECT COUNT_BIG(*) FROM Records"); Bind(count, person, group, employment); long total = Convert.ToInt64(await count.ExecuteScalarAsync(ct)); long actual = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var query = w.Command("WITH Records AS(" + source + ") SELECT * FROM Records ORDER BY " + order + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY"); Bind(query, person, group, employment); query.Parameters.Add("@skip", SqlDbType.BigInt).Value = actual * size; query.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var data = await query.TableAsync(ct); await w.CommitAsync(ct); return new(SensitiveData.Mask(data, user), total, actual, size);
    }
}
