using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class SchedulingService
{
    internal async Task CopyWeekAsync(Wire.WeekCopy input, CancellationToken ct)
    {
        if (DesktopConnection.IsRemote(connectionString)) { await RemoteApi.For(connectionString, user).Done("schedule/copy-week", input, ct); return; }
        EmploymentService.Day(input.SourceWeek); EmploymentService.Day(input.TargetWeek);
        if (input.BatchId == Guid.Empty || input.SourceWeek == input.TargetWeek || input.SourceWeek.DayOfWeek != DayOfWeek.Monday || input.TargetWeek.DayOfWeek != DayOfWeek.Monday || input.Sources is not { Length: > 0 and <= 50 } || input.Sources.Any(x => x == null || x.ShiftId == Guid.Empty || x.Version is not { Length: 8 }) || input.Sources.Select(x => x.ShiftId).Distinct().Count() != input.Sources.Length) throw new ArgumentException("Farklı pazartesi haftaları ve en fazla 50 vardiya seçin.");
        SchedulingRules.UtcRange(input.TargetWeek, input.TargetWeek.AddDays(8));
        foreach (var item in input.Sources) PersonnelConcurrency.RequireVersion(item.Version);
        string text = AuditContext.ValidateReason(input.Reason); using var reason = AuditContext.UseReason(text);
        await using var w = await SqlUnitOfWork.OpenAsync(connectionString, ct); await ProjectAuthorization.DemandAsync(w, user, input.ProjectId, ProjectAccess.PlanShifts, ct);
        Guid actor; using (var who = w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))")) actor = (Guid)(await who.ExecuteScalarAsync(ct))!;
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { input.ProjectId, input.SourceWeek, input.TargetWeek, Sources = input.Sources.OrderBy(x => x.ShiftId).Select(x => new { x.ShiftId, Version = Convert.ToHexString(x.Version) }), Reason = text, Actor = actor }));
        using (var old = w.Command("SELECT PayloadHash FROM dbo.ScheduleCopyBatches WITH(UPDLOCK,HOLDLOCK) WHERE BatchId=@id")) { Id(old, "@id", input.BatchId); if (await old.ExecuteScalarAsync(ct) is byte[] stored) { if (!CryptographicOperations.FixedTimeEquals(stored, hash)) throw new InvalidOperationException("Kopyalama kimliği farklı önizleme için kullanılmış."); await w.CommitAsync(ct); return; } }
        var sources = new List<(DataRow Shift, DataTable People)>();
        foreach (var item in input.Sources.OrderBy(x => x.ShiftId))
        { var shift = await RowAsync(w, "PlannedShifts", "ShiftId", item.ShiftId, ct) ?? throw new InvalidOperationException("Kaynak vardiya yok."); ProjectRow(shift, input.ProjectId); Version(shift, item.Version); if (shift.Field<string>("Status") == "Cancelled" || shift.Field<DateTime>("LocalStart") < input.SourceWeek || shift.Field<DateTime>("LocalStart") >= input.SourceWeek.AddDays(7)) throw new InvalidOperationException("Kaynak vardiya seçilen başlangıç haftasında değil veya iptal edilmiş."); using var q = w.Command("SELECT PersonId,EmploymentId,PersonnelGroup FROM dbo.ShiftAssignments WITH(UPDLOCK,HOLDLOCK) WHERE ShiftId=@id AND CancelledUtc IS NULL ORDER BY PersonId"); Id(q, "@id", item.ShiftId); sources.Add((shift, await q.TableAsync(ct))); }
        if (sources.Sum(x => x.People.Rows.Count) > 200) throw new InvalidOperationException("Toplu kopyalama en fazla 200 kişi ataması içerir.");
        foreach (var person in sources.SelectMany(x => x.People.AsEnumerable()).Select(r => r.Field<Guid>("PersonId")).Distinct().Order()) await SchedulingRules.LockPersonAsync(w, person, ct);
        using (var batch = w.Command("INSERT dbo.ScheduleCopyBatches(BatchId,ProjectId,SourceWeek,TargetWeek,PayloadHash,ActorAccountId,Reason) VALUES(@id,@project,@source,@target,@hash,@actor,@reason)")) { Id(batch, "@id", input.BatchId); Id(batch, "@project", input.ProjectId); Time(batch, "@source", input.SourceWeek); Time(batch, "@target", input.TargetWeek); batch.Parameters.Add("@hash", SqlDbType.Binary, 32).Value = hash; Id(batch, "@actor", actor); batch.Text("@reason", text, 500); await batch.ExecuteNonQueryAsync(ct); }
        var offset = input.TargetWeek - input.SourceWeek;
        foreach (var item in sources)
        {
            var s = item.Shift; Guid id = Guid.NewGuid(); var start = DateTime.SpecifyKind(s.Field<DateTime>("LocalStart") + offset, DateTimeKind.Unspecified); var end = DateTime.SpecifyKind(s.Field<DateTime>("LocalEnd") + offset, DateTimeKind.Unspecified); var utc = SchedulingRules.UtcRange(start, end); var team = s.Field<Guid?>("TeamId"); await ProjectRangeAsync(w, input.ProjectId, team, start, end, ct);
            using (var c = w.Command(@"INSERT dbo.PlannedShifts(ShiftId,CopyBatchId,SourceShiftId,SourceShiftVersion,ProjectId,TemplateId,TemplateVersion,TeamId,Name,Site,LocalStart,LocalEnd,StartUtc,EndUtc,TimeZoneId,PolicyVersion,BreakMinutes,RequiredCount,CreatedBy,CreateReason)
 VALUES(@id,@batch,@source,@sourceversion,@project,@template,@version,@team,@name,@site,@start,@end,@utcstart,@utcend,@zone,@policy,@break,@count,@actor,@reason)"))
            { Id(c, "@id", id); Id(c, "@batch", input.BatchId); Id(c, "@source", s.Field<Guid>("ShiftId")); c.Parameters.Add("@sourceversion", SqlDbType.Binary, 8).Value = s["RecordVersion"]; Id(c, "@project", input.ProjectId); Id(c, "@template", s.Field<Guid>("TemplateId")); c.Parameters.Add("@version", SqlDbType.Binary, 8).Value = s["TemplateVersion"]; Id(c, "@team", team); c.Text("@name", s.Field<string>("Name")!, 100); c.Text("@site", s.Field<string>("Site")!, 100); Time(c, "@start", start); Time(c, "@end", end); Time(c, "@utcstart", utc.Start); Time(c, "@utcend", utc.End); c.Text("@zone", DailyClosingService.BusinessZone.Id, 100); c.Text("@policy",s.Field<string>("PolicyVersion")!,40); c.Parameters.Add("@break", SqlDbType.Int).Value = s["BreakMinutes"]; c.Parameters.Add("@count", SqlDbType.Int).Value = s["RequiredCount"]; c.Text("@actor", user.Username); c.Text("@reason", text, 500); await c.ExecuteNonQueryAsync(ct); }
            foreach (DataRow person in item.People.Rows)
            { Guid pid = person.Field<Guid>("PersonId"); var target = await TargetAsync(w, input.ProjectId, team, pid, (PersonnelGroup)person.Field<int>("PersonnelGroup"), null, start, end, ct); if (target.Employment != person.Field<Guid>("EmploymentId")) throw new InvalidOperationException("Kopyalanan kişi yeni çalışma dönemine geçmiş; yeni dönemi açıkça yeniden planlayın."); await SchedulingRules.ClearAsync(w, pid, start, end, null, null, true, ct); using var c = w.Command("INSERT dbo.ShiftAssignments(ShiftAssignmentId,ShiftId,PersonId,EmploymentId,PersonnelGroup,RosterAssignmentId,CreatedBy) VALUES(@id,@shift,@person,@employment,@group,@roster,@actor)"); Id(c, "@id", Guid.NewGuid()); Id(c, "@shift", id); Id(c, "@person", pid); Id(c, "@employment", target.Employment); c.Parameters.Add("@group", SqlDbType.Int).Value = person["PersonnelGroup"]; Id(c, "@roster", target.Roster); c.Text("@actor", user.Username); await c.ExecuteNonQueryAsync(ct); }
        }
        await w.CommitAsync(ct);
    }
}
