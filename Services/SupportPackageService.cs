using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;
internal sealed class SupportPackageService(string connectionString,UserSession user)
{
    internal async Task DeliverAsync(Func<byte[],CancellationToken,Task> deliver,CancellationToken ct)
    {
        await using var w=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AccountAuthorization.DemandAsync(w,user,AccountAccess.Admin,ct);
        var diagnostics=await BuildDiagnostics.ReadAsync(connectionString,user,ct);var vault=await new VaultManagementService(connectionString,user).StatusAsync(ct);
        string Safe(string value,string pattern)=>Regex.IsMatch(value,pattern)?value:"unknown";
        var info=diagnostics.Build;
        // Deliberate allowlist. Do not serialize BuildInfo, options, exceptions,
        // raw logs, URLs, database scope, account/session objects or preferences.
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(new{Format=1,CreatedUtc=DateTime.UtcNow,
            Version=Safe(info.Version,@"\A\d+\.\d+\.\d+(-dev)?\z"),Schema=Safe(info.ExpectedSchema,@"\A\d{1,5}\z"),ApiVersion=Safe(info.ApiVersion,@"\A\d+\.\d+\z"),
            Commit=Safe(info.Commit,@"\A[0-9a-fA-F]{40}\z"),SourceState=info.SourceState is "dirty" or "clean"?info.SourceState:"unknown",diagnostics.AppliedUpgrades,diagnostics.SchemaMatches,
            Runtime=Environment.Version.ToString(),WindowsBuild=Environment.OSVersion.Version.ToString(),Architecture=RuntimeInformation.ProcessArchitecture.ToString(),
            Backup=new{vault.KeyAvailable,RecoveryKeyExportRecorded=vault.KeyExportedUtc.HasValue,vault.Keep,vault.MirrorConfigured,vault.ArchiveCount,MirrorState=vault.MirrorState is "NotConfigured" or "Pending" or "RecordedSuccess"?vault.MirrorState:"NeedsAttention",RestoreDrillRecorded=false}});
        using var output=new MemoryStream();using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true))
        {
            using(var stream=zip.CreateEntry("diagnostics.json").Open())await stream.WriteAsync(data,ct);
            using(var stream=zip.CreateEntry("manifest.json").Open())await JsonSerializer.SerializeAsync(stream,new{Format=1,File="diagnostics.json",Sha256=Convert.ToHexString(SHA256.HashData(data)),IncludesLogs=false,IncludesPersonalData=false},cancellationToken:ct);
            using var writer=new StreamWriter(zip.CreateEntry("README.txt").Open(),Encoding.UTF8);await writer.WriteAsync("Bu paket yalnız izin verilen sürüm/şema/ortam ve kasa durum alanlarını içerir. Log, kişi, hesap, IBAN, yol, adres, veritabanı veya anahtar içermez. Kurtarma provası yapıldığını doğrulamaz. Paylaşmadan önce ZIP içeriğini inceleyebilirsiniz.");
        }
        await new PersonnelService(connectionString,user).AuditAsync(w,"Support.SafeExport",ct);await deliver(output.ToArray(),ct);await w.CommitAsync(ct);
    }
    internal async Task SaveAsync(string destination,CancellationToken ct)
    {
        string path=Path.GetFullPath(destination);if(!path.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("ZIP dosyası seçin.");
        async Task Publish(byte[] bytes,CancellationToken token){string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{await File.WriteAllBytesAsync(temp,bytes,token);token.ThrowIfCancellationRequested();File.Move(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
        if(DesktopConnection.IsRemote(connectionString))await Publish(await RemoteApi.For(connectionString,user).SafeSupportAsync(ct),ct);else await DeliverAsync(Publish,ct);
    }
}
