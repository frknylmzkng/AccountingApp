using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal enum ProjectAccess { Read, ManageWorkforce, Costs, Export, PreparePayment, ApprovePayment, Payments, PlanShifts, ApproveLeaves, EnterTimesheets, ApproveTimesheets }
internal static class ProjectAuthorization
{
    internal const string Visible = "(@scoped=0 OR EXISTS(SELECT 1 FROM dbo.ProjectAccountAccess G WITH(HOLDLOCK) WHERE G.ProjectId=P.ProjectId AND G.AccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND G.IsActive=1))";
    internal static void Bind(SqlCommand command, UserSession user) => command.Parameters.Add("@scoped", SqlDbType.Bit).Value = user.Role == UserRole.ProjectUser;
    internal static async Task DemandAsync(IUnitOfWork work, UserSession user, Guid project, ProjectAccess access, CancellationToken ct)
    {
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Read, ct).ConfigureAwait(false);
        if (access is ProjectAccess.Costs or ProjectAccess.PreparePayment or ProjectAccess.ApprovePayment or ProjectAccess.Payments) user.DemandViewSalary();
        using var c = work.Command(@"SELECT G.CanManageWorkforce,G.CanViewCosts,G.CanExport,G.CanPreparePayments,G.CanApprovePayments,G.CanPlanShifts,G.CanApproveLeaves,G.CanEnterTimesheets,G.CanApproveTimesheets FROM dbo.FinanceProjects P WITH(UPDLOCK,HOLDLOCK)
            LEFT JOIN dbo.ProjectAccountAccess G WITH(HOLDLOCK) ON G.ProjectId=P.ProjectId AND G.AccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND G.IsActive=1 WHERE P.ProjectId=@id");
        c.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = project;
        using var r = await c.ExecuteReaderAsync(ct).ConfigureAwait(false);
        bool found = await r.ReadAsync(ct).ConfigureAwait(false);
        bool scoped = user.Role == UserRole.ProjectUser;
        if (!found || (scoped && r.IsDBNull(0))) throw new UnauthorizedAccessException("Bu proje için erişim yetkiniz yok.");
        bool allowed = access switch
        {
            ProjectAccess.Read => true,
            ProjectAccess.ManageWorkforce => scoped ? r.GetBoolean(0) : user.Role == UserRole.Admin,
            ProjectAccess.Costs => !scoped || r.GetBoolean(1),
            ProjectAccess.Export => scoped ? r.GetBoolean(2) : user.CanExportPersonalData,
            ProjectAccess.PreparePayment => user.Role == UserRole.Admin || (scoped && r.GetBoolean(1) && r.GetBoolean(3)),
            ProjectAccess.ApprovePayment => user.Role == UserRole.Admin || (scoped && r.GetBoolean(1) && r.GetBoolean(4)),
            ProjectAccess.Payments => user.Role == UserRole.Admin || (scoped && r.GetBoolean(1) && (r.GetBoolean(3) || r.GetBoolean(4))),
            ProjectAccess.PlanShifts => user.Role == UserRole.Admin || (scoped && r.GetBoolean(5)),
            ProjectAccess.ApproveLeaves => user.Role == UserRole.Admin || (scoped && r.GetBoolean(6)),
            ProjectAccess.EnterTimesheets => user.Role == UserRole.Admin || (scoped && r.GetBoolean(7)),
            ProjectAccess.ApproveTimesheets => user.Role == UserRole.Admin || (scoped && r.GetBoolean(8)),
            _ => false
        };
        if (!allowed) throw new UnauthorizedAccessException("Bu projede istenen işlem için yetkiniz yok.");
    }
    // Project users may work only with the current employment of people already
    // introduced to this project by a central operator. No global directory/TC.
    internal static async Task<long> PersonTcAsync(IUnitOfWork w, UserSession user, Guid project, Guid person, PersonnelGroup group, CancellationToken ct)
    {
        using var c = w.Command($@"SELECT C.TC FROM {PersonnelRepository.Table(group)} C WITH(UPDLOCK,HOLDLOCK) WHERE C.PersonId=@person AND C.ArchivedUtc IS NULL
 AND (@scoped=0 OR EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 WHERE A.ProjectId=@project AND A.PersonId=C.PersonId AND L.EmploymentId=C.CurrentEmploymentId))");
        Bind(c, user); c.Parameters.Add("@person", SqlDbType.UniqueIdentifier).Value = person; c.Parameters.Add("@project", SqlDbType.UniqueIdentifier).Value = project;
        return await c.ExecuteScalarAsync(ct).ConfigureAwait(false) is long tc ? tc : throw new UnauthorizedAccessException("Personel bu projenin güncel çalışma dönemi havuzunda değil.");
    }
}
