using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
namespace WinFormsApp2;

// Keys never travel through HTTP, logs, preferences or the diagnostic package.
internal static partial class BackupVault
{
    internal static string Root=>Path.Combine(UserPreferences.DirectoryPath,"backup-vault");
    private static string KeysPath=>Path.Combine(Root,"keys.dpapi");
    private static readonly byte[] Entropy=Encoding.UTF8.GetBytes("AccountingApp.BackupVault.Keys.v1");
    internal static void RejectLinks(string path)
    {
        for(var current=new DirectoryInfo(Path.GetFullPath(path));current!=null;current=current.Parent)
            if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Yedek kasası bağlantı/junction içeren bir yol kullanamaz.");
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Yedek kasası bağlantı dosyası kullanamaz.");
    }
    internal static void PrivateDirectory(string path)
    {
        RejectLinks(path);Directory.CreateDirectory(path);var security=new DirectorySecurity();security.SetAccessRuleProtection(true,false);
        foreach(var sid in new[]{WindowsIdentity.GetCurrent().User!,new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null)})
            security.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(path).SetAccessControl(security);
    }
    private sealed record Ring(Guid Active,Dictionary<Guid,byte[]> Keys):IDisposable
    {public void Dispose(){foreach(var key in Keys.Values)CryptographicOperations.ZeroMemory(key);}}
    private static FileStream KeyLock()
    {PrivateDirectory(Root);var path=Path.Combine(Root,"keys.lock");RejectLinks(path);return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    private static Ring LoadRing(bool create)
    {
        RejectLinks(KeysPath);
        if(!File.Exists(KeysPath))
        {
            if(!create)throw new InvalidOperationException("Kurtarma anahtarı yok. Bu Windows hesabına parolalı anahtar yedeğini içe aktarın.");
            var id=Guid.NewGuid();var ring=new Ring(id,new(){{id,RandomNumberGenerator.GetBytes(32)}});SaveRing(ring);return ring;
        }
        if(new FileInfo(KeysPath).Length>65536)throw new InvalidOperationException("Anahtar dosyası geçersiz.");
        byte[] raw=ProtectedData.Unprotect(File.ReadAllBytes(KeysPath),Entropy,DataProtectionScope.CurrentUser);
        try{var ring=JsonSerializer.Deserialize<Ring>(raw)??throw new InvalidOperationException("Anahtar dosyası boş.");ValidateRing(ring);return ring;}finally{CryptographicOperations.ZeroMemory(raw);}
    }
    private static void ValidateRing(Ring ring)
    {if(ring.Active==Guid.Empty||ring.Keys.Count is <1 or >64||!ring.Keys.ContainsKey(ring.Active)||ring.Keys.Any(p=>p.Key==Guid.Empty||p.Value.Length!=32))throw new InvalidOperationException("Anahtar kümesi geçersiz.");}
    private static void SaveRing(Ring ring)
    {ValidateRing(ring);byte[] raw=JsonSerializer.SerializeToUtf8Bytes(ring);try{AtomicFile.Write(KeysPath,ProtectedData.Protect(raw,Entropy,DataProtectionScope.CurrentUser),true);}finally{CryptographicOperations.ZeroMemory(raw);}}
    private static (Guid Id,byte[] Key) ActiveKey()
    {using var gate=KeyLock();using var ring=LoadRing(true);return(ring.Active,ring.Keys[ring.Active].ToArray());}
    private static byte[] FindKey(Guid id)
    {using var gate=KeyLock();using var ring=LoadRing(false);return ring.Keys.TryGetValue(id,out var key)?key.ToArray():throw new InvalidOperationException("Bu arşivin anahtarı kasada bulunmuyor; eski anahtar yedeğini içe aktarın.");}
    internal static Guid RotateKey()
    {
        using var gate=KeyLock();using var ring=LoadRing(true);if(ring.Keys.Count>=64)throw new InvalidOperationException("Anahtar sınırına ulaşıldı; eski anahtarlar otomatik silinmez.");
        var id=Guid.NewGuid();ring.Keys.Add(id,RandomNumberGenerator.GetBytes(32));SaveRing(ring with{Active=id});return id;
    }
    internal static (bool Available,Guid? Active,DateTime? ExportedUtc) KeyStatus()
    {
        if(!File.Exists(KeysPath))return(false,null,null);
        try{using var gate=KeyLock();using var ring=LoadRing(false);DateTime? exported=null;string path=Path.Combine(Root,"key-export.json");RejectLinks(path);
            if(File.Exists(path)&&new FileInfo(path).Length<4096){var e=JsonSerializer.Deserialize<KeyExportState>(File.ReadAllBytes(path));if(e?.KeyId==ring.Active)exported=e.ExportedUtc;}return(true,ring.Active,exported);}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or InvalidOperationException){return(false,null,null);}
    }
    private sealed record KeyExportState(Guid KeyId,DateTime ExportedUtc);
    private sealed record WrappedKeys(int Format,int Iterations,byte[] Salt,byte[] Nonce,byte[] Cipher,byte[] Tag);
    private static void Password(string password){if(password.Length is <16 or >256||password.Any(char.IsControl))throw new ArgumentException("Anahtar yedeği parolası 16–256 karakter olmalı.");}
    internal static void ExportKeys(string destination,string password)
    {
        Password(password);string path=Path.GetFullPath(destination);if(!path.EndsWith(".abkey",StringComparison.OrdinalIgnoreCase)||File.Exists(path))throw new ArgumentException("Yeni bir .abkey dosyası seçin; mevcut dosyanın üzerine yazılmaz.");RejectLinks(path);
        using var gate=KeyLock();using var ring=LoadRing(true);byte[] raw=JsonSerializer.SerializeToUtf8Bytes(ring),salt=RandomNumberGenerator.GetBytes(32),nonce=RandomNumberGenerator.GetBytes(12),cipher=new byte[raw.Length],tag=new byte[16];
        byte[] key=Rfc2898DeriveBytes.Pbkdf2(password,salt,600000,HashAlgorithmName.SHA256,32);
        try{using var aes=new AesGcm(key,16);aes.Encrypt(nonce,raw,cipher,tag,Entropy);byte[] payload=JsonSerializer.SerializeToUtf8Bytes(new WrappedKeys(1,600000,salt,nonce,cipher,tag));
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(payload);output.Flush(true);}
            AtomicFile.Write(Path.Combine(Root,"key-export.json"),JsonSerializer.SerializeToUtf8Bytes(new KeyExportState(ring.Active,DateTime.UtcNow)),true);}
        finally{CryptographicOperations.ZeroMemory(raw);CryptographicOperations.ZeroMemory(key);}
    }
    internal static void ImportKeys(string source,string password)
    {
        Password(password);RejectLinks(source);if(new FileInfo(source).Length>65536)throw new ArgumentException("Anahtar yedeği boyutu geçersiz.");var e=JsonSerializer.Deserialize<WrappedKeys>(File.ReadAllBytes(source))??throw new ArgumentException("Anahtar yedeği boş.");
        if(e.Format!=1||e.Iterations!=600000||e.Salt.Length!=32||e.Nonce.Length!=12||e.Tag.Length!=16||e.Cipher.Length is <1 or >32768)throw new ArgumentException("Anahtar yedeği biçimi geçersiz.");
        byte[] key=Rfc2898DeriveBytes.Pbkdf2(password,e.Salt,e.Iterations,HashAlgorithmName.SHA256,32),raw=new byte[e.Cipher.Length];
        try{using var aes=new AesGcm(key,16);aes.Decrypt(e.Nonce,e.Cipher,e.Tag,raw,Entropy);using var imported=JsonSerializer.Deserialize<Ring>(raw)??throw new ArgumentException("Anahtar kümesi boş.");ValidateRing(imported);
            using var gate=KeyLock();using var current=File.Exists(KeysPath)?LoadRing(false):new Ring(imported.Active,new());
            foreach(var pair in imported.Keys){if(current.Keys.TryGetValue(pair.Key,out var existing)&&!CryptographicOperations.FixedTimeEquals(existing,pair.Value))throw new InvalidOperationException("Aynı kimlikte farklı anahtar bulundu; içe aktarım durduruldu.");}
            if(current.Keys.Keys.Union(imported.Keys.Keys).Count()>64)throw new InvalidOperationException("Birleşik anahtar sınırı aşılıyor.");
            foreach(var pair in imported.Keys)if(!current.Keys.ContainsKey(pair.Key))current.Keys.Add(pair.Key,pair.Value.ToArray());SaveRing(current);
        }finally{CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(raw);}
    }
}
