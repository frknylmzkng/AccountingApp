using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal static DateTime EventMinute(DateTime value)=>DateTime.SpecifyKind(new DateTime(value.Ticks-value.Ticks%TimeSpan.TicksPerMinute),DateTimeKind.Unspecified);
    private static string EventText(string? value,int max)
    {value=(value??string.Empty).Trim();if(value.Length<1||value.Length>max||value.Any(char.IsControl))throw new ArgumentException("Olay kaynak/satır kimliğini kontrol edin.");return value;}
    private static DateTime EventUtc(DateTime value)
    {
        var zone=DailyClosingService.BusinessZone;
        if(value.Kind!=DateTimeKind.Unspecified||value.Year is <1900 or >2199||zone.IsInvalidTime(value)||zone.IsAmbiguousTime(value))throw new ArgumentException("Ham olayın yerel zamanı geçersiz/belirsiz veya 1900–2199 aralığı dışında.");
        return TimeZoneInfo.ConvertTimeToUtc(value,zone);
    }
    private static byte[] EventHash(string operation,object request)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Operation=operation,Request=request}));
    private static async Task<DataRow> EventRowAsync(IUnitOfWork w,Guid project,Guid id,CancellationToken ct)
    {
        using var c=w.Command("SELECT * FROM dbo.AttendanceEvents WITH(UPDLOCK,HOLDLOCK) WHERE EventId=@id AND ProjectId=@project");Id(c,"@id",id);Id(c,"@project",project);var rows=await c.TableAsync(ct);
        return rows.Rows.Count==1?rows.Rows[0]:throw new InvalidOperationException("Olay bu projede bulunamadı.");
    }
    private static async Task SnapshotTargetAsync(IUnitOfWork w,Guid project,Guid person,int group,Guid employment,Guid roster,byte[] personVersion,byte[] rosterVersion,CancellationToken ct)
    {
        using(var c=w.Command($"SELECT COUNT(*) FROM {PersonnelRepository.Table((PersonnelGroup)group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND CurrentEmploymentId=@employment AND RecordVersion=@version AND ArchivedUtc IS NULL"))
        {Id(c,"@person",person);Id(c,"@employment",employment);c.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(personVersion);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);}
        using var r=w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE AssignmentId=@id AND ProjectId=@project AND PersonId=@person AND PersonnelGroup=@group AND RecordVersion=@version AND CancelledUtc IS NULL");
        Id(r,"@id",roster);Id(r,"@project",project);Id(r,"@person",person);r.Parameters.Add("@group",SqlDbType.Int).Value=group;r.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(rosterVersion);
        if(Convert.ToInt32(await r.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Kaynak proje ataması değişmiş; yeniden seçin.");
    }
    private static async Task EnsureNotMatchedAsync(IUnitOfWork w,Guid timesheet,CancellationToken ct)
    {
        using var c=w.Command("SELECT COUNT(*) FROM dbo.AttendanceMatches WITH(UPDLOCK,HOLDLOCK) WHERE TimesheetId=@id AND ReleasedUtc IS NULL");Id(c,"@id",timesheet);
        if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Puantaj ham olaylara bağlı. Önce taslak/iade durumunda eşlemeyi kaldırın; ardından düzenleyin veya iptal edin.");
    }
    internal async Task ImportEventsAsync(Wire.AttendanceImport input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("attendance/import",input,ct);return;}
        if(!Enum.IsDefined(input.Group)||input.PersonId==Guid.Empty||input.EmploymentId==Guid.Empty||input.RosterAssignmentId==Guid.Empty||input.Rows==null||input.Rows.Length is <1 or >200||input.Rows.Any(r=>r==null||r.EventId==Guid.Empty)||input.Rows.Select(r=>r.EventId).Distinct().Count()!=input.Rows.Length)
            throw new ArgumentException("1–200 farklı olay ve geçerli kişi/dönem/atama gerekiyor.");
        string source=EventText(input.SourceSystem,50);var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in input.Rows){if(!keys.Add(EventText(row.ExternalId,100))||row.Direction is not ("In" or "Out"))throw new ArgumentException("Kaynak kimliği tekrarlı veya olay yönü geçersiz (In/Out).");EventUtc(row.OccurredLocal);}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Attendance.Import.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        await SchedulingRules.LockPersonAsync(w,input.PersonId,ct);
        await SnapshotTargetAsync(w,input.ProjectId,input.PersonId,(int)input.Group,input.EmploymentId,input.RosterAssignmentId,input.PersonVersion,input.RosterVersion,ct);
        foreach(var row in input.Rows.OrderBy(r=>r.ExternalId,StringComparer.Ordinal))
        {
            string key=EventText(row.ExternalId,100);DateTime utc=EventUtc(row.OccurredLocal);
            using var prior=w.Command("SELECT * FROM dbo.AttendanceEvents WITH(UPDLOCK,HOLDLOCK) WHERE SourceSystem=@source AND ExternalId=@key");Id(prior,"@project",input.ProjectId);prior.Text("@source",source,50);prior.Text("@key",key,100);var data=await prior.TableAsync(ct);
            if(data.Rows.Count>0)
            {
                var e=data.Rows[0];if(e.Field<Guid>("ProjectId")!=input.ProjectId)throw new InvalidOperationException("Kaynak olay kimliği başka kapsamda kullanılmış; başka projeye kopyalanamaz. Proje dışı ayrıntılar gösterilmez.");if(e.Field<Guid>("PersonId")!=input.PersonId||e.Field<Guid>("EmploymentId")!=input.EmploymentId||e.Field<int>("PersonnelGroup")!=(int)input.Group||e.Field<Guid>("RosterAssignmentId")!=input.RosterAssignmentId||e.Field<string>("Direction")!=row.Direction||e.Field<DateTime>("OccurredLocal")!=row.OccurredLocal||e.Field<DateTime>("OccurredUtc")!=utc)
                    throw new InvalidOperationException("Kaynak olay kimliği daha önce farklı içerikle kaydedilmiş; ham kayıt değiştirilmez. Yanlış olayı gerekçeli yoksayıp düzeltmeyi yeni kimlikle girin.");
                continue;
            }
            // An Out exactly at midnight belongs to the preceding covered interval.
            var first=row.Direction=="Out"?row.OccurredLocal.AddTicks(-1):row.OccurredLocal;
            var last=row.Direction=="Out"?row.OccurredLocal:row.OccurredLocal.AddTicks(1);
            await ValidateTargetAsync(w,input.ProjectId,input.PersonId,input.EmploymentId,(int)input.Group,input.RosterAssignmentId,first,last,ct);
            using var c=w.Command(@"INSERT dbo.AttendanceEvents(EventId,ProjectId,PersonId,EmploymentId,PersonnelGroup,RosterAssignmentId,SourceSystem,ExternalId,Direction,OccurredLocal,OccurredUtc,TimeZoneId,ImportedByAccountId,ImportedBy)
 VALUES(@id,@project,@person,@employment,@group,@roster,@source,@key,@direction,@local,@utc,@zone,@actor,@name)");
            Id(c,"@id",row.EventId);Id(c,"@project",input.ProjectId);Id(c,"@person",input.PersonId);Id(c,"@employment",input.EmploymentId);Id(c,"@roster",input.RosterAssignmentId);Id(c,"@actor",actor);
            c.Parameters.Add("@group",SqlDbType.Int).Value=(int)input.Group;c.Text("@source",source,50);c.Text("@key",key,100);c.Text("@direction",row.Direction,3);Time(c,"@local",row.OccurredLocal);Time(c,"@utc",utc);c.Text("@zone",DailyClosingService.BusinessZone.Id,100);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);
        }
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    internal async Task MatchEventsAsync(Wire.AttendanceMatch input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("attendance/match",input,ct);return;}
        if(input.MatchId==Guid.Empty||input.InEventId==input.OutEventId||input.BreakMinutes is <0 or >=1440||input.PayableMinutes is <0 or >1440)throw new ArgumentException("Eşleme kimliklerini, mola ve süreyi kontrol edin.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Attendance.Match.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        var t=await RowAsync(w,input.TimesheetId,ct)??throw new InvalidOperationException("Puantaj bulunamadı.");if(t.Field<Guid>("ProjectId")!=input.ProjectId)throw new UnauthorizedAccessException("Puantaj bu projede değil.");Version(t,input.TimesheetVersion);
        if(t.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Yalnız taslak/iade puantaj eşlenir.");
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);await EnsureNotMatchedAsync(w,input.TimesheetId,ct);await EnsureNotAllocatedAsync(w,input.TimesheetId,ct);
        var a=await EventRowAsync(w,input.ProjectId,input.InEventId,ct);var b=await EventRowAsync(w,input.ProjectId,input.OutEventId,ct);Version(a,input.InVersion);Version(b,input.OutVersion);
        foreach(var e in new[]{a,b})
            if(!e.IsNull("IgnoredUtc")||e.Field<Guid>("PersonId")!=t.Field<Guid>("PersonId")||e.Field<Guid>("EmploymentId")!=t.Field<Guid>("EmploymentId")||e.Field<int>("PersonnelGroup")!=t.Field<int>("PersonnelGroup")||e.Field<Guid>("RosterAssignmentId")!=t.Field<Guid>("RosterAssignmentId"))throw new InvalidOperationException("Olayların kişi, dönem veya ataması puantajla aynı değil ya da olay yoksayılmış.");
        if(a.Field<string>("Direction")!="In"||b.Field<string>("Direction")!="Out"||a.Field<DateTime>("OccurredUtc")>=b.Field<DateTime>("OccurredUtc"))throw new InvalidOperationException("Açık yönlü giriş → çıkış sırasını seçin; otomatik sıra tahmini yapılmaz.");
        using(var used=w.Command("SELECT COUNT(*) FROM dbo.AttendanceMatches WITH(UPDLOCK,HOLDLOCK) WHERE ReleasedUtc IS NULL AND (InEventId=@in OR OutEventId=@out)"))
        {Id(used,"@in",input.InEventId);Id(used,"@out",input.OutEventId);if(Convert.ToInt32(await used.ExecuteScalarAsync(ct))!=0)throw new InvalidOperationException("Olay başka puantaja bağlanmış; yenileyin.");}
        var start=EventMinute(a.Field<DateTime>("OccurredLocal"));var end=EventMinute(b.Field<DateTime>("OccurredLocal"));var utc=SchedulingRules.UtcRange(start,end);
        int net=checked((int)(utc.End-utc.Start).TotalMinutes)-input.BreakMinutes;
        if(start.Date!=t.Field<DateTime>("WorkDate")||utc.End-utc.Start>TimeSpan.FromHours(24)||net<=0||input.PayableMinutes>net)throw new InvalidOperationException("Olay girişi puantajın iş gününde olmalı; net süre pozitif, en çok 24 saat ve ücret dakikası neti aşmayacak şekilde olmalı.");
        await SnapshotTargetAsync(w,input.ProjectId,t.Field<Guid>("PersonId"),t.Field<int>("PersonnelGroup"),t.Field<Guid>("EmploymentId"),t.Field<Guid>("RosterAssignmentId"),input.PersonVersion,input.RosterVersion,ct);
        await ValidateTargetAsync(w,input.ProjectId,t.Field<Guid>("PersonId"),t.Field<Guid>("EmploymentId"),t.Field<int>("PersonnelGroup"),t.Field<Guid>("RosterAssignmentId"),start,end,ct);
        if(!t.IsNull("ShiftAssignmentId"))
        {using var published=w.Command("SELECT COUNT(*) FROM dbo.ShiftAssignments A WITH(HOLDLOCK) JOIN dbo.PlannedShifts S WITH(HOLDLOCK) ON S.ShiftId=A.ShiftId WHERE A.ShiftAssignmentId=@id AND A.CancelledUtc IS NULL AND S.Status=N'Published'");Id(published,"@id",t.Field<Guid>("ShiftAssignmentId"));if(Convert.ToInt32(await published.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Kaynak vardiya artık yayında değil.");}
        using(var c=w.Command("UPDATE dbo.Timesheets SET LocalStart=@localstart,LocalEnd=@localend,StartUtc=@start,EndUtc=@end,BreakMinutes=@break,PayableMinutes=@paid,EditedByAccountId=@actor,EditedBy=@name,EditedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id"))
        {
            Id(c,"@id",input.TimesheetId);Id(c,"@actor",actor);c.Text("@name",user.Username);Time(c,"@localstart",start);Time(c,"@localend",end);Time(c,"@start",utc.Start);Time(c,"@end",utc.End);c.Parameters.Add("@break",SqlDbType.Int).Value=input.BreakMinutes;c.Parameters.Add("@paid",SqlDbType.Int).Value=(object?)input.PayableMinutes??DBNull.Value;await c.ExecuteNonQueryAsync(ct);
        }
        using(var c=w.Command("INSERT dbo.AttendanceMatches(MatchId,ProjectId,TimesheetId,InEventId,OutEventId,MatchedByAccountId,MatchedBy) VALUES(@id,@project,@timesheet,@in,@out,@actor,@name)"))
        {Id(c,"@id",input.MatchId);Id(c,"@project",input.ProjectId);Id(c,"@timesheet",input.TimesheetId);Id(c,"@in",input.InEventId);Id(c,"@out",input.OutEventId);Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);}
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    internal async Task ReleaseMatchAsync(Wire.AttendanceRelease input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("attendance/release",input,ct);return;}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Attendance.Release.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        using var q=w.Command("SELECT * FROM dbo.AttendanceMatches WITH(UPDLOCK,HOLDLOCK) WHERE MatchId=@id AND ProjectId=@project");Id(q,"@id",input.MatchId);Id(q,"@project",input.ProjectId);var matches=await q.TableAsync(ct);if(matches.Rows.Count!=1)throw new InvalidOperationException("Eşleme bulunamadı.");var m=matches.Rows[0];Version(m,input.Version);if(!m.IsNull("ReleasedUtc"))throw new InvalidOperationException("Eşleme zaten kaldırılmış.");
        var t=await RowAsync(w,m.Field<Guid>("TimesheetId"),ct)??throw new InvalidOperationException("Puantaj yok.");Version(t,input.TimesheetVersion);if(t.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Önce puantajı yetkili hesapla iade edin / yeniden açın.");
        await SchedulingRules.LockPersonAsync(w,t.Field<Guid>("PersonId"),ct);await EnsureNotAllocatedAsync(w,t.Field<Guid>("TimesheetId"),ct);
        using(var c=w.Command("UPDATE dbo.AttendanceMatches SET ReleasedUtc=SYSUTCDATETIME(),ReleasedByAccountId=@actor,ReleasedBy=@name WHERE MatchId=@id")){Id(c,"@id",input.MatchId);Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);}
        // Releasing does not leave source-derived hours behind as an unlinked manual claim.
        using(var c=w.Command("UPDATE dbo.Timesheets SET LocalStart=NULL,LocalEnd=NULL,StartUtc=NULL,EndUtc=NULL,BreakMinutes=0,PayableMinutes=NULL,EditedByAccountId=@actor,EditedBy=@name,EditedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id")){Id(c,"@id",t.Field<Guid>("TimesheetId"));Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);}
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    internal async Task IgnoreEventAsync(Wire.AttendanceIgnore input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("attendance/ignore",input,ct);return;}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=EventHash("Attendance.Ignore.v1",input);
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        var e=await EventRowAsync(w,input.ProjectId,input.EventId,ct);Version(e,input.Version);if(!e.IsNull("IgnoredUtc"))throw new InvalidOperationException("Olay zaten yoksayılmış.");await SchedulingRules.LockPersonAsync(w,e.Field<Guid>("PersonId"),ct);
        using(var used=w.Command("SELECT COUNT(*) FROM dbo.AttendanceMatches WITH(UPDLOCK,HOLDLOCK) WHERE ReleasedUtc IS NULL AND (InEventId=@id OR OutEventId=@id)")){Id(used,"@id",input.EventId);if(Convert.ToInt32(await used.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Bağlı olay için önce eşlemeyi kaldırın.");}
        using var c=w.Command("UPDATE dbo.AttendanceEvents SET IgnoredUtc=SYSUTCDATETIME(),IgnoredByAccountId=@actor,IgnoredBy=@name WHERE EventId=@id");Id(c,"@id",input.EventId);Id(c,"@actor",actor);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    private static async Task DemandIndependentApproverAsync(IUnitOfWork w,Guid timesheet,Guid actor,CancellationToken ct)
    {
        using var c=w.Command("SELECT dbo.TimesheetIdentityConflict(@id,@actor)");Id(c,"@id",timesheet);Id(c,"@actor",actor);
        if(Convert.ToBoolean(await c.ExecuteScalarAsync(ct)))throw new InvalidOperationException("Kendi çalışmanızı veya aynı kişiye eşlenmiş hazırlayan/düzenleyen/gönderen/ham olay hesabının kaydını onaylayamazsınız; bağımsız yetkili hesap kullanın.");
    }
}
