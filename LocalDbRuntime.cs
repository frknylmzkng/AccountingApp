using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class LocalDbStartupException(string message, Exception? inner = null) : Exception(message, inner);

internal static class LocalDbRuntime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    internal static string? InstanceName(string connectionString)
    {
        string server = new SqlConnectionStringBuilder(connectionString).DataSource;
        const string prefix = @"(LocalDB)\";
        if (!server.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        string instance = server[prefix.Length..];
        // A shared alias belongs to its owner; do not try to start it as a private instance.
        if (instance.StartsWith(@".\", StringComparison.Ordinal)) return null;
        if (instance.Length is < 1 or > 128 || instance[0] == '-' || instance.Any(c => char.IsControl(c) || c is '\\' or '/' or '"'))
            throw new LocalDbStartupException("LocalDB örnek adı geçersiz. Bağlantı profilini kontrol edin.");
        return instance;
    }

    internal static ProcessStartInfo StartInfo(string executable, string instance)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("start"); start.ArgumentList.Add(instance); return start;
    }

    internal static string FindUtility()
    {
        // Resolve only from the installed SQL Server directory, never a writable PATH entry or the app folder.
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft SQL Server");
        if (Directory.Exists(root))
            foreach (string directory in Directory.EnumerateDirectories(root).Where(d => int.TryParse(Path.GetFileName(d), out _)).OrderByDescending(d => int.Parse(Path.GetFileName(d))))
            {
                string tool = Path.Combine(directory, "Tools", "Binn", "SqlLocalDB.exe");
                if (File.Exists(tool)) return tool;
            }
        throw new LocalDbStartupException("LocalDB başlatma aracı bulunamadı. SQL Server Express LocalDB kurulumunu kontrol edin.");
    }

    public static async Task EnsureStartedAsync(string connectionString, CancellationToken token = default)
    {
        string? instance = InstanceName(connectionString); if (instance == null) return;
        await EnsureStartedAsync(instance, () => FindUtility(), RunAsync, token).ConfigureAwait(false);
    }

    internal static async Task EnsureStartedAsync(string instance, Func<string> findUtility,
        Func<ProcessStartInfo, CancellationToken, Task<int>> run, CancellationToken token)
    {
        await Gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            int result = await run(StartInfo(findUtility(), instance), token).ConfigureAwait(false);
            if (result != 0) throw new LocalDbStartupException("LocalDB başlatılamadı. Yeniden deneyin; sorun sürerse LocalDB kurulumunu kontrol edin.");
        }
        catch (Exception ex) when (ex is Win32Exception or IOException)
        { throw new LocalDbStartupException("LocalDB başlatma aracı çalıştırılamadı. LocalDB kurulumunu ve dosya erişimini kontrol edin.", ex); }
        finally { Gate.Release(); }
    }

    private static async Task<int> RunAsync(ProcessStartInfo start, CancellationToken token)
    {
        using var process = Process.Start(start) ?? throw new LocalDbStartupException("LocalDB başlatma aracı açılamadı.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(); Task<string> error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            // Stop only our helper, never the database server or any other user's process.
            try { if (!process.HasExited) process.Kill(entireProcessTree: false); } catch (InvalidOperationException) { }
            if (token.IsCancellationRequested) throw;
            throw new LocalDbStartupException("LocalDB zamanında başlatılamadı. Bir süre sonra yeniden deneyin.");
        }
        await Task.WhenAll(output, error).ConfigureAwait(false); // Drain without writing command output or local paths to logs.
        return process.ExitCode;
    }
}
