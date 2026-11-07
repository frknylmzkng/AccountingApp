using System.Data;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed partial class ReportCenterService
{
    internal async Task DeliverZipAsync(Wire.ReportCenterExport input,Func<byte[],CancellationToken,Task> deliver,CancellationToken ct)
    {
        var s=Validate(input.Scope);_=AuditContext.ValidateReason(input.Reason);
        using var reason=AuditContext.UseReason(input.Reason);
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AuthorizeAsync(w,s,ct,true);
        // One serializable read and stable ordering cover every part. No HTTP
        // page concatenation, inconsistent live totals or persistent PII job files.
        var readUtc=DateTime.UtcNow;var parts=new List<object>();long total=0,rawBytes=0;
        using var zipBytes=new MemoryStream();
        using(var zip=new ZipArchive(zipBytes,ZipArchiveMode.Create,true))
        {
            using var c=w.Command(Sql(s).Replace("SELECT TOP(500)","SELECT",StringComparison.Ordinal));Bind(c,s);
            using var r=await c.ExecuteReaderAsync(ct);string[] names=Enumerable.Range(0,r.FieldCount).Select(r.GetName).ToArray();
            bool more=await r.ReadAsync(ct);int number=0;
            do
            {
                using var part=new MemoryStream();int count=0;
                using(var writer=new StreamWriter(part,new UTF8Encoding(true),1024,true))
                {
                    await writer.WriteLineAsync(string.Join(';',names.Select(ReportExport.CsvCell)).AsMemory(),ct);
                    while(more&&count<10000)
                    {
                        if(++total>200000)throw new InvalidOperationException("Rapor 200.000 satırı aşıyor; tarih/ekip/kişi filtresini daraltın.");
                        var cells=new string[r.FieldCount];for(int i=0;i<cells.Length;i++){object value=r.GetValue(i);cells[i]=value is DateTime d?ReportExport.CsvCell(d.ToString("O",CultureInfo.InvariantCulture)):ReportExport.CsvCell(value);}
                        string line=string.Join(';',cells);rawBytes+=Encoding.UTF8.GetByteCount(line)+2;
                        if(rawBytes>128L*1024*1024||part.Length>16L*1024*1024)throw new InvalidOperationException("Rapor metin boyutu sınırını aşıyor; filtreyi daraltın.");
                        await writer.WriteLineAsync(line.AsMemory(),ct);count++;more=await r.ReadAsync(ct);
                    }
                }
                byte[] bytes=part.ToArray();string file=$"{s.Report}-{++number:000}.csv";var entry=zip.CreateEntry(file,CompressionLevel.Fastest);using(var stream=entry.Open())await stream.WriteAsync(bytes,ct);
                parts.Add(new{File=file,Rows=count,Sha256=Convert.ToHexString(SHA256.HashData(bytes))});
                if(zipBytes.Length>31L*1024*1024)throw new InvalidOperationException("ZIP boyutu sınırını aşıyor; filtreyi daraltın.");
            }while(more);
            var manifest=zip.CreateEntry("manifest.json");using var output=manifest.Open();await JsonSerializer.SerializeAsync(output,new{Format=1,ProjectId=s.ProjectId,Report=s.Report,ReadUtc=readUtc,ReadBy=user.Username,Scope=s,Description=Describe(s),Rows=total,Parts=parts,Note="UTC kayıt zamanı; CSV parçaları aynı transaction içindedir. Atama adları güncel karttan, talep adları donmuş hesap kaynağından gelir. Bu arşiv bir mali dönem kapanışı değildir."},cancellationToken:ct);
        }
        if(zipBytes.Length>32L*1024*1024)throw new InvalidOperationException("ZIP dosyası 32 MB sınırını aşıyor.");
        byte[] archive=zipBytes.ToArray();await AuditAsync(w,s.ProjectId,"Report.Center.Export",new{Scope=s,Rows=total,Parts=parts.Count,ReadUtc=readUtc,Sha256=Convert.ToHexString(SHA256.HashData(archive))},ct);
        // Keep current session, account and project grants leased through delivery.
        await deliver(archive,ct);await w.CommitAsync(ct);
    }
    internal async Task SaveZipAsync(Wire.ReportCenterScope scope,string destination,CancellationToken ct)
    {
        string path=Path.GetFullPath(destination);if(!string.Equals(Path.GetExtension(path),".zip",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("ZIP dosyası seçin.");
        async Task Publish(byte[] bytes,CancellationToken token)
        {
            string temp=Path.Combine(Path.GetDirectoryName(path)!,".AccountingApp-report-"+Guid.NewGuid().ToString("N")+".tmp");
            try{await File.WriteAllBytesAsync(temp,bytes,token);token.ThrowIfCancellationRequested();File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
        }
        var request=new Wire.ReportCenterExport(Validate(scope),AuditContext.ValidateReason(AuditContext.Reason??""));
        if(DesktopConnection.IsRemote(connectionString))await Publish(await RemoteApi.For(connectionString,user).ReportZipAsync(request,ct),ct);
        else await DeliverZipAsync(request,Publish,ct);
    }
}
