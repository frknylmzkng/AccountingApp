using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed partial class AccrualFoundationService
{
    // Supplemental calculations share the original immutable period, but consume only unused sources.
    private static async Task<DataRow> SupplementalBaseAsync(IUnitOfWork w,Guid project,Guid parent,DateTime from,DateTime to,CancellationToken ct)
    {
        using var c=w.Command(@"SELECT C.* FROM dbo.AccrualCalculations C WITH(UPDLOCK,HOLDLOCK)
 WHERE C.CalculationId=@id AND C.ProjectId=@project AND C.ParentCalculationId IS NULL AND C.IsSealed=1 AND C.PeriodStart=@first AND C.PeriodEnd=@last
 AND EXISTS(SELECT 1 FROM dbo.AccrualPostings P WHERE P.CalculationId=C.CalculationId AND P.IsSealed=1)
 AND EXISTS(SELECT 1 FROM dbo.FinanceProjects F WHERE F.ProjectId=C.ProjectId AND F.IsActive=1)");
        Id(c,"@id",parent);Id(c,"@project",project);Day(c,"@first",from);Day(c,"@last",to);var rows=await c.TableAsync(ct);
        return rows.Rows.Count==1?rows.Rows[0]:throw new InvalidOperationException("Ek hesap için aynı projede deftere aktarılmış asıl hesap ve onun değişmeyen tarih aralığı gerekiyor. Proje etkin olmalı.");
    }
    private sealed record Preview(DataTable Lines,string Fingerprint,string[] Issues);
    private static async Task<Preview> BuildAsync(IUnitOfWork w,Guid project,DateTime from,DateTime to,CancellationToken ct,Guid? parent=null)
    {
        DataRow? baseCalculation=parent.HasValue?await SupplementalBaseAsync(w,project,parent.Value,from,to,ct):null;
        using var c=w.Command(@"WITH Sources AS(
 SELECT N'Whole' SourceKind,T.TimesheetId SourceId,T.TimesheetId,T.PersonId,T.EmploymentId,T.WorkDate,T.PayableMinutes PaidMinutes,T.RecordVersion ParentVersion,CONVERT(binary(8),NULL) SetVersion,CONVERT(uniqueidentifier,NULL) ApprovalId,CONVERT(binary(8),NULL) ApprovalVersion
 FROM dbo.Timesheets T WITH(HOLDLOCK) WHERE T.ProjectId=@project AND T.Status=N'Approved' AND T.WorkDate BETWEEN @first AND @last AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL)
 UNION ALL SELECT N'Segment',S.SegmentId,T.TimesheetId,T.PersonId,T.EmploymentId,T.WorkDate,S.PayableMinutes,T.RecordVersion,H.RecordVersion,D.ApprovalId,D.RecordVersion
 FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId AND H.IsSealed=1 AND H.ReleasedUtc IS NULL JOIN dbo.Timesheets T WITH(HOLDLOCK) ON T.TimesheetId=H.TimesheetId
 LEFT JOIN dbo.TimesheetApprovalRounds Q ON Q.SetId=H.SetId AND Q.IsReady=1 AND Q.ClosedUtc IS NULL LEFT JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=Q.RoundId AND D.SegmentId=S.SegmentId
 WHERE S.ProjectId=@project AND T.Status=N'Approved' AND T.WorkDate BETWEEN @first AND @last AND (H.UsesSegmentApproval=0 OR D.State=N'Approved'))
 SELECT TOP(2001) S.*,COALESCE(C.AD,J.AD) AD,COALESCE(C.SOYAD,J.SOYAD) SOYAD,R.RateId,R.RecordVersion RateVersion,R.HourlyAmount,
 CAST(CASE WHEN U.LineId IS NULL THEN 0 ELSE 1 END AS bit) AlreadyCalculated,
 CAST(CASE WHEN EXISTS(SELECT 1 FROM dbo.WageClaims W WHERE W.EmploymentId=S.EmploymentId AND W.IsReserved=1 AND W.Kind IN(N'DailyWage',N'MonthlyWage') AND S.WorkDate BETWEEN W.FromDay AND W.ToDay) THEN 1 ELSE 0 END AS bit) BaseWageReserved,
 (SELECT COUNT(*) FROM dbo.HourlyRateAgreements A WHERE A.ProjectId=@project AND A.EmploymentId=S.EmploymentId AND A.PersonId=S.PersonId AND A.RevokedUtc IS NULL AND A.ValidFrom<=S.WorkDate AND (A.ValidTo IS NULL OR A.ValidTo>=S.WorkDate)) RateCount
 FROM Sources S LEFT JOIN dbo.Calisan C ON C.PersonId=S.PersonId LEFT JOIN dbo.Joker J ON J.PersonId=S.PersonId
 OUTER APPLY(SELECT TOP(1) A.* FROM dbo.HourlyRateAgreements A WITH(HOLDLOCK) WHERE A.ProjectId=@project AND A.PersonId=S.PersonId AND A.EmploymentId=S.EmploymentId AND A.RevokedUtc IS NULL AND A.ValidFrom<=S.WorkDate AND (A.ValidTo IS NULL OR A.ValidTo>=S.WorkDate) ORDER BY A.ValidFrom DESC,A.RateId) R
 LEFT JOIN dbo.AccrualCalculationLines U ON U.SourceKind=S.SourceKind AND U.SourceId=S.SourceId WHERE (@extra=0 OR U.LineId IS NULL) ORDER BY S.WorkDate,S.SourceKind,S.SourceId");
        Id(c,"@project",project);c.Parameters.Add("@extra",SqlDbType.Bit).Value=parent.HasValue;Day(c,"@first",from);Day(c,"@last",to);var lines=await c.TableAsync(ct);if(lines.Rows.Count>2000)throw new InvalidOperationException("Dönemde 2000 kaynak sınırı aşıldı; daha dar dönem seçin.");
        lines.Columns.Add("Amount",typeof(decimal));lines.Columns.Add("Issue",typeof(string));var issues=new List<string>();
        foreach(DataRow row in lines.Rows)
        {
            string error=row.Field<bool>("AlreadyCalculated")?"Kaynak önceki dönem hesabına alınmış":row.Field<bool>("BaseWageReserved")?"Günlük/aylık temel ücret kapsamına ayrılmış":row.Field<int>("RateCount")!=1?"İş gününde tam bir etkin tarife gerekiyor":row.IsNull("PaidMinutes")?"Ücretlendirilecek dakika eksik":"";
            row["Issue"]=error;if(error.Length>0)issues.Add(error);else row["Amount"]=decimal.Round(row.Field<int>("PaidMinutes")*row.Field<decimal>("HourlyAmount")/60m,2,MidpointRounding.AwayFromZero);
        }
        using var pending=w.Command(@"SELECT COUNT(*) FROM dbo.Timesheets T WHERE T.WorkDate BETWEEN @first AND @last AND T.Status IN(N'Draft',N'Returned',N'Submitted') AND ((T.ProjectId=@project AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL)) OR EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H JOIN dbo.TimesheetSegments S ON S.SetId=H.SetId WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL AND S.ProjectId=@project))");Id(pending,"@project",project);Day(pending,"@first",from);Day(pending,"@last",to);int count=Convert.ToInt32(await pending.ExecuteScalarAsync(ct));if(count>0)issues.Add($"Dönemde {count} sonuçlandırılmamış kaynak var; bütün parçalar dahil onay/iptal ile sonuçlandırın.");
        using var prior=w.Command("SELECT COUNT(*) FROM dbo.AccrualCalculations WHERE ProjectId=@project AND PeriodStart=@first AND PeriodEnd=@last AND ParentCalculationId IS NULL");Id(prior,"@project",project);Day(prior,"@first",from);Day(prior,"@last",to);if(!parent.HasValue&&Convert.ToInt32(await prior.ExecuteScalarAsync(ct))>0)issues.Add("Bu proje/dönem için değişmez hesap zaten var.");
        if(lines.Rows.Count==0)issues.Add("Dönemde onaylı hesap kaynağı yok.");
        lines.AcceptChanges();string fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{Project=project,From=from,To=to,Policy,Parent=parent,BaseVersion=baseCalculation?["RecordVersion"],Data=RemoteApi.Encode(lines),Issues=issues.ToArray()})));
        return new(lines,fingerprint,issues.Distinct().ToArray());
    }
    internal async Task<Wire.AccrualPreview> PreviewAsync(Guid project,DateTime from,DateTime to,CancellationToken ct,Guid? parent=null)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.AccrualPreview>($"accrual/{project:D}/preview?from={RemoteApi.Day(from)}&to={RemoteApi.Day(to)}"+(parent.HasValue?$"&parent={parent:D}":""),null,ct);
        Range(from,to);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);var p=await BuildAsync(w,project,from,to,ct,parent);await w.CommitAsync(ct);return new(RemoteApi.Encode(p.Lines),p.Fingerprint,p.Issues.Length==0,p.Issues);
    }
    internal async Task CalculateAsync(Wire.AccrualCalculate input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("accrual/calculate",input,ct);return;}
        Range(input.From,input.To);if(input.CalculationId==Guid.Empty||input.Fingerprint is not{Length:64})throw new ArgumentException("Önce güncel dönem önizlemesi oluşturun.");
        using var reason=AuditContext.UseReason(AuditContext.ValidateReason(input.Reason));await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,input.ProjectId,true,ct);Guid actor=await ActorAsync(w,ct);
        using(var find=w.Command("SELECT * FROM dbo.AccrualCalculations WITH(UPDLOCK,HOLDLOCK) WHERE CalculationId=@id"))
        {
            Id(find,"@id",input.CalculationId);var rows=await find.TableAsync(ct);if(rows.Rows.Count>0){var r=rows.Rows[0];if(r.Field<Guid?>("ParentCalculationId")!=input.ParentCalculationId||r.Field<Guid>("ProjectId")!=input.ProjectId||r.Field<DateTime>("PeriodStart")!=input.From||r.Field<DateTime>("PeriodEnd")!=input.To||r.Field<Guid>("CreatedByAccountId")!=actor||!r.Field<bool>("IsSealed")||Convert.ToHexString((byte[])r["SnapshotHash"])!=input.Fingerprint)throw new InvalidOperationException("Hesap işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}
        }
        var p=await BuildAsync(w,input.ProjectId,input.From,input.To,ct,input.ParentCalculationId);if(p.Issues.Length>0)throw new InvalidOperationException(string.Join("\n",p.Issues));if(p.Fingerprint!=input.Fingerprint)throw new InvalidOperationException("Önizleme sonrası tarife/kaynak değişmiş; güncel önizlemeyi inceleyin.");
        decimal total=p.Lines.AsEnumerable().Sum(r=>r.Field<decimal>("Amount"));if(total>9999999999999999.99m)throw new InvalidOperationException("Dönem toplamı tutar sınırını aşıyor.");
        using(var c=w.Command("INSERT dbo.AccrualCalculations(CalculationId,ProjectId,PeriodStart,PeriodEnd,Policy,Currency,TotalAmount,LineCount,SnapshotHash,CreatedByAccountId,CreatedBy,ParentCalculationId) VALUES(@id,@project,@first,@last,@policy,N'TRY',@amount,@count,@hash,@actor,@name,@parent)"))
        {Id(c,"@parent",input.ParentCalculationId);Id(c,"@id",input.CalculationId);Id(c,"@project",input.ProjectId);Id(c,"@actor",actor);Day(c,"@first",input.From);Day(c,"@last",input.To);c.Text("@policy",Policy,60);c.Text("@name",user.Username);c.Money("@amount",total);c.Parameters.Add("@count",SqlDbType.Int).Value=p.Lines.Rows.Count;c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=Convert.FromHexString(input.Fingerprint);await c.ExecuteNonQueryAsync(ct);}
        foreach(DataRow r in p.Lines.Rows)
        {
            using var c=w.Command("INSERT dbo.AccrualCalculationLines(LineId,CalculationId,SourceKind,SourceId,TimesheetId,PersonId,EmploymentId,PersonCaption,WorkDate,PaidMinutes,RateId,HourlyAmount,Amount,ParentVersion,RateVersion,SetVersion,ApprovalId,ApprovalVersion) VALUES(NEWID(),@calculation,@kind,@source,@timesheet,@person,@employment,@caption,@day,@minutes,@rate,@hourly,@amount,@parentv,@ratev,@setv,@approval,@approvalv)");
            c.Text("@caption",(r["AD"].ToString()+" "+r["SOYAD"].ToString()).Trim(),101);Id(c,"@calculation",input.CalculationId);c.Text("@kind",r.Field<string>("SourceKind")!,10);foreach(var pair in new[]{("@source","SourceId"),("@timesheet","TimesheetId"),("@person","PersonId"),("@employment","EmploymentId"),("@rate","RateId"),("@approval","ApprovalId")})Id(c,pair.Item1,r.Field<Guid?>(pair.Item2));Day(c,"@day",r.Field<DateTime>("WorkDate"));c.Parameters.Add("@minutes",SqlDbType.Int).Value=r["PaidMinutes"];c.Money("@hourly",r.Field<decimal>("HourlyAmount"));c.Money("@amount",r.Field<decimal>("Amount"));foreach(var pair in new[]{("@parentv","ParentVersion"),("@ratev","RateVersion"),("@setv","SetVersion"),("@approvalv","ApprovalVersion")})c.Parameters.Add(pair.Item1,SqlDbType.Binary,8).Value=r[pair.Item2];await c.ExecuteNonQueryAsync(ct);
        }
        using(var seal=w.Command("UPDATE dbo.AccrualCalculations SET IsSealed=1 WHERE CalculationId=@id")){Id(seal,"@id",input.CalculationId);await seal.ExecuteNonQueryAsync(ct);}await w.CommitAsync(ct);
    }
    internal async Task<DataTable> HistoryAsync(Guid project,DateTime from,DateTime to,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"accrual/{project:D}/history?from={RemoteApi.Day(from)}&to={RemoteApi.Day(to)}",ct);
        Range(from,to);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);using var c=w.Command(@"SELECT TOP(1000) C.*,(SELECT SUM(B.EffectiveAmount) FROM dbo.AccrualItemBalances B WHERE B.PostingId=P.PostingId) EffectivePostedTotal,CASE WHEN C.ParentCalculationId IS NULL THEN N'Asıl hesap' ELSE N'Ek hesap' END CalculationType,CONVERT(nvarchar(36),C.ParentCalculationId) BaseCalculation,A.ApprovalId,A.RecordVersion ApprovalVersion,A.ApprovedBy,A.ApprovedUtc,P.PostingId,P.PostedBy,P.PostedUtc,
 CASE WHEN P.IsSealed=1 THEN N'Posted' WHEN A.ApprovalId IS NOT NULL THEN N'Approved' ELSE N'Prepared' END WorkflowState,
 CAST(CASE WHEN @admin=1 AND A.ApprovalId IS NULL AND dbo.SameAccountPerson(TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),C.CreatedByAccountId)=0 AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND dbo.AccountRepresentsPerson(TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),L.PersonId)=1) THEN 1 ELSE 0 END AS bit) CanApprove
 FROM dbo.AccrualCalculations C LEFT JOIN dbo.AccrualApprovals A ON A.CalculationId=C.CalculationId LEFT JOIN dbo.AccrualPostings P ON P.CalculationId=C.CalculationId WHERE C.ProjectId=@project AND C.PeriodStart<=@last AND C.PeriodEnd>=@first AND C.IsSealed=1 ORDER BY C.CreatedUtc DESC,C.CalculationId");c.Parameters.Add("@admin",SqlDbType.Bit).Value=user.Role==UserRole.Admin;Id(c,"@project",project);Day(c,"@first",from);Day(c,"@last",to);var rows=await c.TableAsync(ct);await w.CommitAsync(ct);return rows;
    }
    internal async Task<DataTable> LinesAsync(Guid project,Guid calculation,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).TableAsync($"accrual/{project:D}/{calculation:D}/lines",ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,project,false,ct);using var c=w.Command("SELECT L.* FROM dbo.AccrualCalculationLines L JOIN dbo.AccrualCalculations P ON P.CalculationId=L.CalculationId WHERE P.ProjectId=@project AND P.CalculationId=@id AND P.IsSealed=1 ORDER BY L.WorkDate,L.SourceKind,L.SourceId");Id(c,"@project",project);Id(c,"@id",calculation);var rows=await c.TableAsync(ct);await w.CommitAsync(ct);return rows;
    }
}
