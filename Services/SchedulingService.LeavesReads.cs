using System.Data;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class SchedulingService
{
    internal async Task RequestLeaveAsync(Wire.LeaveRequestWrite input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("schedule/leave", input, ct); return; }
        if (!Enum.IsDefined(input.Group) || input.LeaveId == Guid.Empty || input.End - input.Start > TimeSpan.FromDays(366)) throw new ArgumentException("İzin aralığını/kimliğini kontrol edin."); var utc = SchedulingRules.UtcRange(input.Start, input.End); string text = AuditContext.ValidateReason(input.Reason); using var reason = AuditContext.UseReason(text); await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, input.ProjectId, ProjectAccess.PlanShifts, ct);
        var old = await RowAsync(w, "LeaveRequests", "LeaveId", input.LeaveId, ct); if (old != null) { ProjectRow(old, input.ProjectId); if (old.Field<Guid>("PersonId") != input.PersonId || old.Field<int>("PersonnelGroup") != (int)input.Group || old.Field<DateTime>("LocalStart") != input.Start || old.Field<DateTime>("LocalEnd") != input.End || old.Field<string>("CreateReason") != text || !((byte[])old["PersonVersion"]).SequenceEqual(input.PersonVersion)) throw new InvalidOperationException("Talep kimliği başka izin için kullanılmış."); await w.CommitAsync(ct); return; }
        await ProjectRangeAsync(w, input.ProjectId, null, input.Start, input.End, ct); var target = await TargetAsync(w, input.ProjectId, null, input.PersonId, (PersonnelGroup)input.Group, input.PersonVersion, input.Start, input.End, ct);
        using var c = w.Command(@"INSERT dbo.LeaveRequests(LeaveId,ProjectId,PersonId,EmploymentId,PersonnelGroup,PersonVersion,RosterAssignmentId,LocalStart,LocalEnd,StartUtc,EndUtc,TimeZoneId,CreatedBy,CreateReason)
 VALUES(@id,@project,@person,@employment,@group,@version,@roster,@start,@end,@utcstart,@utcend,@zone,@actor,@reason)"); Id(c, "@id", input.LeaveId); Id(c, "@project", input.ProjectId); Id(c, "@person", input.PersonId); Id(c, "@employment", target.Employment); c.Parameters.Add("@group", SqlDbType.Int).Value = (int)input.Group; c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = PersonnelConcurrency.RequireVersion(input.PersonVersion); Id(c, "@roster", target.Roster); Time(c, "@start", input.Start); Time(c, "@end", input.End); Time(c, "@utcstart", utc.Start); Time(c, "@utcend", utc.End); c.Text("@zone", DailyClosingService.BusinessZone.Id, 100); c.Text("@actor", user.Username); c.Text("@reason", text, 500); await c.ExecuteNonQueryAsync(ct); await w.CommitAsync(ct);
    }
    internal async Task LeaveStateAsync(Wire.LeaveRequestState input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("schedule/leave-state", input, ct); return; }
        if (input.State is not ("Approved" or "Rejected" or "Cancelled")) throw new ArgumentException("Geçersiz izin durumu."); string text = AuditContext.ValidateReason(input.Reason); using var reason = AuditContext.UseReason(text); await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, input.ProjectId, ProjectAccess.ApproveLeaves, ct); var row = await RowAsync(w, "LeaveRequests", "LeaveId", input.LeaveId, ct) ?? throw new InvalidOperationException("İzin talebi yok."); ProjectRow(row, input.ProjectId); if (row.Field<string>("Status") == input.State) { await w.CommitAsync(ct); return; }
        Version(row, input.Version); if (!((row.Field<string>("Status") == "Requested") || (row.Field<string>("Status") == "Approved" && input.State == "Cancelled"))) throw new InvalidOperationException("İzin artık bu duruma geçirilemez.");
        await SchedulingRules.LockPersonAsync(w, row.Field<Guid>("PersonId"), ct);
        if (input.State == "Approved")
        { var start = DateTime.SpecifyKind(row.Field<DateTime>("LocalStart"), DateTimeKind.Unspecified); var end = DateTime.SpecifyKind(row.Field<DateTime>("LocalEnd"), DateTimeKind.Unspecified); await ProjectRangeAsync(w, input.ProjectId, null, start, end, ct); var target = await TargetAsync(w, input.ProjectId, null, row.Field<Guid>("PersonId"), (PersonnelGroup)row.Field<int>("PersonnelGroup"), (byte[])row["PersonVersion"], start, end, ct); if (target.Employment != row.Field<Guid>("EmploymentId") || target.Roster != row.Field<Guid>("RosterAssignmentId")) throw new InvalidOperationException("İzin eski dönem/atamaya ait. İptal edip yeniden talep oluşturun."); await SchedulingRules.ClearAsync(w, row.Field<Guid>("PersonId"), start, end, null, input.LeaveId, true, ct); }
        using var c = w.Command(input.State == "Cancelled" ? "UPDATE dbo.LeaveRequests SET Status=N'Cancelled',CancelledUtc=SYSUTCDATETIME(),CancelledBy=@actor,CancelReason=@reason WHERE LeaveId=@id" : "UPDATE dbo.LeaveRequests SET Status=@state,DecidedUtc=SYSUTCDATETIME(),DecidedBy=@actor,DecisionReason=@reason WHERE LeaveId=@id"); Id(c, "@id", input.LeaveId); c.Text("@state", input.State, 12); c.Text("@actor", user.Username); c.Text("@reason", text, 500); await c.ExecuteNonQueryAsync(ct); await w.CommitAsync(ct);
    }
    internal async Task<DataTable> TemplatesAsync(Guid project, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync($"schedule/{project:D}/templates", ct);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Read, ct); using var c = w.Command("SELECT TOP(501) T.*,G.Name TeamName FROM dbo.ShiftTemplates T LEFT JOIN dbo.ProjectTeams G ON G.TeamId=T.TeamId WHERE T.ProjectId=@id ORDER BY T.Name,T.TemplateId"); Id(c, "@id", project); var data = await c.TableAsync(ct); if (data.Rows.Count > 500) throw new InvalidOperationException("Proje şablonları 500 sınırını aşıyor."); await w.CommitAsync(ct); return data;
    }
    internal async Task<(DataTable Shifts, DataTable Assignments, DataTable Leaves, DataTable Legacy)> WeekAsync(Guid project, DateTime first, Guid? team, bool history, CancellationToken ct)
    {
        EmploymentService.Day(first); DateTime end = first.AddDays(7); SchedulingRules.UtcRange(first, end);
        if (DesktopConnection.IsRemote(connectionString)) { var result = await RemoteApi.For(connectionString, user).SendAsync<Wire.CalendarWeek>($"schedule/{project:D}/week?first={RemoteApi.Day(first)}&history={history}" + (team.HasValue ? "&team=" + team.Value.ToString("D") : ""), null, ct); return (RemoteApi.Decode(result.Shifts), RemoteApi.Decode(result.Assignments), RemoteApi.Decode(result.Leaves), RemoteApi.Decode(result.LegacyLeaves)); }
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, project, ProjectAccess.Read, ct);
        async Task<DataTable> Read(string sql) { using var c = w.Command(sql); Id(c, "@project", project); Id(c, "@team", team); Time(c, "@first", first); Time(c, "@end", end); c.Parameters.Add("@history", SqlDbType.Bit).Value = history; var data = await c.TableAsync(ct); if (data.Rows.Count > 2000) throw new InvalidOperationException("Hafta 2000 kayıt sınırını aşıyor; ekip filtresiyle daraltın."); return data; }
        var shifts = await Read(@"SELECT TOP(2001) S.*,T.Code TeamCode,T.Name TeamName,(SELECT COUNT(*) FROM dbo.ShiftAssignments A WHERE A.ShiftId=S.ShiftId AND A.CancelledUtc IS NULL) AssignedCount,
 S.RequiredCount-(SELECT COUNT(*) FROM dbo.ShiftAssignments A WHERE A.ShiftId=S.ShiftId AND A.CancelledUtc IS NULL) MissingCount,
 DATEDIFF(minute,S.LocalStart,S.LocalEnd)-S.BreakMinutes PlannedMinutes,
 (SELECT TOP(1) A.username FROM dbo.ProjectAccountAccess G JOIN dbo.admin A ON A.AccountId=G.AccountId WHERE G.ProjectId=S.ProjectId AND G.IsResponsible=1 AND G.IsActive=1 AND A.IsActive=1 AND A.RoleName=N'ProjectUser') ProjectResponsible
 FROM dbo.PlannedShifts S LEFT JOIN dbo.ProjectTeams T ON T.TeamId=S.TeamId WHERE S.ProjectId=@project AND S.LocalStart<@end AND S.LocalEnd>@first AND (@team IS NULL OR S.TeamId=@team) AND (@history=1 OR S.Status<>N'Cancelled') ORDER BY S.LocalStart,S.ShiftId");
        var assignments = await Read(@"SELECT TOP(2001) A.ShiftAssignmentId,A.ShiftId,A.PersonId,A.EmploymentId,A.PersonnelGroup,A.RecordVersion,A.CancelledUtc,A.CreatedBy,S.Name ShiftName,S.LocalStart,S.LocalEnd,S.Status,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD
 FROM dbo.ShiftAssignments A JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId LEFT JOIN dbo.Calisan C ON C.PersonId=A.PersonId AND A.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=A.PersonId AND A.PersonnelGroup=1
 WHERE S.ProjectId=@project AND S.LocalStart<@end AND S.LocalEnd>@first AND (@team IS NULL OR S.TeamId=@team) AND (@history=1 OR (S.Status<>N'Cancelled' AND A.CancelledUtc IS NULL)) ORDER BY S.LocalStart,A.ShiftAssignmentId");
        var leaves = await Read(@"SELECT TOP(2001) L.LeaveId,L.PersonId,L.EmploymentId,L.PersonnelGroup,L.LocalStart,L.LocalEnd,L.Status,L.PolicyVersion,L.CreatedBy,L.CreatedUtc,L.DecidedBy,L.DecidedUtc,L.CancelledBy,L.CancelledUtc,L.RecordVersion,DATEDIFF(minute,L.LocalStart,L.LocalEnd) CalendarMinutes,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD
 FROM dbo.LeaveRequests L JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=L.RosterAssignmentId LEFT JOIN dbo.Calisan C ON C.PersonId=L.PersonId AND L.PersonnelGroup=0 LEFT JOIN dbo.Joker J ON J.PersonId=L.PersonId AND L.PersonnelGroup=1
 WHERE L.ProjectId=@project AND L.LocalStart<@end AND L.LocalEnd>@first AND (@team IS NULL OR A.TeamId=@team) AND (@history=1 OR L.Status IN(N'Requested',N'Approved')) ORDER BY L.LocalStart,L.LeaveId");
        var legacy = await Read(@"SELECT DISTINCT TOP(2001) I.IzinId,I.PersonId,I.EmploymentId,C.AD,C.SOYAD,I.IZINBASLANGIC,I.IZINBITIS,N'Doğrudan Genel izni (ayrı onay adımı yok)' Source
 FROM dbo.izinler I JOIN dbo.Calisan C ON C.PersonId=I.PersonId
 WHERE I.IZINBASLANGIC<CONVERT(date,@end) AND I.IZINBITIS>=CONVERT(date,@first) AND EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks E ON E.RecordKind=N'Assignment' AND E.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 WHERE A.ProjectId=@project AND A.PersonId=I.PersonId AND E.EmploymentId=I.EmploymentId AND (@history=1 OR A.CancelledUtc IS NULL) AND (@team IS NULL OR A.TeamId=@team) AND A.StartDate<=I.IZINBITIS AND (A.EndDate IS NULL OR A.EndDate>=I.IZINBASLANGIC)) ORDER BY I.IZINBASLANGIC,I.IzinId");
        await w.CommitAsync(ct); return (shifts, assignments, leaves, legacy);
    }
}
