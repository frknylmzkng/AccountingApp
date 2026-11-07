using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Wire=AccountingApp.Contracts;
namespace WinFormsApp2;

internal sealed class VaultManagementService(string connectionString,UserSession user)
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    private async Task<(BackupScope Scope,string Folder,string Mirror,VaultPolicy Policy)> LocationAsync(CancellationToken ct)
    {
        var scope=await BackupScope.ReadAsync(connectionString,ct);var prefs=UserPreferences.Load();var policy=BackupVault.Policy(scope.Key);
        return(scope,string.IsNullOrWhiteSpace(prefs.BackupFolder)?BackupService.DefaultFolder:prefs.BackupFolder,string.IsNullOrWhiteSpace(policy.Mirror)?prefs.MirrorFolder:policy.Mirror,policy);
    }
    internal async Task<Wire.VaultStatus> StatusAsync(CancellationToken ct)
    {
        if(DesktopConnection.IsRemote(connectionString))return await RemoteApi.For(connectionString,user).SendAsync<Wire.VaultStatus>("backup-vault/status",null,ct);
        await AccountAuthorization.ValidateAsync(connectionString,user,AccountAccess.Admin,ct);var l=await LocationAsync(ct);var key=BackupVault.KeyStatus();int count=0;DateTime? last=null,mirrorUtc=null;string mirrorState=l.Mirror.Length==0?"NotConfigured":"Pending";
        if(Directory.Exists(l.Folder))foreach(string path in Directory.EnumerateFiles(l.Folder,l.Scope.Prefix+"*.bak.abk").OrderByDescending(Path.GetFileName,StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();try{_=BackupVault.Inspect(path,l.Scope);count++;if(last==null&&File.Exists(path+".verified")){last=File.GetLastWriteTimeUtc(path+".verified");
                string statePath=path+".mirror.json";BackupVault.RejectLinks(statePath);if(File.Exists(statePath)&&new FileInfo(statePath).Length<8192){var state=JsonSerializer.Deserialize<BackupMirrorState>(File.ReadAllBytes(statePath));if(state!=null&&string.Equals(Path.GetFullPath(state.Destination),Path.GetFullPath(l.Mirror.Length==0?l.Folder:l.Mirror),StringComparison.OrdinalIgnoreCase)){mirrorUtc=state.CompletedUtc;mirrorState=state.Failure==null&&state.CompletedUtc!=null&&File.Exists(Path.Combine(l.Mirror,Path.GetFileName(path)))?"RecordedSuccess":"Pending";}}}}
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException or InvalidOperationException){mirrorState="NeedsAttention";}
        }
        return new(key.Available,key.Active,key.ExportedUtc,l.Policy.Keep,l.Mirror.Length>0,count,last,mirrorUtc,mirrorState,key.Available?(key.ExportedUtc.HasValue?"KeyExportRecorded_DrillNotRecorded":"ExportRecoveryKey_DrillNotRecorded"):"RecoveryKeyUnavailable");
    }
    internal async Task ExecuteAsync(Wire.VaultCommand command,CancellationToken ct)
    {
        _=AuditContext.ValidateReason(command.Reason);if(command.Operation is not("Backup" or "Verify" or "RetryMirror" or "Retention"))throw new ArgumentException("Kasa işlemi geçersiz.");
        if(DesktopConnection.IsRemote(connectionString)){await RemoteApi.For(connectionString,user).Done("backup-vault/command",command,ct);return;}
        using var reason=AuditContext.UseReason(command.Reason);await Gate.WaitAsync(ct);
        try
        {
            await using var authorization=await SqlUnitOfWork.OpenAsync(connectionString,ct);await AccountAuthorization.DemandAsync(authorization,user,AccountAccess.Admin,ct);
            var l=await LocationAsync(ct);var backup=new BackupService(connectionString,user);
            switch(command.Operation)
            {
                case "Backup":await backup.BackupAsync(l.Folder,l.Mirror,ct);break;
                case "RetryMirror":await backup.RetryMirrorAsync(l.Folder,l.Mirror,ct);break;
                case "Retention":BackupVault.SavePolicy(l.Scope.Key,command.Keep);break; // Cleanup starts after the next successful backup.
                case "Verify":
                    string path=Directory.Exists(l.Folder)?Directory.EnumerateFiles(l.Folder,l.Scope.Prefix+"*.bak.abk").OrderByDescending(Path.GetFileName,StringComparer.Ordinal).FirstOrDefault()??"":"";
                    if(path.Length==0)throw new InvalidOperationException("Şifreli yedek bulunamadı.");using(var opened=await BackupVault.OpenAsync(path,l.Scope,ct))
                    {var b=new SqlConnectionStringBuilder(connectionString){InitialCatalog="master",AttachDBFilename="",Pooling=false};await using var master=new SqlConnection(b.ConnectionString);await master.OpenAsync(ct);using var verify=new SqlCommand("RESTORE VERIFYONLY FROM DISK=N'"+opened.BackupPath!.Replace("'","''")+"' WITH CHECKSUM",master){CommandTimeout=180};await verify.ExecuteNonQueryAsync(ct);}
                    break;
            }
            await new PersonnelService(connectionString,user).AuditAsync(authorization,"BackupVault."+command.Operation,ct);await authorization.CommitAsync(ct);
        }finally{Gate.Release();}
    }
}
