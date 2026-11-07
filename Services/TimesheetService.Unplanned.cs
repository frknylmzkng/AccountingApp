using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire = AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class TimesheetService
{
    internal async Task<DataTable> RostersAsync(Guid project,Guid person,Wire.PersonnelGroup group,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))
            return await RemoteApi.For(connectionString,user).TableAsync($"timesheets/{project:D}/rosters/{person:D}?group={group}",ct);
        if(!Enum.IsDefined(group)||person==Guid.Empty)throw new ArgumentException("Geçerli kişi/grup seçin.");
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.EnterTimesheets,ct);
        using var c=w.Command($@"SELECT TOP(501) A.AssignmentId RosterAssignmentId,A.RecordVersion RosterVersion,A.StartDate,A.EndDate,A.TeamId,T.Name TeamName,
 C.PersonId,C.RecordVersion PersonVersion,C.CurrentEmploymentId EmploymentId,A.PersonnelGroup
 FROM dbo.PersonnelProjectAssignments A WITH(HOLDLOCK)
 JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId)
 JOIN {PersonnelRepository.Table((PersonnelGroup)group)} C WITH(HOLDLOCK) ON C.PersonId=A.PersonId AND C.CurrentEmploymentId=L.EmploymentId AND C.ArchivedUtc IS NULL
 JOIN dbo.EmploymentPeriods E ON E.EmploymentId=C.CurrentEmploymentId AND E.IsClosed=0
 JOIN dbo.FinanceProjects P ON P.ProjectId=A.ProjectId AND P.IsActive=1
 LEFT JOIN dbo.ProjectTeams T ON T.TeamId=A.TeamId AND T.ProjectId=A.ProjectId
 WHERE A.ProjectId=@project AND A.PersonId=@person AND A.PersonnelGroup=@group AND A.CancelledUtc IS NULL AND (A.TeamId IS NULL OR T.IsActive=1)
 ORDER BY A.StartDate DESC,A.AssignmentId");
        Id(c,"@project",project);Id(c,"@person",person);c.Parameters.Add("@group",SqlDbType.Int).Value=(int)group;
        var rows=await c.TableAsync(ct);if(rows.Rows.Count>500)throw new InvalidOperationException("Personelin proje atamaları 500 sınırını aşıyor.");
        await w.CommitAsync(ct);return rows;
    }

    internal async Task SaveUnplannedAsync(Wire.UnplannedTimesheetWrite input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("timesheets/unplanned",input,ct);return;}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);
        await ProjectAuthorization.DemandAsync(w,user,input.ProjectId,ProjectAccess.EnterTimesheets,ct);
        var actor=await ActorAsync(w,ct);var hash=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));
        if(await ReplayAsync(w,input.CommandId,input.ProjectId,actor,hash,ct)){await w.CommitAsync(ct);return;}
        await SaveUnplannedRowAsync(w,input,actor,ct);
        await ReceiptAsync(w,input.CommandId,input.ProjectId,actor,hash,ct);await w.CommitAsync(ct);
    }
    private async Task SaveUnplannedRowAsync(IUnitOfWork w,Wire.UnplannedTimesheetWrite input,Guid actor,CancellationToken ct)
    {
        if(!Enum.IsDefined(input.Group)||input.PersonId==Guid.Empty||input.EmploymentId==Guid.Empty||input.RosterAssignmentId==Guid.Empty||input.TimesheetId==Guid.Empty)
            throw new ArgumentException("Geçerli kişi, dönem, proje ataması ve puantaj kimliği gerekiyor.");
        EmploymentService.Day(input.WorkDate);
        if(input.WorkDate.Year is <1900 or >2199)throw new ArgumentException("İş günü 1900–2199 aralığında olmalı.");
        string source=Source(input.SourceKey);DateTime? start=Utc(input.Start),end=Utc(input.End);
        if(input.Start.HasValue&&input.Start.Value.Date!=input.WorkDate)throw new ArgumentException("Vardiyasız kaydın iş günü gerçek girişin günü olmalı. İş günü değişmez; farklı gün gerekiyorsa kaydı iptal edip yeni kayıt açın.");
        if(input.End.HasValue&&input.End<=input.WorkDate)throw new ArgumentException("Çıkış iş gününün başlangıcından sonra olmalı.");
        if(input.BreakMinutes is <0 or >=1440||input.PayableMinutes is <0 or >1440)throw new ArgumentException("Mola / ücretlendirilecek dakika sınırlarını kontrol edin.");
        var old=await RowAsync(w,input.TimesheetId,ct);
        if(old!=null)
        {
            if(!old.IsNull("ShiftAssignmentId")||old.Field<Guid>("ProjectId")!=input.ProjectId||old.Field<Guid>("PersonId")!=input.PersonId||old.Field<Guid>("EmploymentId")!=input.EmploymentId||old.Field<int>("PersonnelGroup")!=(int)input.Group||old.Field<Guid>("RosterAssignmentId")!=input.RosterAssignmentId||old.Field<DateTime>("WorkDate")!=input.WorkDate||old.Field<string>("SourceKey")!=source)
                throw new InvalidOperationException("Puantajın kişi, dönem, kaynak ve iş günü kimliği değiştirilemez.");
            if(input.Version==null)
            {
                if(old.Field<DateTime?>("LocalStart")!=input.Start||old.Field<DateTime?>("LocalEnd")!=input.End||old.Field<int>("BreakMinutes")!=input.BreakMinutes||old.Field<int?>("PayableMinutes")!=input.PayableMinutes)
                    throw new InvalidOperationException("Kaynak kimliği farklı içerikle kaydedilmiş; listeden sürümlü düzenleyin.");
                return;
            }
            Version(old,input.Version);
            await EnsureNotMatchedAsync(w,input.TimesheetId,ct);await EnsureNotAllocatedAsync(w,input.TimesheetId,ct);
            if(old.Field<string>("Status") is not ("Draft" or "Returned"))throw new InvalidOperationException("Yalnız taslak / iade edilmiş puantaj düzenlenir.");
        }
        else if(input.Version!=null)throw new InvalidOperationException("Puantaj bulunamadı; listeyi yenileyin.");
        await SchedulingRules.LockPersonAsync(w,input.PersonId,ct);
        using(var card=w.Command($"SELECT COUNT(*) FROM {PersonnelRepository.Table((PersonnelGroup)input.Group)} WITH(UPDLOCK,HOLDLOCK) WHERE PersonId=@person AND CurrentEmploymentId=@employment AND RecordVersion=@version AND ArchivedUtc IS NULL"))
        {
            Id(card,"@person",input.PersonId);Id(card,"@employment",input.EmploymentId);card.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(input.PersonVersion);
            if(Convert.ToInt32(await card.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException(PersonnelConcurrency.ConflictMessage);
        }
        using(var roster=w.Command("SELECT COUNT(*) FROM dbo.PersonnelProjectAssignments WITH(UPDLOCK,HOLDLOCK) WHERE AssignmentId=@id AND ProjectId=@project AND PersonId=@person AND RecordVersion=@version AND CancelledUtc IS NULL"))
        {
            Id(roster,"@id",input.RosterAssignmentId);Id(roster,"@project",input.ProjectId);Id(roster,"@person",input.PersonId);roster.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(input.RosterVersion);
            if(Convert.ToInt32(await roster.ExecuteScalarAsync(ct))!=1)throw new InvalidOperationException("Proje ataması seçimden sonra değişmiş; yenileyin.");
        }
        var first=input.Start??input.WorkDate;var last=input.End??first.AddMinutes(1);if(last<=first)last=first.AddMinutes(1);
        await ValidateTargetAsync(w,input.ProjectId,input.PersonId,input.EmploymentId,(int)input.Group,input.RosterAssignmentId,first,last,ct);
        if(old==null)
        {
            using var duplicate=w.Command("SELECT COUNT(*) FROM dbo.Timesheets WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND SourceKey=@source");
            Id(duplicate,"@project",input.ProjectId);duplicate.Text("@source",source,100);
            if(Convert.ToInt32(await duplicate.ExecuteScalarAsync(ct))!=0)throw new InvalidOperationException("Kaynak kimliği daha önce kullanılmış; mevcut kaydı açın.");
        }
        using var c=w.Command(old==null?@"INSERT dbo.Timesheets(TimesheetId,ProjectId,ShiftAssignmentId,PersonId,EmploymentId,PersonnelGroup,RosterAssignmentId,WorkDate,LocalStart,LocalEnd,StartUtc,EndUtc,TimeZoneId,BreakMinutes,PayableMinutes,SourceKey,CreatedByAccountId,CreatedBy,EditedByAccountId,EditedBy)
 VALUES(@id,@project,NULL,@person,@employment,@group,@roster,@day,@localstart,@localend,@start,@end,@zone,@break,@paid,@source,@actor,@name,@actor,@name)":@"UPDATE dbo.Timesheets SET LocalStart=@localstart,LocalEnd=@localend,StartUtc=@start,EndUtc=@end,BreakMinutes=@break,PayableMinutes=@paid,EditedByAccountId=@actor,EditedBy=@name,EditedUtc=SYSUTCDATETIME() WHERE TimesheetId=@id");
        Id(c,"@id",input.TimesheetId);Id(c,"@project",input.ProjectId);Id(c,"@person",input.PersonId);Id(c,"@employment",input.EmploymentId);Id(c,"@roster",input.RosterAssignmentId);Id(c,"@actor",actor);
        c.Parameters.Add("@group",SqlDbType.Int).Value=(int)input.Group;c.Parameters.Add("@day",SqlDbType.Date).Value=input.WorkDate;
        Time(c,"@localstart",input.Start);Time(c,"@localend",input.End);Time(c,"@start",start);Time(c,"@end",end);
        c.Text("@zone",DailyClosingService.BusinessZone.Id,100);c.Text("@source",source,100);c.Text("@name",user.Username);
        c.Parameters.Add("@break",SqlDbType.Int).Value=input.BreakMinutes;c.Parameters.Add("@paid",SqlDbType.Int).Value=(object?)input.PayableMinutes??DBNull.Value;
        await c.ExecuteNonQueryAsync(ct);
    }
}
