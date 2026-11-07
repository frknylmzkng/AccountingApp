using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed record FiscalAmounts(decimal Labor=0,decimal OtherCost=0,decimal CashOut=0,decimal CashReturn=0,decimal AdvanceOut=0,decimal AdvanceReturn=0,decimal Allocated=0,decimal BankMatched=0)
{
    [JsonIgnore] internal decimal NetCash=>CashOut-CashReturn;
    [JsonIgnore] internal decimal NetAdvance=>AdvanceOut-AdvanceReturn;
    [JsonIgnore] internal decimal Cost=>Labor+OtherCost;
    [JsonIgnore] internal decimal ProjectPosition=>Labor-NetCash;
    internal FiscalAmounts Add(FiscalAmounts b)=>new(Labor+b.Labor,OtherCost+b.OtherCost,CashOut+b.CashOut,CashReturn+b.CashReturn,AdvanceOut+b.AdvanceOut,AdvanceReturn+b.AdvanceReturn,Allocated+b.Allocated,BankMatched+b.BankMatched);
    internal static FiscalAmounts Sum(IEnumerable<FiscalLine> lines)=>lines.Aggregate(new FiscalAmounts(),(a,l)=>a.Add(l.Amounts));
}
internal sealed record FiscalLine(int LineNo,string SourceKind,string SourceKey,DateTime OccurredUtc,Guid? PersonId,string Reference,string? OriginKind,string? OriginKey,DateTime? ServiceDay,string Bucket,string LinkKind,Guid? RelatedClosingId,int? RelatedLineNo,FiscalAmounts Amounts);
internal sealed record FiscalSnapshot(int Format,Guid ClosingId,Guid ProjectId,Guid? PreviousClosingId,string? PreviousSnapshotHash,DateTime FromDay,DateTime ToDay,DateTime RangeStartUtc,DateTime RangeEndUtc,string TimeZoneId,DateTime ClosedUtc,Guid ClosedByAccountId,string ClosedBy,string Reason,string ProjectCaption,string SourceFingerprint,FiscalAmounts Opening,FiscalAmounts Period,FiscalAmounts Cumulative,FiscalLine[] Lines)
{[JsonIgnore] internal string StoredHash{get;init;}="";}

internal sealed class FiscalPeriodService(string connectionString,UserSession user)
{
    internal const int MaxLines=10000,MaxSnapshotBytes=16*1024*1024;
    private static readonly string[] AmountNames=["Labor","OtherCost","CashOut","CashReturn","AdvanceOut","AdvanceReturn","Allocated","BankMatched"];
    private static readonly HashSet<string> Kinds=["FinancePosted","FinanceReversed","AccrualCorrection","Payment","PaymentReversal","Allocation","AllocationRelease","BankMatch","BankMatchRelease"];
    private static void Id(SqlCommand c,string name,Guid? value)=>c.Parameters.Add(name,SqlDbType.UniqueIdentifier).Value=(object?)value??DBNull.Value;
    private static DateTime Utc(DateTime d)=>DateTime.SpecifyKind(d,DateTimeKind.Utc);
    private static byte[] JsonHash(string json)=>SHA256.HashData(Encoding.Unicode.GetBytes(json));
    private static byte[] PayloadHash(object input)=>SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input,input.GetType()));
    internal static async Task AuthorizeAsync(IUnitOfWork w,UserSession user,Guid project,bool write,CancellationToken ct)
    {await TimesheetService.AllocationGateAsync(w,ct);await AccountAuthorization.DemandAsync(w,user,write?AccountAccess.Admin:AccountAccess.Read,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Costs,ct);await ProjectAuthorization.DemandAsync(w,user,project,ProjectAccess.Payments,ct);}
    private static void ValidateDates(DateTime from,DateTime to)
    {if(from.TimeOfDay!=TimeSpan.Zero||to.TimeOfDay!=TimeSpan.Zero||from.Year is <1900 or >2199||to.Year is <1900 or >2199||from>to)throw new ArgumentException("1900–2199 aralığında başlangıç/bitiş günü seçin; başlangıç bitişten sonra olamaz.");}
    private static async Task<(Guid? Id,DateTime? To,string? Hash)> LatestAsync(IUnitOfWork w,Guid project,CancellationToken ct)
    {using var c=w.Command("SELECT TOP(1) ClosingId,ToDay,SnapshotHash FROM dbo.FiscalPeriodClosings WITH(UPDLOCK,HOLDLOCK) WHERE ProjectId=@project AND IsSealed=1 ORDER BY ToDay DESC");Id(c,"@project",project);using var r=await c.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?(r.GetGuid(0),r.GetDateTime(1),Convert.ToHexString(r.GetFieldValue<byte[]>(2))):(null,null,null);}
    internal static DataTable Summary(FiscalAmounts opening,FiscalAmounts period,FiscalAmounts cumulative)
    {
        var t=new DataTable();t.Columns.Add("Scope");foreach(string name in AmountNames.Concat(["TotalCost","NetCash","NetAdvance","ProjectPosition"]))t.Columns.Add(name,typeof(decimal));
        foreach(var pair in new[]{("Devir / açılış",opening),("Bu döneme kaydedilen (geç yansıma dahil)",period),("Kümülatif kapanış",cumulative)}){var a=pair.Item2;t.Rows.Add(pair.Item1,a.Labor,a.OtherCost,a.CashOut,a.CashReturn,a.AdvanceOut,a.AdvanceReturn,a.Allocated,a.BankMatched,a.Cost,a.NetCash,a.NetAdvance,a.ProjectPosition);}return t;
    }
    internal static DataTable LinesTable(IEnumerable<FiscalLine> lines)
    {
        var t=new DataTable();t.Columns.Add("LineNo",typeof(int));t.Columns.Add("SourceKind");t.Columns.Add("SourceKey");t.Columns.Add("OccurredUtc",typeof(DateTime));t.Columns.Add("PersonId",typeof(Guid));t.Columns.Add("Reference");t.Columns.Add("OriginKind");t.Columns.Add("OriginKey");t.Columns.Add("ServiceDay",typeof(DateTime));t.Columns.Add("Bucket");t.Columns.Add("LinkKind");t.Columns.Add("RelatedClosingId",typeof(Guid));t.Columns.Add("RelatedLineNo",typeof(int));foreach(string name in AmountNames)t.Columns.Add(name,typeof(decimal));
        foreach(var l in lines){var a=l.Amounts;t.Rows.Add(l.LineNo,l.SourceKind,l.SourceKey,l.OccurredUtc,(object?)l.PersonId??DBNull.Value,l.Reference,l.OriginKind??"",l.OriginKey??"",(object?)l.ServiceDay??DBNull.Value,l.Bucket,l.LinkKind,(object?)l.RelatedClosingId??DBNull.Value,(object?)l.RelatedLineNo??DBNull.Value,a.Labor,a.OtherCost,a.CashOut,a.CashReturn,a.AdvanceOut,a.AdvanceReturn,a.Allocated,a.BankMatched);}return t;
    }
    private static bool Reconciles(FiscalAmounts a)=>a.Labor>=0&&a.OtherCost>=0&&a.CashOut>=a.CashReturn&&a.CashReturn>=0&&a.AdvanceOut>=a.AdvanceReturn&&a.AdvanceReturn>=0&&a.CashOut>=a.AdvanceOut&&a.CashReturn>=a.AdvanceReturn&&a.NetAdvance<=a.NetCash&&a.Allocated>=0&&a.Allocated<=a.Labor&&a.Allocated<=a.NetCash&&a.BankMatched>=0&&a.BankMatched<=a.NetCash;
    private sealed record Prepared(Guid? Parent,string? ParentHash,string Caption,DateTime Start,DateTime End,FiscalAmounts Opening,FiscalAmounts Period,FiscalAmounts Cumulative,FiscalLine[] Lines,string Fingerprint);
    private static async Task<Prepared> PrepareAsync(IUnitOfWork w,Guid project,DateTime from,DateTime to,CancellationToken ct)
    {
        ValidateDates(from,to);DateTime start=PaymentReportService.UtcDayRange(from,DailyClosingService.BusinessZone).Start,end=PaymentReportService.UtcDayRange(to,DailyClosingService.BusinessZone).End;
        using(var clock=w.Command("SELECT SYSUTCDATETIME()")){if(end>Utc((DateTime)(await clock.ExecuteScalarAsync(ct))!))throw new InvalidOperationException("Yalnız tamamlanmış günlerden oluşan mali dönem kesinleştirilebilir.");}
        var latest=await LatestAsync(w,project,ct);if(latest.To.HasValue&&from.Date!=latest.To.Value.AddDays(1))throw new InvalidOperationException("Başlangıç son kesin kapanışın ertesi günü olmalı; boşluk veya çakışma bırakılamaz. Kapanış listesini yenileyin.");
        FiscalSnapshot? previous=latest.Id.HasValue?await LoadSnapshotAsync(w,project,latest.Id.Value,ct):null;
        string caption;byte[] projectVersion;using(var p=w.Command("SELECT Code,Name,RecordVersion FROM dbo.FinanceProjects WHERE ProjectId=@project")){Id(p,"@project",project);using var r=await p.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("Proje bulunamadı.");caption=r.GetString(0)+" · "+r.GetString(1);projectVersion=r.GetFieldValue<byte[]>(2);}
        using(var issues=w.Command(@"SELECT COUNT(*) FROM dbo.PaymentLedger P WHERE P.ProjectId=@project AND P.Kind=N'Payment' AND P.CreatedUtc<@end
 AND(P.Delta>=0 OR(P.ReversedUtc IS NOT NULL AND P.ReversedUtc<@end AND NOT EXISTS(SELECT 1 FROM dbo.PaymentLedger R WHERE R.ReversesLedgerId=P.LedgerId AND R.Kind=N'Reversal' AND R.Delta=-P.Delta)))"))
        {Id(issues,"@project",project);issues.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;if(Convert.ToInt32(await issues.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Proje ödemelerinde işaret veya bağlantılı iptal kanıtı eksik. Kaynakları uzlaştırmadan dönem kapatılamaz.");}
        using var c=w.Command(@"SELECT TOP(10001) E.*,O.ClosingId OriginClosingId,O.[LineNo] OriginLineNo,L.ClosingId DateClosingId
 FROM dbo.FiscalSourceEvents E LEFT JOIN dbo.FiscalPeriodLines O ON O.ProjectId=E.ProjectId AND O.SourceKind=E.OriginKind AND O.SourceKey=E.OriginKey
 OUTER APPLY(SELECT TOP(1) H.ClosingId FROM dbo.FiscalPeriodClosings H WHERE H.ProjectId=E.ProjectId AND H.IsSealed=1 AND H.ToDay<@from
 AND H.ToDay>=CASE WHEN E.OccurredUtc<@start THEN CONVERT(date,E.OccurredUtc AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') WHEN E.SourceKind=N'FinancePosted' AND E.ServiceDay<@from THEN E.ServiceDay ELSE NULL END ORDER BY H.ToDay) L
 WHERE E.ProjectId=@project AND E.OccurredUtc<@end AND NOT EXISTS(SELECT 1 FROM dbo.FiscalPeriodLines X WHERE X.ProjectId=E.ProjectId AND X.SourceKind=E.SourceKind AND X.SourceKey=E.SourceKey)
 ORDER BY E.OccurredUtc,E.SourceKind,E.SourceKey");Id(c,"@project",project);c.Parameters.Add("@from",SqlDbType.Date).Value=from;c.Parameters.Add("@start",SqlDbType.DateTime2).Value=start;c.Parameters.Add("@end",SqlDbType.DateTime2).Value=end;var data=await c.TableAsync(ct);
        if(data.Rows.Count>MaxLines)throw new InvalidOperationException("Kapanış 10.000 yeni kaynak olayını aşıyor. Daha erken bir bitişle veya daha kısa dönemlerle ilerleyin; ilk kapanış önceki açılış olaylarını da içerir.");
        var lines=new List<FiscalLine>();foreach(DataRow row in data.Rows)
        {
            DateTime occurred=Utc(row.Field<DateTime>("OccurredUtc"));string kind=row.Field<string>("SourceKind")!;Guid? related=row.Field<Guid?>("OriginClosingId");int? relatedLine=row.Field<int?>("OriginLineNo");string link=related.HasValue?"ClosedSource":"";
            if(!related.HasValue&&latest.Id.HasValue){related=row.Field<Guid?>("DateClosingId");if(related.HasValue)link=occurred<start?"LateBooking":"PriorService";}
            string bucket=occurred<start?(latest.Id.HasValue?"Carry":"Opening"):"Period";
            lines.Add(new(lines.Count+1,kind,row.Field<string>("SourceKey")!,occurred,row.Field<Guid?>("PersonId"),row.Field<string?>("Reference")??"",row.Field<string?>("OriginKind"),row.Field<string?>("OriginKey"),row.Field<DateTime?>("ServiceDay"),bucket,link,related,relatedLine,new(row.Field<decimal>("Labor"),row.Field<decimal>("OtherCost"),row.Field<decimal>("CashOut"),row.Field<decimal>("CashReturn"),row.Field<decimal>("AdvanceOut"),row.Field<decimal>("AdvanceReturn"),row.Field<decimal>("Allocated"),row.Field<decimal>("BankMatched"))));
        }
        var opening=previous?.Cumulative??FiscalAmounts.Sum(lines.Where(l=>l.Bucket=="Opening"));var period=FiscalAmounts.Sum(lines.Where(l=>l.Bucket!="Opening"));var cumulative=opening.Add(period);
        foreach(var a in new[]{opening,period,cumulative})foreach(decimal value in new[]{a.Labor,a.OtherCost,a.CashOut,a.CashReturn,a.AdvanceOut,a.AdvanceReturn,a.Allocated,a.BankMatched,a.Cost,a.NetCash,a.NetAdvance,a.ProjectPosition})if(Math.Abs(value)>99999999999999999999999999.99m)throw new InvalidOperationException("Kapanış toplamı para sınırını aşıyor.");
        if(!Reconciles(cumulative))throw new InvalidOperationException("Kümülatif mali kaynaklar, ödeme/iptal, tahsis veya banka bağlantıları uzlaşmıyor. Kaynakları incelemeden dönem kapatılamaz.");
        string fingerprint=Convert.ToHexString(PayloadHash(new{ProjectId=project,From=from.Date,To=to.Date,Parent=latest.Id,ParentHash=latest.Hash,ProjectVersion=projectVersion,StartUtc=start,EndUtc=end,TimeZoneId="Europe/Istanbul",Opening=opening,Period=period,Lines=lines}));return new(latest.Id,latest.Hash,caption,start,end,opening,period,cumulative,lines.ToArray(),fingerprint);
    }
    internal async Task<Wire.FiscalPeriodQuote> QuoteAsync(Wire.FiscalPeriodScope input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.FiscalPeriodQuote>("fiscal-periods/quote",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,user,input.ProjectId,true,ct);var p=await PrepareAsync(w,input.ProjectId,input.From,input.To,ct);await w.CommitAsync(ct);
        return new(RemoteApi.Encode(Summary(p.Opening,p.Period,p.Cumulative)),RemoteApi.Encode(LinesTable(p.Lines.Take(200))),p.Lines.Length,p.Parent,p.Fingerprint,["Kapsam: bu projeye bağlı mali kayıt olayları, İstanbul kayıt günü. Çalışma/hizmet tarihi farklı olabilir; projesiz eski hareketler ve tüm personel bakiyesi dahil değildir.","İlk kapanışta başlangıçtan önceki olaylar açılışa; sonraki kapanışlarda geç yansıyan olaylar bu döneme eklenir. Kesin kapanış değişmez ve yeniden açılamaz.",$"Önceki kapanışa bağlı olay: {p.Lines.Count(l=>l.RelatedClosingId.HasValue)}; açılış: {p.Lines.Count(l=>l.Bucket=="Opening")}; geç kayıt: {p.Lines.Count(l=>l.Bucket=="Carry")}. Önizlemede ilk 200 satır gösterilir; tamamı saklanır.","Yeni ödeme, hakediş veya bakiye hareketi üretilmez. Sonraki mali düzeltme mevcut bağımsız onay akışından geçer ve sonraki kapanışta önceki kaynağa bağlanır."]);
    }
    internal async Task FinalizeAsync(Wire.FiscalPeriodFinalize input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("fiscal-periods/finalize",input,ct);return;}
        if(input.ClosingId==Guid.Empty||input.Fingerprint is not{Length:64})throw new ArgumentException("Güncel kapanış önizlemesi gerekiyor.");string reasonText=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(reasonText);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,user,input.ProjectId,true,ct);Guid actor;using(var a=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))"))actor=(Guid)(await a.ExecuteScalarAsync(ct)??throw new SessionRevokedException());byte[] payload=PayloadHash(input);
        using(var old=w.Command("SELECT ProjectId,ClosedByAccountId,PayloadHash,IsSealed FROM dbo.FiscalPeriodClosings WHERE ClosingId=@id")){Id(old,"@id",input.ClosingId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){var r=t.Rows[0];if(r.Field<Guid>("ProjectId")!=input.ProjectId||r.Field<Guid>("ClosedByAccountId")!=actor||!r.Field<bool>("IsSealed")||!((byte[])r["PayloadHash"]).SequenceEqual(payload))throw new InvalidOperationException("Kapanış işlem kimliği farklı içerikle kullanılmış veya tamamlanmamış.");await LoadSnapshotAsync(w,input.ProjectId,input.ClosingId,ct);await w.CommitAsync(ct);return;}}
        var p=await PrepareAsync(w,input.ProjectId,input.From,input.To,ct);if(p.Parent!=input.PreviousClosingId||p.Fingerprint!=input.Fingerprint)throw new InvalidOperationException("Kapanış önizlemesi veya önceki kapanış değişmiş; yeni önizleme oluşturun.");
        DateTime now;using(var clock=w.Command("SELECT SYSUTCDATETIME()"))now=Utc((DateTime)(await clock.ExecuteScalarAsync(ct))!);
        var snapshot=new FiscalSnapshot(1,input.ClosingId,input.ProjectId,p.Parent,p.ParentHash,input.From.Date,input.To.Date,p.Start,p.End,"Europe/Istanbul",now,actor,user.Username,reasonText,p.Caption,p.Fingerprint,p.Opening,p.Period,p.Cumulative,p.Lines);string json=JsonSerializer.Serialize(snapshot);if(Encoding.Unicode.GetByteCount(json)>MaxSnapshotBytes)throw new InvalidOperationException("Kapanış 16 MiB sınırını aşıyor; daha kısa dönem seçin.");
        using(var save=w.Command(@"INSERT dbo.FiscalPeriodClosings(ClosingId,ProjectId,PreviousClosingId,FromDay,ToDay,RangeStartUtc,RangeEndUtc,ClosedUtc,ClosedByAccountId,ClosedBy,Reason,LineCount,SnapshotJson,SnapshotHash,PayloadHash) VALUES(@id,@project,@previous,@from,@to,@start,@end,@now,@actor,@name,@reason,@count,@json,@hash,@payload)"))
        {Id(save,"@id",input.ClosingId);Id(save,"@project",input.ProjectId);Id(save,"@previous",p.Parent);Id(save,"@actor",actor);save.Parameters.Add("@from",SqlDbType.Date).Value=input.From;save.Parameters.Add("@to",SqlDbType.Date).Value=input.To;save.Parameters.Add("@start",SqlDbType.DateTime2).Value=p.Start;save.Parameters.Add("@end",SqlDbType.DateTime2).Value=p.End;save.Parameters.Add("@now",SqlDbType.DateTime2).Value=now;save.Text("@name",user.Username);save.Text("@reason",reasonText,500);save.Parameters.Add("@count",SqlDbType.Int).Value=p.Lines.Length;save.Text("@json",json,-1);save.Parameters.Add("@hash",SqlDbType.Binary,32).Value=JsonHash(json);save.Parameters.Add("@payload",SqlDbType.Binary,32).Value=payload;await save.ExecuteNonQueryAsync(ct);}
        using(var lines=w.Command(@"INSERT dbo.FiscalPeriodLines(ClosingId,ProjectId,[LineNo],SourceKind,SourceKey,OccurredUtc,RelatedClosingId,RelatedLineNo,PayloadJson,PayloadHash)
 SELECT H.ClosingId,H.ProjectId,CONVERT(int,J.[key])+1,JSON_VALUE(J.value,'$.SourceKind'),JSON_VALUE(J.value,'$.SourceKey'),CONVERT(datetime2(7),JSON_VALUE(J.value,'$.OccurredUtc'),127),TRY_CONVERT(uniqueidentifier,JSON_VALUE(J.value,'$.RelatedClosingId')),TRY_CONVERT(int,JSON_VALUE(J.value,'$.RelatedLineNo')),J.value,HASHBYTES('SHA2_256',J.value)
 FROM dbo.FiscalPeriodClosings H CROSS APPLY OPENJSON(H.SnapshotJson,'$.Lines') J WHERE H.ClosingId=@id AND H.IsSealed=0")){Id(lines,"@id",input.ClosingId);await lines.ExecuteNonQueryAsync(ct);}
        using(var seal=w.Command("UPDATE dbo.FiscalPeriodClosings SET IsSealed=1 WHERE ClosingId=@id AND IsSealed=0")){Id(seal,"@id",input.ClosingId);if(await seal.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Kapanış kesinleştirilemedi.");}
        await w.CommitAsync(ct);
    }
    internal static async Task<FiscalSnapshot> LoadSnapshotAsync(IUnitOfWork w,Guid project,Guid id,CancellationToken ct)
    {
        using var c=w.Command("SELECT * FROM dbo.FiscalPeriodClosings WHERE ProjectId=@project AND ClosingId=@id AND IsSealed=1");Id(c,"@project",project);Id(c,"@id",id);var t=await c.TableAsync(ct);if(t.Rows.Count!=1)throw new InvalidOperationException("Bu projede kesin kapanış bulunamadı.");var r=t.Rows[0];string json=r.Field<string>("SnapshotJson")!;
        if(Encoding.Unicode.GetByteCount(json)>MaxSnapshotBytes||!CryptographicOperations.FixedTimeEquals(JsonHash(json),(byte[])r["SnapshotHash"]))throw new InvalidOperationException("Kapanış içerik özeti doğrulanamadı.");var s=JsonSerializer.Deserialize<FiscalSnapshot>(json)??throw new InvalidOperationException("Kapanış içeriği okunamadı.");
        bool bad=s.Format!=1||s.ClosingId!=id||s.ProjectId!=project||s.PreviousClosingId!=r.Field<Guid?>("PreviousClosingId")||s.FromDay!=r.Field<DateTime>("FromDay")||s.ToDay!=r.Field<DateTime>("ToDay")||s.RangeStartUtc!=Utc(r.Field<DateTime>("RangeStartUtc"))||s.RangeEndUtc!=Utc(r.Field<DateTime>("RangeEndUtc"))||s.ClosedUtc!=Utc(r.Field<DateTime>("ClosedUtc"))||s.ClosedByAccountId!=r.Field<Guid>("ClosedByAccountId")||s.ClosedBy!=r.Field<string>("ClosedBy")||s.Reason!=r.Field<string>("Reason")||s.Lines==null||s.Lines.Length!=r.Field<int>("LineCount");if(bad)throw new InvalidOperationException("Kapanış başlık bilgileri uzlaşmıyor.");
        if(s.Lines==null)throw new InvalidOperationException("Kapanış satırları eksik.");
        ValidateDates(s.FromDay,s.ToDay);
        if(s.TimeZoneId!="Europe/Istanbul"||s.RangeStartUtc>=s.RangeEndUtc||s.ClosedUtc<s.RangeEndUtc||s.Lines!.Where((l,i)=>l.LineNo!=i+1||!Kinds.Contains(l.SourceKind)||l.OccurredUtc>=s.RangeEndUtc||l.Bucket is not("Opening" or "Period" or "Carry")||((l.Bucket=="Opening"||l.Bucket=="Carry")!=(l.OccurredUtc<s.RangeStartUtc))||l.RelatedClosingId==id).Any()||s.Period!=FiscalAmounts.Sum(s.Lines.Where(l=>l.Bucket!="Opening"))||s.Cumulative!=s.Opening.Add(s.Period)||!Reconciles(s.Cumulative))throw new InvalidOperationException("Kapanış satırları veya toplamları uzlaşmıyor.");
        if(s.PreviousClosingId.HasValue)
        {
            using var p=w.Command("SELECT SnapshotHash,SnapshotJson,ToDay FROM dbo.FiscalPeriodClosings WHERE ProjectId=@project AND ClosingId=@id AND IsSealed=1");Id(p,"@project",project);Id(p,"@id",s.PreviousClosingId);var parents=await p.TableAsync(ct);if(parents.Rows.Count!=1)throw new InvalidOperationException("Önceki kapanış bulunamadı.");var parent=parents.Rows[0];string parentJson=parent.Field<string>("SnapshotJson")!;var body=JsonSerializer.Deserialize<FiscalSnapshot>(parentJson);if(body==null||!JsonHash(parentJson).SequenceEqual((byte[])parent["SnapshotHash"])||s.PreviousSnapshotHash!=Convert.ToHexString((byte[])parent["SnapshotHash"])||s.FromDay!=parent.Field<DateTime>("ToDay").AddDays(1)||s.Opening!=body.Cumulative||s.Lines.Any(l=>l.Bucket=="Opening"))throw new InvalidOperationException("Önceki kapanış bağlantısı veya devir toplamı doğrulanamadı.");
        }
        else if(s.PreviousSnapshotHash!=null||s.Opening!=FiscalAmounts.Sum(s.Lines.Where(l=>l.Bucket=="Opening"))||s.Lines.Any(l=>l.Bucket=="Carry"||l.RelatedClosingId.HasValue))throw new InvalidOperationException("İlk kapanışın açılış toplamı uzlaşmıyor.");return s with{StoredHash=Convert.ToHexString((byte[])r["SnapshotHash"])};
    }
    internal static DataTable Header(FiscalSnapshot s)
    {var t=new DataTable();foreach(string col in new[]{"ClosingId","PreviousClosingId","ProjectCaption","FromDay","ToDay","ClosedUtc","ClosedBy","Reason","LineCount","SourceFingerprint","SnapshotHash","TimeZoneId"})t.Columns.Add(col);t.Rows.Add(s.ClosingId,s.PreviousClosingId?.ToString()??"",s.ProjectCaption,s.FromDay.ToString("yyyy-MM-dd"),s.ToDay.ToString("yyyy-MM-dd"),s.ClosedUtc.ToString("O"),s.ClosedBy,s.Reason,s.Lines.Length,s.SourceFingerprint,s.StoredHash,s.TimeZoneId);return t;}
    internal async Task<Wire.FiscalPeriodHistory> HistoryAsync(Wire.FiscalPeriodBrowse input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.FiscalPeriodHistory>("fiscal-periods/browse",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,user,input.ProjectId,false,ct);using var c=w.Command(@"SELECT P.Code,P.Name,L.ClosingId LatestClosingId,L.ToDay LatestToDay FROM dbo.FinanceProjects P OUTER APPLY(SELECT TOP(1) H.ClosingId,H.ToDay FROM dbo.FiscalPeriodClosings H WHERE H.ProjectId=P.ProjectId AND H.IsSealed=1 ORDER BY H.ToDay DESC) L WHERE P.ProjectId=@project");Id(c,"@project",input.ProjectId);var context=await c.TableAsync(ct);
        var page=await FinancialQueryScope.ReadPageAsync(w,"SELECT TOP(500) ClosingId,PreviousClosingId,FromDay,ToDay,ClosedUtc,ClosedBy,Reason,LineCount FROM dbo.FiscalPeriodClosings WHERE ProjectId=@project AND IsSealed=1 ORDER BY ToDay DESC,ClosingId",cmd=>Id(cmd,"@project",input.ProjectId),input.Index,input.Size,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(context),new(RemoteApi.Encode(page.Rows),page.Info.Total,page.Info.Index,page.Info.Size));
    }
    internal async Task<Wire.FiscalPeriodView> ReadAsync(Wire.FiscalPeriodRead input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.FiscalPeriodView>("fiscal-periods/read",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,user,input.ProjectId,false,ct);var s=await LoadSnapshotAsync(w,input.ProjectId,input.ClosingId,ct);var page=await FinancialQueryScope.ReadPageAsync(w,"SELECT TOP(500) [LineNo],PayloadJson,PayloadHash FROM dbo.FiscalPeriodLines WHERE ProjectId=@project AND ClosingId=@id ORDER BY [LineNo]",c=>{Id(c,"@project",input.ProjectId);Id(c,"@id",input.ClosingId);},input.Index,input.Size,ct);if(page.Info.Total!=s.Lines.Length)throw new InvalidOperationException("Kapanış satır sayısı doğrulanamadı.");var lines=new List<FiscalLine>();foreach(DataRow row in page.Rows.Rows){string json=row.Field<string>("PayloadJson")!;int n=row.Field<int>("LineNo");var l=JsonSerializer.Deserialize<FiscalLine>(json);if(n<1||n>s.Lines.Length||l==null||l!=s.Lines[n-1]||!JsonHash(json).SequenceEqual((byte[])row["PayloadHash"]))throw new InvalidOperationException("Kapanış satırı doğrulanamadı.");lines.Add(l);}await w.CommitAsync(ct);return new(RemoteApi.Encode(Header(s)),RemoteApi.Encode(Summary(s.Opening,s.Period,s.Cumulative)),new(RemoteApi.Encode(LinesTable(lines)),page.Info.Total,page.Info.Index,page.Info.Size));
    }
}
