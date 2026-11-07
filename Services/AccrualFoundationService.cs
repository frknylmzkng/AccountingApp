using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class AccrualFoundationService(string connectionString,UserSession user)
{
    internal const string Policy="HourlyTRY.WorkDate.PerLineAway.v1";
    private static void Id(SqlCommand c,string name,Guid? value)=>c.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=(object?)value??DBNull.Value;
    private static void Day(SqlCommand c,string name,DateTime? value)=>c.Parameters.Add(name,SqlDbType.Date).Value=(object?)EmploymentService.Day(value)??DBNull.Value;
    private static void Range(DateTime from,DateTime to){EmploymentService.Day(from);EmploymentService.Day(to);if(from.Year<1900||to.Year>2199||to<from||(to-from).Days>365)throw new ArgumentException("Dönem 1900–2199 aralığında ve en çok 366 gün olmalı.");}
    private async Task AuthorizeAsync(IUnitOfWork w,Guid project,bool write,CancellationToken ct)
    {
        await TimesheetService.AllocationGateAsync(w,ct);
        using(var gate=w.Command("DECLARE @r int;EXEC @r=sys.sp_getapplock @Resource=N'Accrual.Foundation.v1',@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;IF @r<0 THROW 51234,N'Hakediş hesap kilidi alınamadı.',1;"))await gate.ExecuteNonQueryAsync(ct);
        await AccountAuthorization.DemandAsync(w,user,write?AccountAccess.Admin:AccountAccess.ViewSalary,ct);
        await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Costs,ct);
    }
    private static async Task<Guid> ActorAsync(IUnitOfWork w,CancellationToken ct){using var c=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");return (Guid)(await c.ExecuteScalarAsync(ct)??throw new SessionRevokedException());}
    internal async Task<DataTable> TargetsAsync(Guid project,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"accrual/{project:D}/targets",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,true,ct);
        using var c=w.Command(@"SELECT DISTINCT TOP(2001) E.EmploymentId,E.PersonId,E.RecordVersion EmploymentVersion,E.StartDate,E.EndDate,E.IsClosed,E.PersonnelGroup,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD FROM dbo.EmploymentPeriods E
 LEFT JOIN dbo.Calisan C ON C.PersonId=E.PersonId LEFT JOIN dbo.Joker J ON J.PersonId=E.PersonId
 WHERE EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE A.ProjectId=@project AND L.EmploymentId=E.EmploymentId) ORDER BY E.PersonId,E.StartDate,E.EmploymentId");Id(c,"@project",project);var rows=await c.TableAsync(ct);if(rows.Rows.Count>2000)throw new InvalidOperationException("Tarife çalışma dönemi seçimi 2000 sınırını aşıyor.");await w.CommitAsync(ct);return rows;
    }
    internal async Task<DataTable> RatesAsync(Guid project,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"accrual/{project:D}/rates",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);
        using var c=w.Command("SELECT TOP(1000) R.*,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,E.StartDate EmploymentStart,E.EndDate EmploymentEnd FROM dbo.HourlyRateAgreements R JOIN dbo.EmploymentPeriods E ON E.EmploymentId=R.EmploymentId LEFT JOIN dbo.Calisan C ON C.PersonId=R.PersonId LEFT JOIN dbo.Joker J ON J.PersonId=R.PersonId WHERE R.ProjectId=@project ORDER BY R.CreatedUtc DESC,R.RateId");Id(c,"@project",project);var rows=await c.TableAsync(ct);await w.CommitAsync(ct);return rows;
    }
    internal async Task CreateRateAsync(Wire.HourlyRateCreate input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("accrual/rate/create",input,ct);return;}
        EmploymentService.Day(input.From);EmploymentService.Day(input.To);
        if(input.RateId==Guid.Empty||input.PersonId==Guid.Empty||input.EmploymentId==Guid.Empty||input.From.Year<1900||input.From.Year>2199||input.To?.Year>2199||input.To<input.From||input.HourlyAmount<=0||input.HourlyAmount>1000000||decimal.Round(input.HourlyAmount,2)!=input.HourlyAmount)throw new ArgumentException("Geçerli tarih aralığı ve 0–1.000.000 arasında pozitif, iki ondalıklı saatlik TL tutarı gerekiyor.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);
        using(var find=w.Command("SELECT * FROM dbo.HourlyRateAgreements WITH(UPDLOCK,HOLDLOCK) WHERE RateId=@id"))
        {
            Id(find,"@id",input.RateId);var rows=await find.TableAsync(ct);if(rows.Rows.Count>0){var r=rows.Rows[0];if(r.Field<Guid>("ProjectId")!=input.ProjectId||r.Field<Guid>("PersonId")!=input.PersonId||r.Field<Guid>("EmploymentId")!=input.EmploymentId||r.Field<DateTime>("ValidFrom")!=input.From||r.Field<DateTime?>("ValidTo")!=input.To||r.Field<decimal>("HourlyAmount")!=input.HourlyAmount||r.Field<Guid>("CreatedByAccountId")!=actor)throw new InvalidOperationException("Tarife işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}
        }
        using(var target=w.Command(@"SELECT E.* FROM dbo.EmploymentPeriods E WITH(UPDLOCK,HOLDLOCK) WHERE E.EmploymentId=@employment AND E.PersonId=@person AND E.RecordVersion=@version AND EXISTS(SELECT 1 FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) WHERE A.ProjectId=@project AND L.EmploymentId=E.EmploymentId)"))
        {
            Id(target,"@employment",input.EmploymentId);Id(target,"@person",input.PersonId);Id(target,"@project",input.ProjectId);target.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(input.EmploymentVersion);var rows=await target.TableAsync(ct);if(rows.Rows.Count!=1)throw new InvalidOperationException("Çalışma dönemi seçimi değişmiş; yeniden seçin.");var e=rows.Rows[0];if((!e.IsNull("StartDate")&&input.From<e.Field<DateTime>("StartDate"))||(!e.IsNull("EndDate")&&(input.To==null||input.To>e.Field<DateTime>("EndDate"))))throw new InvalidOperationException("Tarife çalışma döneminin bilinen tarihleri dışında olamaz.");
        }
        using(var overlap=w.Command("SELECT COUNT(*) FROM dbo.HourlyRateAgreements WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND EmploymentId=@employment AND RevokedUtc IS NULL AND ValidFrom<=COALESCE(@last,CONVERT(date,'99991231')) AND (ValidTo IS NULL OR ValidTo>=@first)"))
        {Id(overlap,"@project",input.ProjectId);Id(overlap,"@employment",input.EmploymentId);Day(overlap,"@first",input.From);Day(overlap,"@last",input.To);if(Convert.ToInt32(await overlap.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Aynı proje/çalışma döneminde etkin tarife tarihleri çakışamaz; mevcut tarifeyi ve tarihleri kontrol edin.");}
        using var c=w.Command("INSERT dbo.HourlyRateAgreements(RateId,ProjectId,PersonId,EmploymentId,ValidFrom,ValidTo,HourlyAmount,CreatedByAccountId,CreatedBy) VALUES(@id,@project,@person,@employment,@first,@last,@amount,@actor,@name)");Id(c,"@id",input.RateId);Id(c,"@project",input.ProjectId);Id(c,"@person",input.PersonId);Id(c,"@employment",input.EmploymentId);Id(c,"@actor",actor);Day(c,"@first",input.From);Day(c,"@last",input.To);c.Money("@amount",input.HourlyAmount);c.Text("@name",user.Username);await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    internal async Task RevokeRateAsync(Wire.HourlyRateRevoke input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("accrual/rate/revoke",input,ct);return;}
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);
        using var c=w.Command("UPDATE dbo.HourlyRateAgreements SET RevokedUtc=SYSUTCDATETIME(),RevokedByAccountId=TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),RevokedBy=@name WHERE RateId=@id AND ProjectId=@project AND RecordVersion=@version AND RevokedUtc IS NULL");Id(c,"@id",input.RateId);Id(c,"@project",input.ProjectId);c.Text("@name",user.Username);c.Parameters.Add("@version",SqlDbType.Binary,8).Value=PersonnelConcurrency.RequireVersion(input.Version);if(await c.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Tarife değişmiş/kaldırılmış; güncel listeyi kontrol edin. Geçmiş hesap değişmez.");await w.CommitAsync(ct);
    }
}
