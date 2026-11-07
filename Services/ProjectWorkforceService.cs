using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;

namespace WinFormsApp2;

internal sealed class ProjectWorkforceService(string connectionString, UserSession user)
{
    private static string Text(string value, int max, bool required = false)
    { value = value.Trim(); if ((required && value.Length == 0) || value.Length > max || value.Any(char.IsControl)) throw new ArgumentException("Proje/ekip alanlarını kontrol edin."); return value; }
    private static DateTime? Day(DateTime? value)
    { if (value.HasValue && (value.Value.TimeOfDay != TimeSpan.Zero || value.Value.Kind != DateTimeKind.Unspecified)) throw new ArgumentException("Tarih saat ve saat dilimi içermemelidir."); return value; }
    private static void Dates(DateTime? start, DateTime? end) { if (start.HasValue && end < start) throw new ArgumentException("Bitiş başlangıçtan önce olamaz."); }
    private static void DateParam(SqlCommand c, string key, DateTime? value) => c.Parameters.Add(key, SqlDbType.Date).Value = (object?)Day(value) ?? DBNull.Value;
    private static void Id(SqlCommand c, string key, Guid? value) => c.Parameters.Add(key, SqlDbType.UniqueIdentifier).Value = (object?)value ?? DBNull.Value;
    private static async Task<(DateTime? Start, DateTime? End, byte[] Version)> ProjectAsync(IUnitOfWork work, Guid project, bool active, CancellationToken ct)
    {
        using var c = work.Command("SELECT StartDate,EndDate,RecordVersion,IsActive FROM dbo.FinanceProjects WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@id"); Id(c, "@id", project);
        using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || (active && !r.GetBoolean(3))) throw new InvalidOperationException("Proje bulunamadı veya pasif; listeyi yenileyin.");
        return (r.IsDBNull(0) ? null : r.GetDateTime(0), r.IsDBNull(1) ? null : r.GetDateTime(1), r.GetFieldValue<byte[]>(2));
    }
    internal async Task<DataTable> ProjectsAsync(CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync("workforce/projects", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        using var c = w.Command(@"SELECT TOP(500) P.ProjectId,P.Code,P.Name,P.Site,P.CustomerLabel,P.StartDate,P.EndDate,P.IsActive,P.RecordVersion,
 R.username ResponsibleUser,
 CAST(CASE WHEN @admin=1 THEN 1 WHEN @scoped=1 THEN G.CanEnterTimesheets ELSE 0 END AS bit) CanEnterTimesheets,
 CAST(CASE WHEN @admin=1 THEN 1 WHEN @scoped=1 THEN G.CanApproveTimesheets ELSE 0 END AS bit) CanApproveTimesheets,
 CAST(CASE WHEN @admin=1 THEN 1 WHEN @scoped=1 THEN G.CanPlanShifts ELSE 0 END AS bit) CanPlanShifts,
 CAST(CASE WHEN @admin=1 THEN 1 WHEN @scoped=1 THEN G.CanApproveLeaves ELSE 0 END AS bit) CanApproveLeaves,
 CAST(CASE WHEN @salary=0 THEN 0 WHEN @admin=1 THEN 1 WHEN @scoped=1 AND G.CanViewCosts=1 THEN G.CanPreparePayments ELSE 0 END AS bit) CanPreparePayments,
 CAST(CASE WHEN @salary=0 THEN 0 WHEN @admin=1 THEN 1 WHEN @scoped=1 AND G.CanViewCosts=1 THEN G.CanApprovePayments ELSE 0 END AS bit) CanApprovePayments,
 CAST(CASE WHEN @scoped=0 THEN @admin ELSE G.CanManageWorkforce END AS bit) CanManageWorkforce,
 CAST(CASE WHEN @salary=0 THEN 0 WHEN @scoped=0 THEN 1 ELSE G.CanViewCosts END AS bit) CanViewCosts,
 CAST(CASE WHEN @scoped=0 THEN @globalExport ELSE G.CanExport END AS bit) CanExport
 FROM dbo.FinanceProjects P LEFT JOIN dbo.ProjectAccountAccess G WITH(HOLDLOCK) ON G.ProjectId=P.ProjectId AND G.AccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')) AND G.IsActive=1
 OUTER APPLY(SELECT A.username FROM dbo.ProjectAccountAccess S JOIN dbo.admin A ON A.AccountId=S.AccountId AND A.IsActive=1 AND A.RoleName=N'ProjectUser' WHERE S.ProjectId=P.ProjectId AND S.IsActive=1 AND S.IsResponsible=1) R
 WHERE " + ProjectAuthorization.Visible + " ORDER BY P.Code,P.ProjectId");
        c.Parameters.Add("@salary",SqlDbType.Bit).Value=user.CanViewSalary; ProjectAuthorization.Bind(c,user);c.Parameters.Add("@admin",SqlDbType.Bit).Value=user.Role==UserRole.Admin;c.Parameters.Add("@globalExport",SqlDbType.Bit).Value=user.CanExportPersonalData;
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return data;
    }
    internal async Task SaveProjectAsync(Wire.ProjectDetails input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("workforce/project", input, ct); return; }
        user.DemandAdmin(); using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); var version = PersonnelConcurrency.RequireVersion(input.Version);
        string name = Text(input.Name, 100, true), site = Text(input.Site, 100), customer = Text(input.CustomerLabel, 100); var start = Day(input.Start); var end = Day(input.End); Dates(start, end);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Admin, ct);
        var current = await ProjectAsync(w, input.ProjectId, false, ct);
        if (!current.Version.SequenceEqual(version)) throw new InvalidOperationException("Proje seçimden sonra değişmiş; listeyi yenileyin.");
        using (var check = w.Command(@"SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@id AND CancelledUtc IS NULL AND
          ((@start IS NOT NULL AND StartDate<@start) OR (@end IS NOT NULL AND (EndDate IS NULL OR EndDate>@end)) OR
          (@active=0 AND (EndDate IS NULL OR EndDate>=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time'))))"))
        {
            Id(check, "@id", input.ProjectId); DateParam(check, "@start", start); DateParam(check, "@end", end); check.Parameters.Add("@active", SqlDbType.Bit).Value = input.Active;
            if (Convert.ToInt32(await check.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Atamalar yeni proje aralığı/durumuyla çelişiyor. Önce ilgili atamaları gerekçeli düzeltin.");
        }
        using(var calendar=w.Command(@"SELECT COUNT(*) FROM dbo.PlannedShifts WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@id AND Status<>N'Cancelled' AND ((@start IS NOT NULL AND CONVERT(date,LocalStart)<@start) OR (@end IS NOT NULL AND CONVERT(date,DATEADD(minute,-1,LocalEnd))>@end) OR (@active=0 AND EndUtc>SYSUTCDATETIME()))"))
        {Id(calendar,"@id",input.ProjectId);DateParam(calendar,"@start",start);DateParam(calendar,"@end",end);calendar.Parameters.Add("@active",SqlDbType.Bit).Value=input.Active;if(Convert.ToInt32(await calendar.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Etkin vardiyalar yeni proje tarih/durumuyla çelişiyor; önce takvimde iptal edin.");}
        using (var attendance=w.Command(@"SELECT COUNT(*) FROM dbo.Timesheets WHERE ProjectId=@id AND Status<>N'Cancelled' AND
 ((@start IS NOT NULL AND COALESCE(CONVERT(date,LocalStart),WorkDate)<@start) OR (@end IS NOT NULL AND COALESCE(CONVERT(date,DATEADD(minute,-1,LocalEnd)),WorkDate)>@end) OR (@active=0 AND Status IN(N'Draft',N'Returned',N'Submitted')))") )
        { Id(attendance,"@id",input.ProjectId);DateParam(attendance,"@start",start);DateParam(attendance,"@end",end);attendance.Parameters.Add("@active",SqlDbType.Bit).Value=input.Active;if(Convert.ToInt32(await attendance.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Projenin puantaj tarihleri veya bekleyen puantajları bu değişikliğe izin vermiyor."); }
        using(var allocated=w.Command(@"SELECT COUNT(*) FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.ReleasedUtc IS NULL JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE S.ProjectId=@id AND ((@start IS NOT NULL AND CONVERT(date,S.LocalStart)<@start) OR (@end IS NOT NULL AND CONVERT(date,DATEADD(minute,-1,S.LocalEnd))>@end) OR (@active=0 AND T.Status IN(N'Draft',N'Returned',N'Submitted')))") )
        {Id(allocated,"@id",input.ProjectId);DateParam(allocated,"@start",start);DateParam(allocated,"@end",end);allocated.Parameters.Add("@active",SqlDbType.Bit).Value=input.Active;if(Convert.ToInt32(await allocated.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Projeye dağıtılmış zaman parçaları bu tarih/durum değişikliğine izin vermiyor.");}
        using var c = w.Command("UPDATE dbo.FinanceProjects SET Name=@name,Site=@site,CustomerLabel=@customer,StartDate=@start,EndDate=@end,IsActive=@active WHERE ProjectId=@id AND RecordVersion=@version");
        c.Text("@name", name, 100); c.Text("@site", site, 100); c.Text("@customer", customer, 100); DateParam(c, "@start", start); DateParam(c, "@end", end); Id(c, "@id", input.ProjectId);
        c.Parameters.Add("@active", SqlDbType.Bit).Value = input.Active; c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Proje sürümü değişmiş."); await w.CommitAsync(ct);
    }
    internal async Task<DataTable> TeamsAsync(Guid project, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"workforce/{project:D}/teams", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        using var c = w.Command("SELECT TOP(500) TeamId,ProjectId,Code,Name,IsActive,RecordVersion FROM dbo.ProjectTeams WHERE ProjectId=@id ORDER BY Code,TeamId"); Id(c, "@id", project);
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return data;
    }
    internal async Task SaveTeamAsync(Wire.TeamWrite input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("workforce/team", input, ct); return; }
        using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); string code = ProjectFinanceService.Reference(input.Code, 30), name = Text(input.Name, 100, true);
        byte[]? version = input.TeamId.HasValue ? PersonnelConcurrency.RequireVersion(input.Version) : null;
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.ManageWorkforce,ct);
        await ProjectAsync(w, input.ProjectId, true, ct);
        if (input.TeamId.HasValue && !input.Active)
        {
            using var future = w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE TeamId=@team AND ProjectId=@project AND CancelledUtc IS NULL AND (EndDate IS NULL OR EndDate>=CONVERT(date,SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time'))");
            Id(future, "@team", input.TeamId); Id(future,"@project",input.ProjectId);
            if (Convert.ToInt32(await future.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Ekibin güncel/gelecek atamaları var; önce ilgili atamaları gerekçeli düzenleyin.");
        }
        if(input.TeamId.HasValue&&!input.Active){using var calendar=w.Command("SELECT COUNT(*) FROM dbo.PlannedShifts WITH(UPDLOCK,HOLDLOCK) WHERE TeamId=@team AND Status<>N'Cancelled' AND EndUtc>SYSUTCDATETIME()");Id(calendar,"@team",input.TeamId);if(Convert.ToInt32(await calendar.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ekibin güncel/gelecek vardiyası var; önce iptal edin.");}
        if(input.TeamId.HasValue&&!input.Active){using var attendance=w.Command("SELECT COUNT(*) FROM dbo.Timesheets T JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=T.RosterAssignmentId WHERE A.TeamId=@team AND T.Status IN(N'Draft',N'Returned',N'Submitted')");Id(attendance,"@team",input.TeamId);if(Convert.ToInt32(await attendance.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ekibin bekleyen puantajları var; önce sonuçlandırın.");}
        if(input.TeamId.HasValue&&!input.Active){using var allocation=w.Command("SELECT COUNT(*) FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.ReleasedUtc IS NULL JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=S.RosterAssignmentId WHERE A.TeamId=@team AND T.Status IN(N'Draft',N'Returned',N'Submitted')");Id(allocation,"@team",input.TeamId);if(Convert.ToInt32(await allocation.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ekibe dağıtılmış bekleyen puantaj var.");}
        using var c = w.Command(input.TeamId.HasValue ? "UPDATE dbo.ProjectTeams SET Name=@name,IsActive=@active WHERE TeamId=@team AND ProjectId=@project AND Code=@code AND RecordVersion=@version" : "INSERT dbo.ProjectTeams(ProjectId,Code,Name,IsActive,CreatedBy) VALUES(@project,@code,@name,@active,@actor)");
        Id(c, "@project", input.ProjectId); Id(c, "@team", input.TeamId); c.Text("@code", code, 30); c.Text("@name", name, 100); c.Text("@actor", user.Username); c.Parameters.Add("@active", SqlDbType.Bit).Value = input.Active;
        c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = (object?)version ?? DBNull.Value;
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Ekip değişmiş; listeyi yenileyin. Ekip kodu değiştirilemez."); await w.CommitAsync(ct);
    }
    internal async Task<PersonnelPage> AssignmentsAsync(Guid project, long index, int size, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).PageAsync($"workforce/{project:D}/assignments?index={index}&size={size}", ct);
        if (index < 0 || size is < 1 or > 200) throw new ArgumentException("Geçersiz sayfa.");
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Read,ct);
        using var count = w.Command("SELECT COUNT_BIG(*) FROM dbo.PersonnelProjectAssignments WHERE ProjectId=@id"); Id(count, "@id", project); long total = Convert.ToInt64(await count.ExecuteScalarAsync(ct)); index = Math.Min(index, total == 0 ? 0 : (total - 1) / size);
        using var c = w.Command(@"SELECT A.*,(SELECT EmploymentId FROM dbo.EmploymentRecordLinks WHERE RecordKind=N'Assignment' AND RecordKey=CONVERT(nvarchar(64),A.AssignmentId)) EmploymentId,CASE WHEN @scoped=1 THEN CAST(NULL AS bigint) ELSE I.TC END TC,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,T.Name TeamName FROM dbo.PersonnelProjectAssignments A
          JOIN dbo.PersonnelIdentities I ON I.PersonId=A.PersonId LEFT JOIN dbo.Calisan C ON C.PersonId=A.PersonId AND A.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=A.PersonId AND A.PersonnelGroup=1
          LEFT JOIN dbo.ProjectTeams T ON T.TeamId=A.TeamId WHERE A.ProjectId=@id ORDER BY A.StartDate DESC,A.AssignmentId OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY");
        ProjectAuthorization.Bind(c,user);Id(c, "@id", project); c.Parameters.Add("@skip", SqlDbType.BigInt).Value = index * size; c.Parameters.Add("@take", SqlDbType.Int).Value = size;
        var data = await c.TableAsync(ct); await w.CommitAsync(ct); return new(data, total, index, size);
    }
    internal async Task AssignAsync(Wire.AssignmentWrite input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("workforce/assign", input, ct); return; }
        using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); var version = PersonnelConcurrency.RequireVersion(input.PersonVersion); var projectVersion = PersonnelConcurrency.RequireVersion(input.ProjectVersion);
        if (!Enum.IsDefined(input.Group) || input.PersonId == Guid.Empty) throw new ArgumentException("Güncel personel seçin."); var start = Day(input.Start)!.Value; var end = Day(input.End); Dates(start, end);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.ManageWorkforce,ct);
        using (var gate = w.Command("DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=@key,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000; IF @r<0 THROW 51072,N'Atama kilidi alınamadı.',1;"))
        { gate.Text("@key", "Assignment.Person." + input.PersonId.ToString("N"), 100); await gate.ExecuteNonQueryAsync(ct); }
        var project = await ProjectAsync(w, input.ProjectId, true, ct);
        if (!project.Version.SequenceEqual(projectVersion)) throw new InvalidOperationException("Proje değişmiş; listeyi yenileyin.");
        if ((project.Start.HasValue && start < project.Start) || (project.End.HasValue && (!end.HasValue || end > project.End))) throw new InvalidOperationException("Atama proje tarih aralığı dışında.");
        long selectedTc = user.Role==UserRole.ProjectUser ? await ProjectAuthorization.PersonTcAsync(w,user,input.ProjectId,input.PersonId,(PersonnelGroup)input.Group,ct) : input.Tc;
        var repo = new PersonnelRepository(w); await repo.LockPersonAsync((PersonnelGroup)input.Group, selectedTc, ct, version);
        if (await repo.IdentityAsync((PersonnelGroup)input.Group, selectedTc, ct) != input.PersonId) throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        await EmploymentService.ValidateRangeAsync(w, (PersonnelGroup)input.Group, input.PersonId, start, end, ct);
        if (input.TeamId.HasValue) { using var team = w.Command("SELECT COUNT(*) FROM dbo.ProjectTeams WITH(HOLDLOCK) WHERE TeamId=@team AND ProjectId=@project AND IsActive=1 AND RecordVersion=@version"); Id(team, "@team", input.TeamId); Id(team, "@project", input.ProjectId); team.Parameters.Add("@version", SqlDbType.Binary, 8).Value = PersonnelConcurrency.RequireVersion(input.TeamVersion); if (Convert.ToInt32(await team.ExecuteScalarAsync(ct)) != 1) throw new InvalidOperationException("Ekip değişmiş, pasif veya başka projeye ait."); }
        else if (input.TeamVersion != null) throw new ArgumentException("Ekipsiz atamada ekip sürümü olamaz.");
        using (var overlap = w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND CancelledUtc IS NULL AND ((@extra=0 AND IsSupplementary=0) OR (ProjectId=@project AND ISNULL(TeamId,'00000000-0000-0000-0000-000000000000')=ISNULL(@team,'00000000-0000-0000-0000-000000000000'))) AND StartDate<=COALESCE(@end,CONVERT(date,'99991231')) AND @start<=COALESCE(EndDate,CONVERT(date,'99991231'))"))
        { overlap.Parameters.Add("@extra",SqlDbType.Bit).Value=input.IsSupplementary;Id(overlap,"@project",input.ProjectId);Id(overlap,"@team",input.TeamId);Id(overlap, "@person", input.PersonId); DateParam(overlap, "@start", start); DateParam(overlap, "@end", end); if (Convert.ToInt32(await overlap.ExecuteScalarAsync(ct)) > 0) throw new InvalidOperationException("Asıl atama veya aynı proje/ekip ataması bu tarihlerle çakışıyor; gün içi başka proje için ek atama seçin."); }
        using var c = w.Command("INSERT dbo.PersonnelProjectAssignments(ProjectId,TeamId,PersonId,PersonnelGroup,StartDate,EndDate,CreatedBy,IsSupplementary) VALUES(@project,@team,@person,@group,@start,@end,@actor,@extra)");
        c.Parameters.Add("@extra",SqlDbType.Bit).Value=input.IsSupplementary;Id(c, "@project", input.ProjectId); Id(c, "@team", input.TeamId); Id(c, "@person", input.PersonId); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)input.Group; DateParam(c, "@start", start); DateParam(c, "@end", end); c.Text("@actor", user.Username);
        await c.ExecuteNonQueryAsync(ct); await w.CommitAsync(ct);
    }
    internal async Task CancelAsync(Wire.AssignmentCancel input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("workforce/cancel", input, ct); return; }
        using var reason = AuditContext.UseReason(AuditContext.ValidateReason(input.Reason ?? "")); var version = PersonnelConcurrency.RequireVersion(input.Version);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await TimesheetService.AllocationGateAsync(w,ct); await AccountAuthorization.DemandAsync(w, user, AccountAccess.Read, ct);
        Guid project;
        using(var find=w.Command("SELECT ProjectId FROM dbo.PersonnelProjectAssignments WITH(READUNCOMMITTED) WHERE AssignmentId=@id")){Id(find,"@id",input.AssignmentId);project=await find.ExecuteScalarAsync(ct) is Guid id?id:throw new UnauthorizedAccessException("Atama için erişim yok.");}
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.ManageWorkforce,ct);
        await SchedulingRules.RosterCancelAsync(w,input.AssignmentId,ct);
        using var c = w.Command("UPDATE dbo.PersonnelProjectAssignments SET CancelledUtc=SYSUTCDATETIME(),CancelledBy=@actor WHERE AssignmentId=@id AND RecordVersion=@version AND CancelledUtc IS NULL");
        Id(c, "@id", input.AssignmentId); c.Text("@actor", user.Username); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = version;
        if (await c.ExecuteNonQueryAsync(ct) != 1) throw new InvalidOperationException("Atama değişmiş veya iptal edilmiş; listeyi yenileyin."); await w.CommitAsync(ct);
    }
}
