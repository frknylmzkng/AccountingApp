using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class BankReconciliationService(string connectionString,UserSession user)
{
    // Statements can contain several projects and unrelated beneficiaries: never grant
    // statement-wide visibility through a single project's payment permission.
    private async Task AuthorizeAsync(IUnitOfWork w,CancellationToken ct)
    {
        await TimesheetService.AllocationGateAsync(w,ct);
        await AccountAuthorization.DemandAsync(w,user,AccountAccess.Admin,ct);
        await AccountAuthorization.DemandAsync(w,user,AccountAccess.ViewSalary,ct);
        await AccountAuthorization.DemandAsync(w,user,AccountAccess.ViewIban,ct);
    }
    private static void Id(SqlCommand c,string key,Guid value)=>c.Parameters.Add(key,SqlDbType.UniqueIdentifier).Value=value;
    private static async Task<Guid> ActorAsync(IUnitOfWork w,CancellationToken ct){using var c=w.Command("SELECT TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId'))");return (Guid)(await c.ExecuteScalarAsync(ct)??throw new SessionRevokedException());}
    private static async Task<string> AccountAsync(IUnitOfWork w,Guid account,CancellationToken ct)
    {using var c=w.Command("SELECT Iban FROM dbo.BankAccounts WITH(HOLDLOCK) WHERE AccountId=@account");Id(c,"@account",account);return await c.ExecuteScalarAsync(ct) as string??throw new ArgumentException("Banka hesabı bulunamadı.");}
    private static bool Same(DataRow row,string actorColumn,Guid actor,string hashColumn,byte[] hash)=>row.Field<Guid>(actorColumn)==actor&&((byte[])row[hashColumn]).SequenceEqual(hash);
    internal async Task<DataTable> AccountsAsync(CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return RemoteApi.Decode(await RemoteApi.For(connectionString,user).SendAsync<Wire.Table>("bank/accounts",null,ct));
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);using var c=w.Command("SELECT AccountId,Name,Iban,Currency FROM dbo.BankAccounts ORDER BY Name,AccountId");var data=await c.TableAsync(ct);await w.CommitAsync(ct);return data;
    }
    internal async Task CreateAccountAsync(Wire.BankAccountCreate input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("bank/accounts",input,ct);return;}
        string name=input.Name.Trim(),iban=BankCsv.Iban(input.Iban),reasonText=AuditContext.ValidateReason(input.Reason);
        if(input.AccountId==Guid.Empty||name.Length is <1 or >100||name.Any(char.IsControl))throw new ArgumentException("Hesap adı 1–100 karakter olmalı.");
        using var reason=AuditContext.UseReason(reasonText);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=BankCsv.Hash(input with{Name=name,Iban=iban,Reason=reasonText});
        using(var old=w.Command("SELECT CreatedByAccountId,PayloadHash FROM dbo.BankAccounts WHERE AccountId=@id")){Id(old,"@id",input.AccountId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){if(!Same(t.Rows[0],"CreatedByAccountId",actor,"PayloadHash",hash))throw new InvalidOperationException("Hesap işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        using(var exists=w.Command("SELECT COUNT(*) FROM dbo.BankAccounts WHERE Iban=@iban")){exists.Text("@iban",iban,34);if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Bu IBAN zaten tanımlı; mevcut banka hesabını seçin.");}
        using var c=w.Command("INSERT dbo.BankAccounts(AccountId,Name,Iban,Reason,CreatedByAccountId,CreatedBy,PayloadHash) VALUES(@id,@name,@iban,@reason,@actor,@user,@hash)");Id(c,"@id",input.AccountId);Id(c,"@actor",actor);c.Text("@name",name,100);c.Text("@iban",iban,34);c.Text("@reason",reasonText,500);c.Text("@user",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    private sealed record ImportData(BankCsv.Row[] Rows,Guid?[] Existing,byte[] ImportHash,Wire.BankImportPreview Preview);
    private static async Task<ImportData> PreviewAsync(IUnitOfWork w,Wire.BankCsvSource source,CancellationToken ct)
    {
        string iban=await AccountAsync(w,source.AccountId,ct);var rows=BankCsv.Parse(source,iban);
        if(source.FileName.Length is <1 or >150||source.FileName!=Path.GetFileName(source.FileName)||source.FileName.Any(char.IsControl))throw new ArgumentException("Dosya adı yol içermemeli ve 1–150 karakter olmalı.");
        byte[] importHash=BankCsv.Hash(new{source.AccountId,source.Csv,source.Separator,source.DateFormat,source.DecimalSeparator,source.Columns});
        using var batch=w.Command("SELECT COUNT(*) FROM dbo.BankImportBatches WHERE AccountId=@account AND ImportHash=@hash");Id(batch,"@account",source.AccountId);batch.Parameters.Add("@hash",SqlDbType.Binary,32).Value=importHash;bool imported=Convert.ToInt32(await batch.ExecuteScalarAsync(ct))!=0;
        var table=new DataTable();table.Columns.Add("SourceLine",typeof(int));table.Columns.Add("ExternalId");table.Columns.Add("BookingDay",typeof(DateTime));table.Columns.Add("SignedAmount",typeof(decimal));table.Columns.Add("CounterpartyIban");table.Columns.Add("Description");table.Columns.Add("PaymentReference");table.Columns.Add("ImportStatus");
        var existing=new Guid?[rows.Length];int duplicate=0;
        for(int i=0;i<rows.Length;i++)
        {
            var row=rows[i];using var c=w.Command("SELECT TransactionId,RowHash FROM dbo.BankTransactions WITH(UPDLOCK,HOLDLOCK) WHERE AccountId=@account AND ExternalId=@external");Id(c,"@account",source.AccountId);c.Text("@external",row.ExternalId,100);var t=await c.TableAsync(ct);
            if(t.Rows.Count>0){if(!((byte[])t.Rows[0]["RowHash"]).SequenceEqual(row.Hash))throw new InvalidOperationException($"CSV satır {row.Line}: aynı hesap/işlem kimliği farklı içerikle daha önce aktarılmış. Aktarım durduruldu; kaynak dosyayı kontrol edin.");existing[i]=t.Rows[0].Field<Guid>("TransactionId");duplicate++;}
            table.Rows.Add(row.Line,row.ExternalId,row.Day,row.Amount,row.Iban,row.Description,row.PaymentReference?.ToString("D")??"",existing[i].HasValue?"Önceden var; atlanacak":"Yeni ham hareket");
        }
        string fingerprint=Convert.ToHexString(BankCsv.Hash(new{ImportHash=importHash,Existing=existing,Imported=imported}));return new(rows,existing,importHash,new(RemoteApi.Encode(table),fingerprint,rows.Length-duplicate,duplicate,imported));
    }
    internal async Task<Wire.BankImportPreview> PreviewAsync(Wire.BankCsvSource source,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.BankImportPreview>("bank/import/preview",source,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);var data=await PreviewAsync(w,source,ct);await w.CommitAsync(ct);return data.Preview;
    }
    internal async Task ImportAsync(Wire.BankImport input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("bank/import",input,ct);return;}
        if(input.BatchId==Guid.Empty||input.Fingerprint is not{Length:64})throw new ArgumentException("Önce CSV önizlemesini oluşturun.");string reasonText=AuditContext.ValidateReason(input.Reason);using var reason=AuditContext.UseReason(reasonText);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=BankCsv.Hash(input);
        using(var old=w.Command("SELECT AccountId,CreatedByAccountId,PayloadHash FROM dbo.BankImportBatches WHERE BatchId=@id")){Id(old,"@id",input.BatchId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){if(t.Rows[0].Field<Guid>("AccountId")!=input.Source.AccountId||!Same(t.Rows[0],"CreatedByAccountId",actor,"PayloadHash",hash))throw new InvalidOperationException("Aktarım işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        var data=await PreviewAsync(w,input.Source,ct);if(data.Preview.AlreadyImported)throw new InvalidOperationException("Bu hesap/dosya/eşleme daha önce aktarıldı. Aktarım geçmişini kontrol edin.");if(data.Preview.Fingerprint!=input.Fingerprint)throw new InvalidOperationException("Önizleme sonrası banka hareketleri değişmiş; yeni önizleme oluşturun.");
        using(var c=w.Command("INSERT dbo.BankImportBatches(BatchId,AccountId,ImportHash,FileHash,FileName,Mapping,AddedCount,DuplicateCount,Reason,CreatedByAccountId,CreatedBy,PayloadHash) VALUES(@id,@account,@import,@file,@name,@mapping,@added,@duplicate,@reason,@actor,@user,@hash)"))
        {Id(c,"@id",input.BatchId);Id(c,"@account",input.Source.AccountId);Id(c,"@actor",actor);c.Parameters.Add("@import",SqlDbType.Binary,32).Value=data.ImportHash;c.Parameters.Add("@file",SqlDbType.Binary,32).Value=SHA256.HashData(Encoding.UTF8.GetBytes(input.Source.Csv));c.Text("@name",input.Source.FileName,150);c.Text("@mapping",JsonSerializer.Serialize(new{input.Source.Separator,input.Source.DateFormat,input.Source.DecimalSeparator,input.Source.Columns}),2000);c.Parameters.Add("@added",SqlDbType.Int).Value=data.Preview.NewRows;c.Parameters.Add("@duplicate",SqlDbType.Int).Value=data.Preview.DuplicateRows;c.Text("@reason",reasonText,500);c.Text("@user",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);}
        for(int i=0;i<data.Rows.Length;i++)
        {
            if(data.Existing[i].HasValue)continue;var row=data.Rows[i];using var c=w.Command("INSERT dbo.BankTransactions(TransactionId,AccountId,BatchId,SourceLine,ExternalId,BookingDay,SignedAmount,Currency,CounterpartyIban,Description,PaymentReference,RowHash) VALUES(@id,@account,@batch,@line,@external,@day,@amount,N'TRY',@iban,@description,@reference,@hash)");Id(c,"@id",Guid.NewGuid());Id(c,"@account",input.Source.AccountId);Id(c,"@batch",input.BatchId);c.Parameters.Add("@line",SqlDbType.Int).Value=row.Line;c.Text("@external",row.ExternalId,100);c.Parameters.Add("@day",SqlDbType.Date).Value=row.Day;c.Money("@amount",row.Amount);c.Text("@iban",row.Iban,34);c.Text("@description",row.Description,500);c.Parameters.Add("@reference",SqlDbType.UniqueIdentifier).Value=(object?)row.PaymentReference??DBNull.Value;c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=row.Hash;await c.ExecuteNonQueryAsync(ct);
        }
        await w.CommitAsync(ct);
    }
    private static async Task<DataTable> QuoteRowsAsync(IUnitOfWork w,Guid account,Guid transaction,long ledger,CancellationToken ct)
    {
        // Use the same ledger-first order as payment cancellation. A current person
        // name/IBAN is deliberately not used as proof of an old payment's beneficiary.
        using(var payment=w.Command("SELECT LedgerId FROM dbo.PaymentLedger WITH(UPDLOCK,HOLDLOCK) WHERE LedgerId=@ledger")){payment.Parameters.Add("@ledger",SqlDbType.BigInt).Value=ledger;if(await payment.ExecuteScalarAsync(ct)==null)throw new InvalidOperationException("Ödeme bulunamadı.");}
        using var c=w.Command(@"SELECT B.TransactionId,B.ExternalId,B.BookingDay,B.SignedAmount,B.CounterpartyIban,B.Description,B.PaymentReference,
 P.LedgerId,P.RequestId,P.PersonId,P.TC PersonTc,P.PersonnelGroup,P.ProjectId,P.CreatedUtc PaymentUtc,-P.Delta PaymentAmount,
 (SELECT COUNT_BIG(*) FROM dbo.BankPaymentMatches M WHERE M.TransactionId=B.TransactionId OR M.LedgerId=P.LedgerId) MatchHistoryCount,
 (SELECT MAX(CONVERT(bigint,M.RecordVersion)) FROM dbo.BankPaymentMatches M WHERE M.TransactionId=B.TransactionId OR M.LedgerId=P.LedgerId) LastMatchVersion
 FROM dbo.BankTransactions B WITH(UPDLOCK,HOLDLOCK) JOIN dbo.PaymentLedger P ON P.LedgerId=@ledger
 WHERE B.AccountId=@account AND B.TransactionId=@transaction AND B.SignedAmount<0 AND B.Currency=N'TRY' AND B.SignedAmount=P.Delta
 AND P.Kind=N'Payment' AND P.Delta<0 AND P.RequestId IS NOT NULL AND P.PersonId IS NOT NULL AND P.ReversedUtc IS NULL
 AND(B.PaymentReference IS NULL OR B.PaymentReference=P.RequestId)
 AND NOT EXISTS(SELECT 1 FROM dbo.BankPaymentMatches M WITH(UPDLOCK,HOLDLOCK) WHERE M.ReleasedUtc IS NULL AND(M.TransactionId=B.TransactionId OR M.LedgerId=P.LedgerId))");Id(c,"@account",account);Id(c,"@transaction",transaction);c.Parameters.Add("@ledger",SqlDbType.BigInt).Value=ledger;var rows=await c.TableAsync(ct);if(rows.Rows.Count!=1)throw new InvalidOperationException("Banka çıkışı ve ödeme tutarı/referansı eşleşmiyor, ödeme iptal edilmiş veya kayıt zaten eşleştirilmiş. Listeyi yenileyin.");return rows;
    }
    private static string Fingerprint(Guid account,DataTable rows)=>Convert.ToHexString(BankCsv.Hash(new{Account=account,Rows=RemoteApi.Encode(rows)}));
    internal async Task<Wire.BankMatchQuote> QuoteAsync(Wire.BankMatchQuoteRequest input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.BankMatchQuote>("bank/matches/quote",input,ct);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);await AccountAsync(w,input.AccountId,ct);var rows=await QuoteRowsAsync(w,input.AccountId,input.TransactionId,input.LedgerId,ct);var row=rows.Rows[0];var warnings=new List<string>{"Aday bir öneridir; tutar/tarih aynı olsa da alıcıyı kanıtlamaz. Banka dekontu ile bu ödemenin aynı işlem olduğunu kendiniz kontrol edin.","Ödeme anındaki IBAN uygulamada saklanmadığından güncel personel IBAN'ı ile otomatik doğrulama yapılmaz. Yeni ödeme, bakiye düşümü veya banka transferi oluşturulmaz."};if(row.IsNull("PaymentReference"))warnings.Add("Banka satırında uygulama ödeme referansı yok; kişi ve dekont eşlemesini özellikle kontrol edin.");if(Math.Abs((row.Field<DateTime>("BookingDay")-TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(row.Field<DateTime>("PaymentUtc"),DateTimeKind.Utc),DailyClosingService.BusinessZone).Date).TotalDays)>7)warnings.Add("Banka günü ile ödeme kayıt günü arasında 7 günden fazla fark var.");await w.CommitAsync(ct);return new(RemoteApi.Encode(rows),Fingerprint(input.AccountId,rows),warnings.ToArray());
    }
    internal async Task MatchAsync(Wire.BankMatchCreate input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("bank/matches",input,ct);return;}
        if(input.MatchId==Guid.Empty||input.Fingerprint is not{Length:64})throw new ArgumentException("Güncel eşleştirme önizlemesi gerekiyor.");using var reason=AuditContext.UseReason(input.Reason);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);await AccountAuthorization.DemandAsync(w,user,AccountAccess.Pay,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=BankCsv.Hash(input);
        using(var old=w.Command("SELECT M.CreatedByAccountId,M.PayloadHash,B.AccountId FROM dbo.BankPaymentMatches M JOIN dbo.BankTransactions B ON B.TransactionId=M.TransactionId WHERE M.MatchId=@id")){Id(old,"@id",input.MatchId);var t=await old.TableAsync(ct);if(t.Rows.Count>0){if(t.Rows[0].Field<Guid>("AccountId")!=input.AccountId||!Same(t.Rows[0],"CreatedByAccountId",actor,"PayloadHash",hash))throw new InvalidOperationException("Eşleştirme işlem kimliği farklı içerikle kullanılmış.");await w.CommitAsync(ct);return;}}
        var rows=await QuoteRowsAsync(w,input.AccountId,input.TransactionId,input.LedgerId,ct);if(Fingerprint(input.AccountId,rows)!=input.Fingerprint)throw new InvalidOperationException("Ödeme/eşleştirme bilgileri değişmiş; yeni önizleme oluşturun.");
        using var c=w.Command("INSERT dbo.BankPaymentMatches(MatchId,TransactionId,LedgerId,Reason,CreatedByAccountId,CreatedBy,PayloadHash) VALUES(@id,@transaction,@ledger,@reason,@actor,@user,@hash)");Id(c,"@id",input.MatchId);Id(c,"@transaction",input.TransactionId);Id(c,"@actor",actor);c.Parameters.Add("@ledger",SqlDbType.BigInt).Value=input.LedgerId;c.Text("@reason",AuditContext.ValidateReason(input.Reason),500);c.Text("@user",user.Username);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;await c.ExecuteNonQueryAsync(ct);await w.CommitAsync(ct);
    }
    internal async Task ReleaseAsync(Wire.BankMatchRelease input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("bank/matches/release",input,ct);return;}
        if(input.ReleaseId==Guid.Empty||input.Version is not{Length:8})throw new ArgumentException("Geçerli eşleştirme sürümü gerekiyor.");using var reason=AuditContext.UseReason(input.Reason);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);await AccountAuthorization.DemandAsync(w,user,AccountAccess.Pay,ct);Guid actor=await ActorAsync(w,ct);byte[] hash=BankCsv.Hash(input);
        using(var old=w.Command("SELECT M.* FROM dbo.BankPaymentMatches M WITH(UPDLOCK,HOLDLOCK) JOIN dbo.BankTransactions B ON B.TransactionId=M.TransactionId WHERE M.MatchId=@id AND B.AccountId=@account")){Id(old,"@id",input.MatchId);Id(old,"@account",input.AccountId);var t=await old.TableAsync(ct);if(t.Rows.Count!=1)throw new InvalidOperationException("Eşleştirme bulunamadı.");var row=t.Rows[0];if(!row.IsNull("ReleaseId")){if(row.Field<Guid>("ReleaseId")!=input.ReleaseId||!Same(row,"ReleasedByAccountId",actor,"ReleaseHash",hash))throw new InvalidOperationException("Eşleştirme daha önce kaldırılmış; geçmişi yenileyin.");await w.CommitAsync(ct);return;}}
        using var c=w.Command("UPDATE dbo.BankPaymentMatches SET ReleaseId=@release,ReleasedUtc=SYSUTCDATETIME(),ReleasedByAccountId=@actor,ReleasedBy=@user,ReleaseReason=@reason,ReleaseHash=@hash WHERE MatchId=@id AND RecordVersion=@version AND ReleasedUtc IS NULL");Id(c,"@id",input.MatchId);Id(c,"@release",input.ReleaseId);Id(c,"@actor",actor);c.Parameters.Add("@version",SqlDbType.Binary,8).Value=input.Version;c.Text("@user",user.Username);c.Text("@reason",AuditContext.ValidateReason(input.Reason),500);c.Parameters.Add("@hash",SqlDbType.Binary,32).Value=hash;if(await c.ExecuteNonQueryAsync(ct)!=1)throw new InvalidOperationException("Eşleştirme sürümü değişmiş; listeyi yenileyin.");await w.CommitAsync(ct);
    }
    internal static async Task DemandPaymentFreeAsync(IUnitOfWork w,long ledger,CancellationToken ct)
    {using var c=w.Command("SELECT COUNT(*) FROM dbo.BankPaymentMatches WHERE LedgerId=@ledger AND ReleasedUtc IS NULL");c.Parameters.Add("@ledger",SqlDbType.BigInt).Value=ledger;if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("Ödemeyi iptal etmeden önce Banka CSV / ödeme eşleştirme ekranında banka bağlantısını gerekçeyle kaldırın.");}
    internal async Task<Wire.Page> BrowseAsync(Wire.BankBrowse input,CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.Page>("bank/browse",input,ct);
        FinancialQueryScope.Page(input.Index,input.Size);await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,ct);await AccountAsync(w,input.AccountId,ct);
        string sql=input.Section switch
        {
            "Transactions"=>@"SELECT TOP(500) B.TransactionId,B.ExternalId,B.BookingDay,B.SignedAmount,B.Currency,B.CounterpartyIban,B.Description,B.PaymentReference,B.BatchId,M.MatchId,M.LedgerId,CASE WHEN M.MatchId IS NOT NULL THEN N'Eşleştirildi' WHEN B.SignedAmount>0 THEN N'Giriş; ödeme çıkışıyla eşleşmez' ELSE N'Eşleştirme bekliyor' END MatchStatus FROM dbo.BankTransactions B LEFT JOIN dbo.BankPaymentMatches M ON M.TransactionId=B.TransactionId AND M.ReleasedUtc IS NULL WHERE B.AccountId=@account ORDER BY B.BookingDay DESC,B.TransactionId",
            "Imports"=>@"SELECT TOP(500) H.BatchId,H.FileName,H.AddedCount,H.DuplicateCount,H.CreatedBy,H.CreatedUtc,H.Reason FROM dbo.BankImportBatches H WHERE H.AccountId=@account ORDER BY H.CreatedUtc DESC,H.BatchId",
            "History"=>@"SELECT TOP(500) M.MatchId,B.TransactionId,B.ExternalId,B.BookingDay,B.SignedAmount,M.LedgerId,P.RequestId,P.PersonId,P.ProjectId,M.CreatedBy,M.CreatedUtc,M.Reason,M.ReleaseId,M.ReleasedUtc,M.ReleasedBy,M.ReleaseReason,M.RecordVersion FROM dbo.BankPaymentMatches M JOIN dbo.BankTransactions B ON B.TransactionId=M.TransactionId JOIN dbo.PaymentLedger P ON P.LedgerId=M.LedgerId WHERE B.AccountId=@account ORDER BY M.CreatedUtc DESC,M.MatchId",
            "Candidates"=>@"SELECT TOP(500) P.LedgerId,P.RequestId,P.PersonId,P.TC PersonTc,P.PersonnelGroup,P.ProjectId,P.CreatedUtc PaymentUtc,-P.Delta PaymentAmount,
 CASE WHEN B.PaymentReference=P.RequestId THEN N'İşlem referansı + tutar' ELSE N'Tutar + yakın kayıt günü; kimlik kanıtı değil' END CandidateBasis
 FROM dbo.PaymentLedger P JOIN dbo.BankTransactions B ON B.TransactionId=@transaction AND B.AccountId=@account
 WHERE B.SignedAmount<0 AND P.Delta=B.SignedAmount AND P.Kind=N'Payment' AND P.ReversedUtc IS NULL AND P.RequestId IS NOT NULL AND P.PersonId IS NOT NULL
 AND(B.PaymentReference IS NULL OR B.PaymentReference=P.RequestId)
 AND ((@ledger IS NOT NULL AND P.LedgerId=@ledger) OR (@ledger IS NULL AND (B.PaymentReference=P.RequestId OR CONVERT(date,P.CreatedUtc AT TIME ZONE 'UTC' AT TIME ZONE 'Turkey Standard Time') BETWEEN DATEADD(day,-7,B.BookingDay) AND DATEADD(day,7,B.BookingDay))))
 AND NOT EXISTS(SELECT 1 FROM dbo.BankPaymentMatches M WHERE M.ReleasedUtc IS NULL AND(M.TransactionId=B.TransactionId OR M.LedgerId=P.LedgerId)) ORDER BY P.LedgerId DESC",
            _=>throw new ArgumentException("Banka liste bölümü geçersiz.")
        };
        var data=await FinancialQueryScope.ReadPageAsync(w,sql,c=>{Id(c,"@account",input.AccountId);c.Parameters.Add("@transaction",SqlDbType.UniqueIdentifier).Value=(object?)input.TransactionId??DBNull.Value;c.Parameters.Add("@ledger",SqlDbType.BigInt).Value=(object?)input.LedgerId??DBNull.Value;},input.Index,input.Size,ct);await w.CommitAsync(ct);return new(RemoteApi.Encode(data.Rows),data.Info.Total,data.Info.Index,data.Info.Size);
    }
}
