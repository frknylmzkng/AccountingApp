using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService(string connectionString, UserSession user)
{
    private static void Id(SqlCommand c,string key,Guid? value) => c.Parameters.Add(key,SqlDbType.UniqueIdentifier).Value=(object?)value??DBNull.Value;
    private static void Time(SqlCommand c,string key,DateTime? value) { var p=c.Parameters.Add(key,SqlDbType.DateTime2);p.Scale=7;p.Value=(object?)value??DBNull.Value; }
    private static void Version(DataRow r,byte[]? value) { if (!((byte[])r["RecordVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(value))) throw new InvalidOperationException("Puantaj veya kaynak atama değişmiş; yenileyin."); }
    private static async Task<DataRow?> RowAsync(IUnitOfWork w,Guid id,CancellationToken ct)
    { using var c=w.Command("SELECT * FROM dbo.Timesheets WITH(UPDLOCK,HOLDLOCK) WHERE TimesheetId=@id"); Id(c,"@id",id); var d=await c.TableAsync(ct); return d.Rows.Count==1?d.Rows[0]:null; }
    private static async Task<Guid> ActorAsync(IUnitOfWork w,CancellationToken ct)
    { using var c=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))"); return await c.ExecuteScalarAsync(ct) is Guid a?a:throw new UnauthorizedAccessException("Hesap kimliği doğrulanamadı."); }
    // A permanent receipt is committed with all writes; retries return before stale-version checks.
    private static async Task<bool> ReplayAsync(IUnitOfWork w,Guid command,Guid project,Guid actor,byte[] hash,CancellationToken ct,bool allowCanceled=false)
    {
        if(command==Guid.Empty)throw new ArgumentException("İşlem kimliği gerekiyor.");
        using(var c=w.Command("SELECT ProjectId,ActorId,PayloadHash FROM dbo.TimesheetCommands WITH(UPDLOCK,HOLDLOCK) WHERE CommandId=@id"))
        {
            Id(c,"@id",command);using var r=await c.ExecuteReaderAsync(ct);
            if(await r.ReadAsync(ct))
            {if(r.GetGuid(0)!=project||r.GetGuid(1)!=actor||!r.GetFieldValue<byte[]>(2).SequenceEqual(hash))throw new InvalidOperationException("İşlem kimliği farklı içerikle kullanılmış.");return true;}
        }
        if(!allowCanceled)
        {
            using var c=w.Command("SELECT COUNT(*) FROM dbo.TimesheetCanceledCommands WITH(UPDLOCK,HOLDLOCK) WHERE CommandId=@id");Id(c,"@id",command);
            if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Bu işlem taslak kurtarmadan kalıcı olarak iptal edilmiş; gecikmiş istek uygulanmadı.");
        }
        return false;
    }
    private static async Task ReceiptAsync(IUnitOfWork w,Guid command,Guid project,Guid actor,byte[] hash,CancellationToken ct)
    {using var c=w.Command("INSERT dbo.TimesheetCommands(CommandId,ProjectId,ActorId,PayloadHash) VALUES(@id,@project,@actor,@hash)");Id(c,"@id",command);Id(c,"@project",project);Id(c,"@actor",actor);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);}
    private static DateTime? Utc(DateTime? value)
    {
        if(value==null)return null;var t=value.Value;var z=DailyClosingService.BusinessZone;
        if(t.Kind!=DateTimeKind.Unspecified||t.Ticks%TimeSpan.TicksPerMinute!=0||t.Year<1900||t.Year>2200||z.IsInvalidTime(t)||z.IsAmbiguousTime(t))throw new ArgumentException("Saatler İstanbul yerel saatiyle tam dakika olmalı; belirsiz/geçersiz saat kullanılamaz.");return TimeZoneInfo.ConvertTimeToUtc(t,z);
    }
    private static string Source(string value)
    {value=(value??string.Empty).Trim();if(value.Length is <1 or >100||value.Any(char.IsControl))throw new ArgumentException("Kaynak satır kimliği 1–100 karakter olmalı.");return value;}
    internal async Task SaveAsync(Wire.TimesheetBatch input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("timesheets/save",input,ct);return;}
        if(input.Rows==null||input.Rows.Length is <1 or >200||input.Rows.Any(r=>r==null||r.TimesheetId==Guid.Empty)||input.Rows.Select(r=>r.TimesheetId).Distinct().Count()!=input.Rows.Length)throw new ArgumentException("1–200 farklı puantaj satırı gönderin.");
        string text=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(text);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);var actor=await ActorAsync(w,ct);var hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        foreach(var r in input.Rows.OrderBy(r=>r.ShiftAssignmentId))await SaveRowAsync(w,input.ProjectId,actor,r,ct);
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    private async Task SaveRowAsync(IUnitOfWork w,Guid project,Guid actor,Wire.TimesheetWrite input,CancellationToken ct)
    {
        string source=Source(input.SourceKey);DateTime? start=Utc(input.Start),end=Utc(input.End);
        if(input.BreakMinutes is <0 or >=1440||input.PayableMinutes is <0 or >1440)throw new ArgumentException("Mola / ücretlendirilecek süre 0–1440 dakika aralığında olmalı.");
        var old=await RowAsync(w,input.TimesheetId,ct);
        if(old!=null){if(old.IsNull("ShiftAssignmentId")||old.Field<Guid>("ProjectId")!=project||old.Field<Guid>("ShiftAssignmentId")!=input.ShiftAssignmentId||old.Field<string>("SourceKey")!=source)throw new InvalidOperationException("Puantaj kaynak kimliği değiştirilemez.");if(input.Version==null){if(old.Field<DateTime?>("LocalStart")==input.Start&&old.Field<DateTime?>("LocalEnd")==input.End&&old.Field<int>("BreakMinutes")==input.BreakMinutes&&old.Field<int?>("PayableMinutes")==input.PayableMinutes)return;throw new InvalidOperationException("Bu kaynak satırı daha önce farklı içerikle kaydedilmiş; puantaj listesinden sürümlü düzenleyin.");}Version(old,input.Version);await EnsureNotMatchedAsync(w,input.TimesheetId,ct);await EnsureNotAllocatedAsync(w,input.TimesheetId,ct);if(old.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Yalnız taslak / iade edilmiş puantaj düzenlenir.");}
        else if(input.Version!=null)throw new InvalidOperationException("Puantaj bulunamadı; listeyi yenileyin.");
        if(old==null){using var unique=w.Command("SELECT COUNT(*) FROM dbo.Timesheets WITH(UPDLOCK,HOLDLOCK) WHERE ShiftAssignmentId=@assignment OR (ProjectId=@project AND SourceKey=@source)");Id(unique,"@assignment",input.ShiftAssignmentId);Id(unique,"@project",project);unique.Text("@source",source,100);if(Convert.ToInt32(await unique.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Bu vardiya ataması / kaynak satırı daha önce kaydedilmiş. İptal dahil geçmiş korunur; listeden mevcut kaydı açın.");}

        using var q=w.Command(@"SELECT A.*,S.ProjectId,S.LocalStart PlannedStart,S.LocalEnd PlannedEnd,S.RecordVersion ShiftVersion,S.Status ShiftStatus FROM dbo.ShiftAssignments A WITH(UPDLOCK,HOLDLOCK) JOIN dbo.PlannedShifts S WITH(UPDLOCK,HOLDLOCK) ON S.ShiftId=A.ShiftId WHERE A.ShiftAssignmentId=@id AND S.ProjectId=@project");Id(q,"@id",input.ShiftAssignmentId);Id(q,"@project",project);var d=await q.TableAsync(ct);if(d.Rows.Count!=1)throw new InvalidOperationException("Bu projede vardiya ataması yok.");var a=d.Rows[0];Version(a,input.AssignmentVersion);if(!((byte[])a["ShiftVersion"]).SequenceEqual(PersonnelConcurrency.RequireVersion(input.ShiftVersion))||a.Field<string>("ShiftStatus")!="Published"||!a.IsNull("CancelledUtc"))throw new InvalidOperationException("Vardiya değişmiş, yayında değil veya atama iptal edilmiş.");
        await SchedulingRules.LockPersonAsync(w,a.Field<Guid>("PersonId"),ct);
        var first=input.Start??a.Field<DateTime>("PlannedStart");var last=input.End??a.Field<DateTime>("PlannedEnd");if(last<=first)last=first.AddMinutes(1);
        await ValidateTargetAsync(w,project,a.Field<Guid>("PersonId"),a.Field<Guid>("EmploymentId"),a.Field<int>("PersonnelGroup"),a.Field<Guid>("RosterAssignmentId"),first,last,ct);
        using var c=w.Command(old==null?@"INSERT dbo.Timesheets(TimesheetId,ProjectId,ShiftAssignmentId,PersonId,EmploymentId,PersonnelGroup,RosterAssignmentId,WorkDate,LocalStart,LocalEnd,StartUtc,EndUtc,TimeZoneId,BreakMinutes,PayableMinutes,SourceKey,CreatedByAccountId,CreatedBy,EditedByAccountId,EditedBy) VALUES(@id,@project,@assignment,@person,@employment,@group,@roster,@day,@localstart,@localend,@start,@end,@zone,@break,@paid,@source,@actor,@name,@actor,@name)":@"UPDATE dbo.Timesheets SET LocalStart=@localstart,LocalEnd=@localend,StartUtc=@start,EndUtc=@end,BreakMinutes=@break,PayableMinutes=@paid,EditedByAccountId=@actor,EditedBy=@name,EditedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id");
        Id(c,"@id",input.TimesheetId);Id(c,"@project",project);Id(c,"@assignment",input.ShiftAssignmentId);Id(c,"@person",a.Field<Guid>("PersonId"));Id(c,"@employment",a.Field<Guid>("EmploymentId"));Id(c,"@roster",a.Field<Guid>("RosterAssignmentId"));Id(c,"@actor",actor);c.Text("@name",user.Username);c.Parameters.Add("@group",SqlDbType.Int).Value=a.Field<int>("PersonnelGroup");c.Parameters.Add("@day",SqlDbType.Date).Value=a.Field<DateTime>("PlannedStart").Date;
        Time(c,"@localstart",input.Start);Time(c,"@localend",input.End);Time(c,"@start",start);Time(c,"@end",end);c.Text("@zone",DailyClosingService.BusinessZone.Id,100);c.Text("@source",source,100);c.Parameters.Add("@break",SqlDbType.Int).Value=input.BreakMinutes;c.Parameters.Add("@paid",SqlDbType.Int).Value=(object?)input.PayableMinutes??DBNull.Value;await c.ExecuteNonQueryAsync(ct);
    }
    private static async Task ValidateTargetAsync(IUnitOfWork w,Guid project,Guid person,Guid employment,int group,Guid roster,DateTime first,DateTime last,CancellationToken ct)
    {
        using var c=w.Command($@"SELECT COUNT(*) FROM {PersonnelRepository.Table((PersonnelGroup)group)} C WITH(UPDLOCK,HOLDLOCK)
 JOIN dbo.EmploymentPeriods E WITH(HOLDLOCK) ON E.EmploymentId=C.CurrentEmploymentId
 JOIN dbo.PersonnelProjectAssignments A WITH(HOLDLOCK) ON A.AssignmentId=@roster AND A.PersonId=C.PersonId AND A.PersonnelGroup=@group
 JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) AND L.EmploymentId=@employment
 JOIN dbo.FinanceProjects P WITH(HOLDLOCK) ON P.ProjectId=A.ProjectId
 WHERE C.PersonId=@person AND C.CurrentEmploymentId=@employment AND C.ArchivedUtc IS NULL AND E.IsClosed=0
 AND (E.StartDate IS NULL OR E.StartDate<=@first) AND (E.EndDate IS NULL OR E.EndDate>=@last)
 AND A.ProjectId=@project AND A.CancelledUtc IS NULL AND A.StartDate<=@first AND (A.EndDate IS NULL OR A.EndDate>=@last)
 AND P.IsActive=1 AND (P.StartDate IS NULL OR P.StartDate<=@first) AND (P.EndDate IS NULL OR P.EndDate>=@last)
 AND (A.TeamId IS NULL OR EXISTS(SELECT 1 FROM dbo.ProjectTeams T WHERE T.TeamId=A.TeamId AND T.ProjectId=P.ProjectId AND T.IsActive=1))");
        Id(c,"@person",person);Id(c,"@employment",employment);Id(c,"@roster",roster);Id(c,"@project",project);c.Parameters.Add("@group",SqlDbType.Int).Value=group;c.Parameters.Add("@first",SqlDbType.Date).Value=first.Date;c.Parameters.Add("@last",SqlDbType.Date).Value=last.AddTicks(-1).Date;
        if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Puantaj kişinin güncel dönemine ve tüm saat aralığını kapsayan etkin proje/ekip atamasına ait olmalı.");
    }
    internal async Task StateAsync(Wire.TimesheetState input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("timesheets/state",input,ct);return;}
        if(input.State is not ("Submitted" or "Approved" or "Returned" or "Cancelled"))throw new ArgumentException("Geçersiz puantaj durumu.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AllocationGateAsync(w,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,input.State is "Approved" or "Returned"?ProjectAccess.ApproveTimesheets:ProjectAccess.EnterTimesheets,ct);
        var actor=await ActorAsync(w,ct);var hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        var row=await RowAsync(w,input.TimesheetId,ct)??throw new InvalidOperationException("Puantaj bulunamadı.");if(row.Field<Guid>("ProjectId")!=input.ProjectId)throw new UnauthorizedAccessException("Puantaj bu projede değil.");Version(row,input.Version);string state=row.Field<string>("Status")!;
        bool valid=input.State switch{"Submitted"=>state is "Draft" or "Returned","Approved"=>state=="Submitted","Returned"=>state is "Submitted" or "Approved","Cancelled"=>state is "Draft" or "Returned",_=>false};if(!valid)throw new InvalidOperationException("Bu puantaj artık istenen duruma geçirilemez; yenileyin.");
        if(input.State is "Approved" or "Returned" && await HasAllocationAsync(w,input.TimesheetId,ct))throw new InvalidOperationException("Dağıtılmış puantajın onay/iadesi Projeye ait zaman parçaları sekmesinden, her proje için ayrı verilir.");
        if(input.State=="Returned")await DemandNotAccruedAsync(w,input.TimesheetId,ct);
        await DemandAllocationStateAsync(w,row,input.State,ct);
        await SchedulingRules.LockPersonAsync(w,row.Field<Guid>("PersonId"),ct);
        if(input.State=="Cancelled"){await EnsureNotMatchedAsync(w,input.TimesheetId,ct);await EnsureNotAllocatedAsync(w,input.TimesheetId,ct);}
        if(input.State is "Submitted" or "Approved")
        {
            var fault=await FaultAsync(w,row,ct);if(fault.Length>0)throw new InvalidOperationException(fault);
            await ValidateTargetAsync(w,input.ProjectId,row.Field<Guid>("PersonId"),row.Field<Guid>("EmploymentId"),row.Field<int>("PersonnelGroup"),row.Field<Guid>("RosterAssignmentId"),row.Field<DateTime>("LocalStart"),row.Field<DateTime>("LocalEnd"),ct);
            if(input.State=="Approved")await DemandIndependentApproverAsync(w,input.TimesheetId,actor,ct);
            if(input.State=="Approved"&&(actor==row.Field<Guid>("CreatedByAccountId")||actor==row.Field<Guid>("EditedByAccountId")||actor==row.Field<Guid?>("SubmittedByAccountId")))throw new InvalidOperationException("Puantajı hazırlayan, son düzenleyen veya gönderen hesap kendi kaydını onaylayamaz; başka yetkili hesap kullanın.");
        }
        if(input.State=="Submitted")await BeginSegmentRoundAsync(w,row,actor,user.Username,ct);
        using var c=w.Command(input.State=="Submitted"?"UPDATE dbo.Timesheets SET Status=@state,SubmittedByAccountId=@actor,SubmittedUtc=SYSUTCDATETIME(),DecidedByAccountId=NULL,DecidedBy=NULL,DecidedUtc=NULL WHERE TimesheetId=@id":input.State=="Cancelled"?"UPDATE dbo.Timesheets SET Status=@state WHERE TimesheetId=@id":"UPDATE dbo.Timesheets SET Status=@state,DecidedByAccountId=@actor,DecidedBy=@name,DecidedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id");Id(c,"@id",input.TimesheetId);Id(c,"@actor",actor);c.Text("@state",input.State,20);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    private static async Task DemandNotAccruedAsync(IUnitOfWork w,Guid timesheet,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.AccrualCalculationLines WHERE TimesheetId=@id");Id(c,"@id",timesheet);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Bu puantaj dönem hesabında kullanılmış. Kaynak/onay değiştirilemez; bağlantılı mali düzeltme gerekir.");}
    private static async Task<string> FaultAsync(IUnitOfWork w,DataRow row,CancellationToken ct)
    {
        if(row.IsNull("StartUtc")||row.IsNull("EndUtc"))return "Giriş veya çıkış eksik; düzeltip yeniden gönderin.";
        var start=row.Field<DateTime>("StartUtc");var end=row.Field<DateTime>("EndUtc");int minutes=(int)(end-start).TotalMinutes-row.Field<int>("BreakMinutes");
        if(end<=start||(end-start).TotalMinutes>1440||minutes<=0)return "Süre ters, 24 saatten uzun veya mola net süreyi tüketiyor.";
        if(row.IsNull("PayableMinutes")||row.Field<int>("PayableMinutes")>minutes)return "Ücretlendirilecek dakika açıkça girilmeli ve gerçekleşen net süreyi aşmamalı.";
        using var c=w.Command(@"SELECT
 (SELECT COUNT(*) FROM dbo.Timesheets T WITH(HOLDLOCK) WHERE T.PersonId=@person AND T.TimesheetId<>@id AND T.Status IN(N'Submitted',N'Approved') AND T.StartUtc<@end AND T.EndUtc>@start)
 +(SELECT COUNT(*) FROM dbo.LeaveRequests L WITH(HOLDLOCK) WHERE L.PersonId=@person AND L.Status=N'Approved' AND L.StartUtc<@end AND L.EndUtc>@start)
 +(SELECT COUNT(*) FROM dbo.izinler I WITH(HOLDLOCK) WHERE I.PersonId=@person AND I.IZINBASLANGIC<=@last AND I.IZINBITIS>=@first)
 +(CASE WHEN @assignment IS NULL THEN 0 WHEN EXISTS(SELECT 1 FROM dbo.ShiftAssignments A JOIN dbo.PlannedShifts S ON S.ShiftId=A.ShiftId WHERE A.ShiftAssignmentId=@assignment AND S.Status=N'Published' AND A.CancelledUtc IS NULL) THEN 0 ELSE 1 END)
 +(SELECT COUNT(*) FROM dbo.ShiftAssignments A WITH(HOLDLOCK) JOIN dbo.PlannedShifts S WITH(HOLDLOCK) ON S.ShiftId=A.ShiftId WHERE @assignment IS NULL AND A.PersonId=@person AND A.CancelledUtc IS NULL AND S.Status<>N'Cancelled' AND S.StartUtc<@end AND S.EndUtc>@start)");Id(c,"@person",row.Field<Guid>("PersonId"));Id(c,"@id",row.Field<Guid>("TimesheetId"));Id(c,"@assignment",row.Field<Guid?>("ShiftAssignmentId"));Time(c,"@start",start);Time(c,"@end",end);c.Parameters.Add("@first",SqlDbType.Date).Value=row.Field<DateTime>("LocalStart").Date;c.Parameters.Add("@last",SqlDbType.Date).Value=row.Field<DateTime>("LocalEnd").AddTicks(-1).Date;
        return Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0?"Gönderilmiş/onaylı çalışma, izin veya vardiyasız kayıt için planlı vardiya çakışması var; kaynak vardiya da artık yayında olmayabilir. Proje dışı ayrıntılar gösterilmez.":"";
    }
}
