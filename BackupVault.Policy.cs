using System.Text.Json;
namespace WinFormsApp2;
internal sealed record VaultPolicy(int Keep=14,string Mirror="");
internal static partial class BackupVault
{
    private static string PolicyPath(string scope){if(!System.Text.RegularExpressions.Regex.IsMatch(scope,"\\A[0-9A-F]{32}\\z"))throw new ArgumentException("Kasa kapsamı geçersiz.");return Path.Combine(Root,scope+".policy.json");}
    internal static VaultPolicy Policy(string scope)
    {
        string path=PolicyPath(scope);RejectLinks(path);if(!File.Exists(path))return new();if(new FileInfo(path).Length>8192)throw new InvalidOperationException("Kasa ilkesi geçersiz.");
        var p=JsonSerializer.Deserialize<VaultPolicy>(File.ReadAllBytes(path))??throw new InvalidOperationException("Kasa ilkesi boş.");if(p.Keep is <2 or >90||p.Mirror.Length>240)throw new InvalidOperationException("Kasa ilkesi geçersiz.");return p;
    }
    internal static void SavePolicy(string scope,int keep,string? mirror=null)
    {
        if(keep is <2 or >90)throw new ArgumentException("Saklama sayısı 2–90 olmalı.");using var gate=KeyLock();var old=Policy(scope);string target=mirror??old.Mirror;
        if(target.Length>240)throw new ArgumentException("Aynalama yolu çok uzun.");if(target.Length>0){target=Path.GetFullPath(target);RejectLinks(target);}
        AtomicFile.Write(PolicyPath(scope),JsonSerializer.SerializeToUtf8Bytes(new VaultPolicy(keep,target)),true);
    }
}
