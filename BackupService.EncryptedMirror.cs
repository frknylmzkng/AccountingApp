using System.Security.Cryptography;
using System.Text;
namespace WinFormsApp2;
internal sealed partial class BackupService
{
    private async Task<bool> MirrorEncryptedAsync(string path,string mirror,BackupScope scope,CancellationToken ct)
    {
        await using var authorization=await SqlUnitOfWork.OpenAsync(connectionString,ct).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(authorization,user,AccountAccess.Admin,ct).ConfigureAwait(false);
        string destination=Path.GetFullPath(mirror),target=Path.Combine(destination,Path.GetFileName(path));BackupVault.RejectLinks(destination);string? temporary=null;
        var pending=new BackupMirrorState(destination,null,DateTime.UtcNow,null,"Pending");
        try
        {
            using var pin=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);await Task.Run(()=>BackupVault.Validate(path,scope,ct),ct).ConfigureAwait(false);
            string hash=await HashAsync(path,ct).ConfigureAwait(false);var old=ReadMirrorState(path);
            if(old?.CompletedUtc!=null)pending=pending with{CompletedUtc=old.CompletedUtc};
            if(string.Equals(Path.GetFullPath(path),target,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Aynalama ayrı bir klasör olmalı.");
            BackupVault.RejectLinks(target);
            await using var existing = File.Exists(target)
                ? new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true)
                : null;
            if (existing != null)
            {
                string targetHash = Convert.ToHexString(await SHA256.HashDataAsync(existing, ct).ConfigureAwait(false));
                if (targetHash != hash)
                    throw new IOException("Aynı adlı farklı aynalama dosyasının üzerine yazılmadı.");
                if (old?.Failure == null && old?.SourceHash == hash &&
                    string.Equals(old.Destination, destination, StringComparison.OrdinalIgnoreCase)) return false;
            }

            WriteMirrorState(path, pending);
            if (existing == null)
            {
                Directory.CreateDirectory(destination);
                temporary = target + "." + Guid.NewGuid().ToString("N") + ".copying";
                await using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true))
                await using (var copy = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await source.CopyToAsync(copy, ct).ConfigureAwait(false);
                    await copy.FlushAsync(ct).ConfigureAwait(false);
                    copy.Flush(true);
                }
                if (await HashAsync(temporary, ct).ConfigureAwait(false) != hash)
                    throw new IOException("Şifreli kopyanın özeti uyuşmuyor.");
                ct.ThrowIfCancellationRequested();
                File.Move(temporary, target, overwrite: false);
                temporary = null;
            }
            AtomicFile.Write(target+".verified",Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")),true);
            Prune(destination,scope.Prefix,BackupVault.Policy(scope.Key).Keep,target);await RecordAuditAsync("Backup.EncryptedMirror",ct).ConfigureAwait(false);
            WriteMirrorState(path,pending with{SourceHash=hash,CompletedUtc=DateTime.UtcNow,Failure=null});await authorization.CommitAsync(ct).ConfigureAwait(false);return true;
        }
        catch(Exception ex)
        {
            try{WriteMirrorState(path,pending with{Failure=ex is OperationCanceledException?"Cancelled":"CopyUnavailable"});}catch(IOException){}
            if(ex is OperationCanceledException or SessionRevokedException)throw;
            if(ex is IOException or UnauthorizedAccessException)throw new BackupMirrorException(path,ex);throw;
        }
        finally{if(temporary!=null&&File.Exists(temporary))File.Delete(temporary);}
    }
}
