using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class ProjectAccessService(string connectionString, UserSession user)
{
    private static void Id(SqlCommand c, string name, Guid value) => c.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value;
    internal async Task<DataTable> AccountsAsync(CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync("project-access/accounts", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        using var c = w.Command("SELECT TOP(500) AccountId,username,SessionVersion,SessionEpoch FROM dbo.admin WHERE RoleName=N'ProjectUser' AND IsActive=1 ORDER BY username");
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return data;
    }
    internal async Task<DataTable> GrantsAsync(Guid project, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"project-access/{project:D}/grants", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        using var c = w.Command(@"SELECT G.*,COALESCE(A.username,N'[silinmiş hesap]') username,A.IsActive AccountActive,A.SessionVersion AccountVersion,A.SessionEpoch AccountEpoch
 FROM dbo.ProjectAccountAccess G LEFT JOIN dbo.admin A ON A.AccountId=G.AccountId WHERE G.ProjectId=@id ORDER BY G.IsActive DESC,G.CreatedUtc DESC,G.GrantId"); Id(c, "@id", project);
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return data;
    }
    private static async Task LockProjectAsync(IUnitOfWork w, Guid id, byte[] version, CancellationToken ct)
    {
        using var c = w.Command("SELECT COUNT(*) FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@id AND RecordVersion=@version"); Id(c, "@id", id); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = PersonnelConcurrency.RequireVersion(version);
        if (Convert.ToInt32(await c.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Proje değişmiş; proje listesini yenileyip açın.");
    }
    internal async Task SaveAsync(Wire.ProjectGrantWrite input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("project-access/save", input, ct); return; }
        user.DemandAdmin(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? ""));
        if ((input.CanPreparePayments || input.CanApprovePayments) && !input.CanViewCosts) throw new ArgumentException("Ödeme hazırlama/onay için proje maliyet okuması da gerekir.");
        var accountVersion = PersonnelConcurrency.RequireVersion(input.AccountVersion);
        byte[]? grantVersion = input.GrantId.HasValue ? PersonnelConcurrency.RequireVersion(input.GrantVersion) : null;
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.LockAccountChangesAsync(w, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        using (var account = w.Command("SELECT COUNT(*) FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE AccountId=@id AND SessionVersion=@version AND SessionEpoch=@epoch AND RoleName=N'ProjectUser' AND IsActive=1"))
        {
            Id(account, "@id", input.AccountId); Id(account, "@epoch", input.AccountEpoch); account.Parameters.Add("@version", SqlDbType.Binary, 8).Value = accountVersion;
            if (Convert.ToInt32(await account.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Seçilen proje hesabı değişmiş, silinmiş veya pasif. Hesap listesini yenileyin.");
        }
        await LockProjectAsync(w, input.ProjectId, input.ProjectVersion, ct);
        using (var duplicate = w.Command("SELECT COUNT(*) FROM dbo.ProjectAccountAccess WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND IsActive=1 AND (@id IS NULL OR GrantId<>@id) AND (AccountId=@account OR (@responsible=1 AND IsResponsible=1))"))
        {
            Id(duplicate, "@project", input.ProjectId); Id(duplicate, "@account", input.AccountId); duplicate.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = (object?)input.GrantId ?? DBNull.Value; duplicate.Parameters.Add("@responsible", SqlDbType.Bit).Value = input.IsResponsible;
            if (Convert.ToInt32(await duplicate.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Hesabın etkin yetkisini düzenleyin. Projede başka sorumlu varsa önce onun sorumluluğunu kaldırın veya yetkisini geri alın.");
        }
        using var c = w.Command(input.GrantId.HasValue
            ? "UPDATE dbo.ProjectAccountAccess SET IsResponsible=@responsible,CanManageWorkforce=@manage,CanViewCosts=@costs,CanExport=@export,CanPreparePayments=@prepare,CanApprovePayments=@approve,CanPlanShifts=@plan,CanApproveLeaves=@leave,CanEnterTimesheets=@time,CanApproveTimesheets=@timeapprove WHERE GrantId=@id AND ProjectId=@project AND AccountId=@account AND RecordVersion=@version AND IsActive=1"
            : "INSERT dbo.ProjectAccountAccess(ProjectId,AccountId,IsResponsible,CanManageWorkforce,CanViewCosts,CanExport,CanPreparePayments,CanApprovePayments,CanPlanShifts,CanApproveLeaves,CanEnterTimesheets,CanApproveTimesheets,CreatedBy) VALUES(@project,@account,@responsible,@manage,@costs,@export,@prepare,@approve,@plan,@leave,@time,@timeapprove,@actor)");
        Id(c, "@project", input.ProjectId); Id(c, "@account", input.AccountId); c.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = (object?)input.GrantId ?? DBNull.Value; c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = (object?)grantVersion ?? DBNull.Value;
        c.Parameters.Add("@responsible", SqlDbType.Bit).Value = input.IsResponsible; c.Parameters.Add("@manage", SqlDbType.Bit).Value = input.CanManageWorkforce; c.Parameters.Add("@costs", SqlDbType.Bit).Value = input.CanViewCosts; c.Parameters.Add("@export", SqlDbType.Bit).Value = input.CanExport; c.Text("@actor", user.Username);
        c.Parameters.Add("@time",SqlDbType.Bit).Value=input.CanEnterTimesheets; c.Parameters.Add("@timeapprove",SqlDbType.Bit).Value=input.CanApproveTimesheets;
        c.Parameters.Add("@plan",SqlDbType.Bit).Value=input.CanPlanShifts; c.Parameters.Add("@leave",SqlDbType.Bit).Value=input.CanApproveLeaves;
        c.Parameters.Add("@prepare",SqlDbType.Bit).Value=input.CanPreparePayments; c.Parameters.Add("@approve",SqlDbType.Bit).Value=input.CanApprovePayments;
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Yetki kaydı değişmiş; listeyi yenileyin.");
        await w.CommitAsync(ct);
    }
    internal async Task RevokeAsync(Wire.ProjectGrantRevoke input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("project-access/revoke", input, ct); return; }
        user.DemandAdmin(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? ""));
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await AccountAuthorization.LockAccountChangesAsync(w, ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct); await LockProjectAsync(w, input.ProjectId, input.ProjectVersion, ct);
        using var c = w.Command("UPDATE dbo.ProjectAccountAccess SET IsActive=0,IsResponsible=0 WHERE GrantId=@id AND ProjectId=@project AND RecordVersion=@version AND IsActive=1"); Id(c, "@id", input.GrantId); Id(c, "@project", input.ProjectId); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = PersonnelConcurrency.RequireVersion(input.GrantVersion);
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Yetki değişmiş veya daha önce kaldırılmış; yenileyin."); await w.CommitAsync(ct);
    }
    internal async Task<PersonnelPage> PeopleAsync(Guid project, long index, int size, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"project-access/{project:D}/people?index={index}&size={size}", ct);
        if (index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz sayfa.");
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Read, ct);
        const string source = @"WITH Pool AS(SELECT C.PersonId,C.AD,C.SOYAD,C.RecordVersion,0 PersonnelGroup FROM dbo.Calisan C WHERE C.ArchivedUtc IS NULL AND EXISTS(
 SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE A.ProjectId=@project AND A.PersonId=C.PersonId AND L.EmploymentId=C.CurrentEmploymentId)
 UNION ALL SELECT C.PersonId,C.AD,C.SOYAD,C.RecordVersion,1 PersonnelGroup FROM dbo.Joker C WHERE C.ArchivedUtc IS NULL AND EXISTS(
 SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE A.ProjectId=@project AND A.PersonId=C.PersonId AND L.EmploymentId=C.CurrentEmploymentId))";
        using var count = w.Command(source + " SELECT COUNT_BIG(*) FROM Pool"); Id(count, "@project", project); long total = Convert.ToInt64(await count.ExecuteScalarAsync(ct)); index = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var c = w.Command(source + " SELECT * FROM Pool ORDER BY AD,SOYAD,PersonId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY"); Id(c, "@project", project); c.Parameters.Add("@skip", SqlDbType.BigInt).Value = index * size; c.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return new(data, total, index, size);
    }
}
