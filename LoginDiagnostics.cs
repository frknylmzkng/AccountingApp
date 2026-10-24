using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed record LoginFailure(string Code, string Message);

    internal static class LoginDiagnostics
    {
        internal static void RecordPreparationState(string state)
        {
            TryWrite(Path.Combine(AppContext.BaseDirectory, "logs"), "login-startup.log",
                $"{DateTime.UtcNow:O} process={Environment.ProcessId} state={state}{Environment.NewLine}");
        }

        internal static LoginFailure Record(Exception exception, string operation, string? logDirectory = null)
        {
            string code = "G-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            var details = new List<string>
            {
                $"{DateTime.UtcNow:O} {code} operation={operation}"
            };
            // Exception messages can contain database values. Record only types/codes and method names.
            for (Exception? current = exception; current != null; current = current.InnerException)
            {
                details.Add($"type={current.GetType().FullName} hresult={current.HResult.ToString("X8", CultureInfo.InvariantCulture)}");
                if (current is SqlException sql)
                    foreach (SqlError error in sql.Errors)
                        details.Add($"sql.number={error.Number} state={error.State} class={error.Class}");
                if (current is Win32Exception windows)
                    details.Add($"windows.error={windows.NativeErrorCode}");
                details.Add(new StackTrace(current, false).ToString());
            }

            TryWrite(logDirectory ?? Path.Combine(AppContext.BaseDirectory, "logs"), "login-errors.log",
                string.Join(Environment.NewLine, details) + Environment.NewLine);

            string message = exception is DatabaseConfigurationException configurationError
                ? configurationError.Message
                : exception is LocalDbStartupException startupError
                ? startupError.Message
                : exception is SqlException localDbError && localDbError.Number == -1983577846
                ? "LocalDB veritabanı motoru başlatılamadı. Yeniden deneyin; sorun sürerse LocalDB kurulumunu kontrol edin."
                : exception is SqlException certificateError && certificateError.Number == -2146893019
                ? "SQL Server sertifikası bu bilgisayarda güvenilir değil. Sunucuda güvenilir bir TLS sertifikası yapılandırın veya mevcut LocalDB profilini kullanın."
                : exception is SqlException sqlError && sqlError.Number == -2
                ? "Veritabanı zamanında yanıt vermedi. Yeniden deneyin."
                : "Giriş işlemi tamamlanamadı. Yeniden deneyin; hata sürerse hata kodunu paylaşın.";
            return new LoginFailure(code, message);
        }

        private static void TryWrite(string directory, string name, string text)
        {
            try
            {
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, name), text);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Diagnostics must not prevent retrying or closing the login form.
            }
        }
    }
}
