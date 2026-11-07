using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace WinFormsApp2;

internal static partial class BackupVault
{
    private const int Block=1024*1024;
    private static readonly byte[] Magic=Encoding.ASCII.GetBytes("ACCTVB01");
    private sealed record Header(int Format,Guid KeyId,string Scope,byte[] Salt,long Length,int MetadataLength,DateTime CreatedUtc);
    internal sealed class Stage:IDisposable
    {
        internal string Folder{get;}internal string? BackupPath{get;set;}private readonly FileStream lease;
        internal Stage(){PrivateDirectory(Root);PrivateDirectory(Path.Combine(Root,"stage"));Folder=Path.Combine(Root,"stage",Guid.NewGuid().ToString("N"));PrivateDirectory(Folder);lease=new FileStream(Path.Combine(Folder,"lease"),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);}
        public void Dispose(){lease.Dispose();CleanStage(Folder);}
    }
    private static void CleanStage(string folder)
    {
        string root=Path.GetFullPath(Path.Combine(Root,"stage"))+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(folder).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Guid.TryParseExact(Path.GetFileName(folder),"N",out _))throw new IOException("Geçici klasör kapsamı geçersiz.");RejectLinks(folder);
        foreach(string file in Directory.EnumerateFiles(folder))
        {
            RejectLinks(file);string name=Path.GetFileName(file);
            if(name=="lease"||name.EndsWith(".bak",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".bak.backup.json",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".bak.verified",StringComparison.OrdinalIgnoreCase))File.Delete(file);
        }
        if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
    }
    internal static int CleanStaleStages()
    {
        string root=Path.Combine(Root,"stage");RejectLinks(root);if(!Directory.Exists(root))return 0;int count=0;
        foreach(string folder in Directory.EnumerateDirectories(root))
        {
            if(!Guid.TryParseExact(Path.GetFileName(folder),"N",out _)||Directory.GetLastWriteTimeUtc(folder)>DateTime.UtcNow.AddDays(-1))continue;
            try{RejectLinks(folder);using(var lease=new FileStream(Path.Combine(folder,"lease"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){}CleanStage(folder);count++;}catch(IOException){}
        }return count;
    }
    private static bool Matches(BackupManifest m,BackupScope scope)=>m.FormatVersion==2&&m.FamilyId==scope.FamilyId&&m.ScopeKey==scope.Key&&string.Equals(m.Catalog,scope.Catalog,StringComparison.OrdinalIgnoreCase)&&string.Equals(m.Source,scope.Source,StringComparison.OrdinalIgnoreCase)&&string.Equals(m.DataFile,scope.DataFile,StringComparison.OrdinalIgnoreCase);
    private static byte[] Derive(byte[] master,byte[] salt)=>HMACSHA256.HashData(master,salt);
    private static byte[] Aad(byte[] header,int index){byte[] aad=new byte[header.Length+4];header.CopyTo(aad,0);BinaryPrimitives.WriteInt32LittleEndian(aad.AsSpan(header.Length),index);return aad;}
    private static byte[] Nonce(int index){var nonce=new byte[12];BinaryPrimitives.WriteInt32BigEndian(nonce.AsSpan(8),index);return nonce;}
    private static void WriteBlock(Stream target,AesGcm aes,ReadOnlySpan<byte> plain,byte[] header,int index)
    {byte[] cipher=new byte[plain.Length],tag=new byte[16];aes.Encrypt(Nonce(index),plain,cipher,tag,Aad(header,index));target.Write(cipher);target.Write(tag);}
    private static byte[] ReadBlock(Stream source,AesGcm aes,int length,byte[] header,int index)
    {byte[] cipher=new byte[length],tag=new byte[16],plain=new byte[length];source.ReadExactly(cipher);source.ReadExactly(tag);aes.Decrypt(Nonce(index),cipher,tag,plain,Aad(header,index));return plain;}
    internal static async Task<string> SealAsync(string raw,string destination,BackupScope scope,CancellationToken ct)
    {
        var manifest=BackupScope.ReadOwned(raw,scope.Prefix)??throw new InvalidOperationException("Şifreleme kaynağı doğrulanmadı.");if(!Matches(manifest,scope))throw new InvalidOperationException("Şifreleme kapsamı uyuşmuyor.");
        RejectLinks(destination);Directory.CreateDirectory(destination);string path=Path.Combine(Path.GetFullPath(destination),Path.GetFileName(raw)+".abk"),temp=path+"."+Guid.NewGuid().ToString("N")+".sealing";
        var material=ActiveKey();byte[] salt=RandomNumberGenerator.GetBytes(32),key=Derive(material.Key,salt);CryptographicOperations.ZeroMemory(material.Key);
        byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(manifest);var h=new Header(1,material.Id,scope.Key,salt,manifest.Length,metadata.Length,DateTime.UtcNow);byte[] header=JsonSerializer.SerializeToUtf8Bytes(h);
        if(h.Length is <1 or >68719476736L)throw new InvalidOperationException("Yedek boyutu 64 GiB sınırının dışında.");
        try
        {
            using(var input=new FileStream(raw,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var aes=new AesGcm(key,16))
            using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                output.Write(Magic);Span<byte> size=stackalloc byte[4];BinaryPrimitives.WriteInt32LittleEndian(size,header.Length);output.Write(size);output.Write(header);WriteBlock(output,aes,metadata,header,0);
                byte[] block=new byte[Block];long remaining=h.Length;int index=1;
                try{while(remaining>0){ct.ThrowIfCancellationRequested();int n=(int)Math.Min(Block,remaining);input.ReadExactly(block.AsSpan(0,n));hash.AppendData(block,0,n);WriteBlock(output,aes,block.AsSpan(0,n),header,index++);remaining-=n;}
                    if(input.ReadByte()!=-1||Convert.ToHexString(hash.GetHashAndReset())!=manifest.Sha256)throw new IOException("Yedek şifreleme sırasında değişti.");output.Flush(true);}
                finally{CryptographicOperations.ZeroMemory(block);}
            }
            // Authenticate the entire archive before deleting any plaintext.
            _=await Task.Run(()=>Validate(temp,scope,ct),ct).ConfigureAwait(false);ct.ThrowIfCancellationRequested();File.Move(temp,path,false);
            AtomicFile.Write(path+".verified",Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")),true);return path;
        }
        finally{CryptographicOperations.ZeroMemory(metadata);CryptographicOperations.ZeroMemory(key);if(File.Exists(temp))File.Delete(temp);}
    }
    private static BackupManifest Decode(string path,BackupScope? expected,Stream? destination,CancellationToken ct=default,bool metadataOnly=false)
    {
        RejectLinks(path);using var source=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        Span<byte> magic=stackalloc byte[8];source.ReadExactly(magic);if(!magic.SequenceEqual(Magic))throw new InvalidOperationException("Şifreli yedek biçimi geçersiz.");Span<byte> size=stackalloc byte[4];source.ReadExactly(size);int count=BinaryPrimitives.ReadInt32LittleEndian(size);if(count is <1 or >8192)throw new InvalidOperationException("Yedek başlığı geçersiz.");
        byte[] header=new byte[count];source.ReadExactly(header);var h=JsonSerializer.Deserialize<Header>(header)??throw new InvalidOperationException("Yedek başlığı boş.");
        if(h.Format!=1||h.KeyId==Guid.Empty||h.Salt is not { Length: 32 }||h.Length is <1 or >68719476736L||h.MetadataLength is <1 or >65536||h.Scope==null||!System.Text.RegularExpressions.Regex.IsMatch(h.Scope,"\\A[0-9A-F]{32}\\z"))throw new InvalidOperationException("Yedek boyutu/biçimi geçersiz.");
        long payloadBlocks = (h.Length + Block - 1) / Block;
        long archiveLength = source.Position + h.MetadataLength + 16 + h.Length + payloadBlocks * 16;
        if (source.Length != archiveLength)
            throw new InvalidOperationException("Şifreli yedek dosyasının uzunluğu başlığıyla uyuşmuyor.");
        if(expected!=null&&h.Scope!=expected.Key)throw new InvalidOperationException("Şifreli yedek farklı veritabanı kapsamına ait.");
        byte[] master=FindKey(h.KeyId),key=Derive(master,h.Salt);CryptographicOperations.ZeroMemory(master);
        try
        {
            using var aes=new AesGcm(key,16);byte[] meta=ReadBlock(source,aes,h.MetadataLength,header,0);BackupManifest m;
            try{m=JsonSerializer.Deserialize<BackupManifest>(meta)??throw new InvalidOperationException("Yedek manifesti boş.");}finally{CryptographicOperations.ZeroMemory(meta);}
            var scope=new BackupScope(m.Catalog,m.FamilyId,m.Source,m.DataFile);
            if(m.FormatVersion!=2||m.FamilyId==Guid.Empty||m.BackupSetId==Guid.Empty||m.Length!=h.Length||m.ScopeKey!=h.Scope||scope.Key!=h.Scope||(expected!=null&&!Matches(m,expected)))throw new InvalidOperationException("Şifreli yedek kimliği uyuşmuyor.");
            if(metadataOnly)return m;
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long remaining=h.Length;int index=1;
            while(remaining>0){ct.ThrowIfCancellationRequested();int n=(int)Math.Min(Block,remaining);byte[] plain=ReadBlock(source,aes,n,header,index++);try{hash.AppendData(plain);destination?.Write(plain);}finally{CryptographicOperations.ZeroMemory(plain);}remaining-=n;}
            if(source.ReadByte()!=-1||Convert.ToHexString(hash.GetHashAndReset())!=m.Sha256)throw new InvalidOperationException("Şifreli yedeğin tamamı doğrulanamadı.");return m;
        }finally{CryptographicOperations.ZeroMemory(key);}
    }
    internal static BackupManifest Validate(string path,BackupScope? expected=null,CancellationToken ct=default)=>Decode(path,expected,null,ct);
    internal static BackupManifest Inspect(string path,BackupScope scope)=>Decode(path,scope,null,metadataOnly:true);
    internal static async Task<Stage> OpenAsync(string path,BackupScope? expected,CancellationToken ct)
    {
        // Pin the ciphertext against replacement while validating and decrypting.
        using var pinned=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var stage=new Stage();
        try{string temporary=Path.Combine(stage.Folder,"unsealed.bak");BackupManifest m=await Task.Run(()=>{using var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None);var manifest=Decode(path,expected,output,ct);output.Flush(true);return manifest;},ct).ConfigureAwait(false);ct.ThrowIfCancellationRequested();
            string final=Path.Combine(stage.Folder,"AccountingApp-v2-"+m.ScopeKey+"-recovery.bak");File.Move(temporary,final);AtomicFile.Write(final+BackupScope.ManifestSuffix,JsonSerializer.SerializeToUtf8Bytes(m),true);AtomicFile.Write(final+".verified",Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")),true);stage.BackupPath=final;return stage;
        }catch{stage.Dispose();throw;}
    }
    internal static bool Owns(string path,string prefix)
    {try{return Path.GetFileName(path).StartsWith(prefix,StringComparison.Ordinal)&&Validate(path).ScopeKey==prefix[17..^1];}catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException or InvalidOperationException or ArgumentException){return false;}}
}
