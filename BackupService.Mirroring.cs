using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class BackupMirrorException(string path, Exception inner)
    : IOException("Ana yedek mevcut; harici kopya tamamlanamadı. Yerel yedek korunuyor; sonraki otomatik kontrolde aynalama yeniden denenecek.", inner)
{
    internal string LocalBackupPath { get; } = path;
}

internal sealed record BackupMirrorState(string Destination, string? SourceHash, DateTime LastAttemptUtc, DateTime? CompletedUtc, string? Failure);

internal sealed partial class BackupService
{
    private static string? LatestVerified(string folder, string prefix, DateTime? since = null) => !Directory.Exists(folder) ? null :
        Directory.EnumerateFiles(folder, prefix + "*.bak")
            .Where(p => File.Exists(p + ".verified") && (!since.HasValue || File.GetLastWriteTimeUtc(p + ".verified") > since.Value))
            .OrderByDescending(p => File.GetLastWriteTimeUtc(p + ".verified")).ThenByDescending(Path.GetFileName, StringComparer.Ordinal).FirstOrDefault();

    private static BackupMirrorState? ReadMirrorState(string path)
    {
        try { return JsonSerializer.Deserialize<BackupMirrorState>(File.ReadAllText(path + ".mirror.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return null; }
    }
    private static void WriteMirrorState(string path, BackupMirrorState state) =>
        AtomicFile.Write(path + ".mirror.json", JsonSerializer.SerializeToUtf8Bytes(state), durable: true);

    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token).ConfigureAwait(false));
    }

    // Caller holds Gate. A completed copy is accepted only while both files still match.
    private async Task<bool> MirrorCoreAsync(string path, string mirror, string name, CancellationToken token)
    {
        path = Path.GetFullPath(path);
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mirror));
        string target = Path.GetFullPath(Path.Combine(destination, Path.GetFileName(path)));
        string? temporary = null;
        bool sourceVerified = false;
        var pending = new BackupMirrorState(destination, null, DateTime.UtcNow, null, "Pending");
        try
        {
            token.ThrowIfCancellationRequested();
            if (!File.Exists(path) || !File.Exists(path + ".verified")) throw new FileNotFoundException("Doğrulanmış yerel yedek bulunamadı.");
            string hash = await HashAsync(path, token).ConfigureAwait(false);
            var previous = ReadMirrorState(path);
            if (previous != null && string.Equals(previous.Destination, destination, StringComparison.OrdinalIgnoreCase))
                pending = pending with { SourceHash = previous.SourceHash, CompletedUtc = previous.CompletedUtc };
            if (previous?.CompletedUtc != null && previous.Failure == null && previous.SourceHash == hash &&
                string.Equals(previous.Destination, destination, StringComparison.OrdinalIgnoreCase) && File.Exists(target) &&
                await HashAsync(target, token).ConfigureAwait(false) == hash) return false;

            WriteMirrorState(path, pending);
            string verifiedHash = hash;
            // A legacy .verified marker is not enough when retrying: verify the source again.
            await using (var master = new SqlConnection(Master()))
            {
                await master.OpenAsync(token).ConfigureAwait(false);
                using var verify = new SqlCommand($"RESTORE VERIFYONLY FROM DISK={Literal(path)} WITH CHECKSUM", master) { CommandTimeout = 180 };
                await verify.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                using var header = new SqlCommand($"RESTORE HEADERONLY FROM DISK={Literal(path)}", master);
                var headers = await header.TableAsync(token).ConfigureAwait(false);
                if (headers.Rows.Count != 1 || !string.Equals(headers.Rows[0]["DatabaseName"].ToString(), name, StringComparison.OrdinalIgnoreCase) || Convert.ToInt32(headers.Rows[0]["BackupType"]) != 1)
                    throw new InvalidOperationException("Aynalama kaynağı bu veritabanının tam yedeği değil.");
            }
            sourceVerified = true;
            Directory.CreateDirectory(destination);
            // Hold the source against concurrent writers/deletion throughout hashing and copying.
            await using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
            {
                hash = Convert.ToHexString(await SHA256.HashDataAsync(source, token).ConfigureAwait(false));
                if (hash != verifiedHash) throw new IOException("Kaynak yedek doğrulama sırasında değişti; kopya yayımlanmadı.");
                if (!string.Equals(path, target, StringComparison.OrdinalIgnoreCase))
                {
                    temporary = target + "." + Guid.NewGuid().ToString("N") + ".copying";
                    await using (var copy = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        source.Position = 0; await source.CopyToAsync(copy, token).ConfigureAwait(false);
                        await copy.FlushAsync(token).ConfigureAwait(false); copy.Flush(flushToDisk: true);
                    }
                    if (await HashAsync(temporary, token).ConfigureAwait(false) != hash) throw new IOException("Aynalama bütünlük kontrolü başarısız.");
                    token.ThrowIfCancellationRequested(); File.Move(temporary, target, overwrite: true); temporary = null;
                }
            }
            // A retry may copy an older backup into a folder containing newer files.
            // Retention must not immediately delete the copy we just published.
            Prune(destination, Prefix(name), 14, target);
            await RecordAuditAsync("Backup.Mirror", token).ConfigureAwait(false);
            WriteMirrorState(path, pending with { SourceHash = hash, CompletedUtc = DateTime.UtcNow, Failure = null });
            return true;
        }
        catch (Exception ex)
        {
            try { WriteMirrorState(path, pending with { Failure = ex is OperationCanceledException ? "Cancelled" : ex is FileNotFoundException ? "SourceMissing" : ex is SqlException ? sourceVerified ? "AuditFailed" : "SourceVerificationFailed" : "CopyUnavailable" }); }
            catch (Exception stateError) when (stateError is IOException or UnauthorizedAccessException) { AppLog.Failure(stateError, "Backup.MirrorState"); }
            if (ex is OperationCanceledException or SessionRevokedException) throw;
            AppLog.Failure(ex, "Backup.Mirror");
            if (ex is FileNotFoundException) throw new InvalidOperationException("Yerel yedek bulunamadı; yeni bir yedek oluşturun.", ex);
            if (ex is IOException or UnauthorizedAccessException) throw new BackupMirrorException(path, ex);
            throw;
        }
        finally
        {
            if (temporary != null)
                try { File.Delete(temporary); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { AppLog.Failure(ex, "Backup.MirrorTemporary"); }
        }
    }

    public async Task RetryMirrorAsync(string folder, string mirror, CancellationToken token = default)
    {
        user.DemandAdmin(); if (string.IsNullOrWhiteSpace(mirror)) throw new ArgumentException("Önce aynalama klasörünü seçin.");
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
            string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
            string path = LatestVerified(folder, Prefix(name)) ?? throw new InvalidOperationException("Önce doğrulanmış bir yerel yedek oluşturun.");
            await MirrorCoreAsync(path, mirror, name, token).ConfigureAwait(false);
        }
        finally { Gate.Release(); }
    }

    public async Task<string> StatusAsync(string folder, string mirror, CancellationToken token = default)
    {
        await AccountAuthorization.ValidateAsync(connectionString, user, AccountAccess.Admin, token).ConfigureAwait(false);
        string name = await CatalogAsync(token).ConfigureAwait(false); ValidateCatalog(name);
        string? path = LatestVerified(folder, Prefix(name));
        if (path == null) return "Doğrulanmış yerel yedek bulunamadı.";
        string text = $"Son doğrulanmış yerel yedek: {File.GetLastWriteTimeUtc(path + ".verified").ToLocalTime():dd.MM.yyyy HH:mm}\n{path}";
        if (string.IsNullOrWhiteSpace(mirror)) return text + "\nHarici aynalama seçilmemiş.";
        var state = ReadMirrorState(path);
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(mirror));
        if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(Path.Combine(destination, Path.GetFileName(path))), StringComparison.OrdinalIgnoreCase))
            return text + "\nAynalama klasörü yerel klasörle aynı; ayrı bir harici kopya oluşturulmadı.";
        if (state?.CompletedUtc != null && state.Failure == null && string.Equals(state.Destination, destination, StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(destination, Path.GetFileName(path))))
            return text + $"\nSon başarılı aynalama: {state.CompletedUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}. Sonraki kontrol dosya bütünlüğünü yeniden sınar.";
        if (state?.CompletedUtc != null && string.Equals(state.Destination, destination, StringComparison.OrdinalIgnoreCase))
            text += $"\nSon başarılı aynalama: {state.CompletedUtc.Value.ToLocalTime():dd.MM.yyyy HH:mm}.";
        string problem = state?.Failure switch
        {
            "SourceVerificationFailed" => "Yerel kaynağın yeniden doğrulaması başarısız; yeni yerel yedek oluşturun. ",
            "AuditFailed" => "Kopya işlem kaydı tamamlanamadı; yeniden deneyin. ",
            "Cancelled" => "Son deneme iptal edildi. ",
            null or "Pending" => "",
            _ => "Son kopyalama tamamlanamadı; USB/ağ erişimini kontrol edin. "
        };
        return text + "\nHarici kopya bekliyor. " + problem + "Aynalamayı yeniden deneyin veya sonraki otomatik kontrolü bekleyin.";
    }
}
