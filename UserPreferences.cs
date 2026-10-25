using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace WinFormsApp2;

internal sealed class UserPreferences
{
    public bool DarkTheme { get; set; }
    public string DatabaseConfigurationPath { get; set; } = "";
    public string BackupFolder { get; set; } = "";
    public string MirrorFolder { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public static string DirectoryPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AccountingApp");
    public static UserPreferences Load(string? directory = null, string registryPath = @"Software\AccountingApp")
    {
        try { return JsonSerializer.Deserialize<UserPreferences>(File.ReadAllText(Path.Combine(directory ?? DirectoryPath, "preferences.json"))) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            try { using var key = Registry.CurrentUser.OpenSubKey(registryPath); return JsonSerializer.Deserialize<UserPreferences>(key?.GetValue("Preferences") as string ?? "{}") ?? new(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { return new(); }
        }
    }
    public void Save(string? directory = null, string registryPath = @"Software\AccountingApp")
    {
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }); bool saved = false;
        try { AtomicFile.Write(Path.Combine(directory ?? DirectoryPath, "preferences.json"), Encoding.UTF8.GetBytes(json)); saved = true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { AppLog.Event("Preferences.FileUnavailable"); }
        try { using var key = Registry.CurrentUser.CreateSubKey(registryPath); key.SetValue("Preferences", json); saved = true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { AppLog.Event("Preferences.RegistryFallbackUnavailable"); }
        if (!saved) throw new IOException("Kullanıcı ayarları dosyaya veya kayıt defterine yazılamadı.");
    }
}
internal static class AtomicFile
{
    public static void Write(string path, byte[] data, bool durable = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!); string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            if (durable)
            {
                using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(data); stream.Flush(flushToDisk: true);
            }
            else File.WriteAllBytes(temp, data);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
internal static class AppLog
{
    private static readonly Logger Logger = Create();
    private static Logger Create()
    {
        var config = new LoggingConfiguration();
        var target = new FileTarget("diagnostics") { FileName = Path.Combine(UserPreferences.DirectoryPath, "logs", "application.log"), Layout = "${longdate}|${level}|${message}", KeepFileOpen = false, ArchiveAboveSize = 1048576, MaxArchiveFiles = 7 };
        config.AddRule(LogLevel.Info, LogLevel.Fatal, target); LogManager.Configuration = config; return LogManager.GetLogger("AccountingApp");
    }
    // Only application-defined event names and exception types; never exception.Message, SQL, credentials, TC or IBAN.
    public static void Event(string eventName) => Logger.Info(eventName);
    public static string Failure(Exception ex, string operation)
    { string id = Guid.NewGuid().ToString("N")[..10]; Logger.Error("{0}|{1}|{2}", operation, ex.GetType().Name, id); return id; }
}
internal static class DraftStore
{
    private static byte[] Entropy(string scope) => SHA256.HashData(Encoding.UTF8.GetBytes("AccountingApp.Draft.v1:" + scope));
    internal static void Save(string path, string scope, object value, bool durable = false) => AtomicFile.Write(path, ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(value), Entropy(scope), DataProtectionScope.CurrentUser), durable);
    internal static T? Load<T>(string path, string scope) => JsonSerializer.Deserialize<T>(ProtectedData.Unprotect(File.ReadAllBytes(path), Entropy(scope), DataProtectionScope.CurrentUser));
}
