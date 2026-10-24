using System.Text;
using Microsoft.Data.SqlClient;
using WinFormsApp2;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
string configurationPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (args.Length == 1 && args[0] == "--help")
{
    Console.WriteLine("AccountingApp.DatabaseSetup.exe [--config appsettings.json]");
    return 0;
}
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] != "--config")
    {
        Console.Error.WriteLine("Kullanım: AccountingApp.DatabaseSetup.exe [--config appsettings.json]");
        return 2;
    }
    configurationPath = args[1];
}

try
{
    DatabaseSettings settings = DatabaseSettings.Load(configurationPath);
    Console.WriteLine("Veritabanı kurulumu kontrol ediliyor. Mevcut dosyalar veya hesaplar üzerine yazılmaz.");
    DatabaseSetupResult result = new DatabaseInstaller(settings).Install(() =>
    {
        Console.Write("İlk yönetici kullanıcı adı: ");
        string username = Console.ReadLine() ?? "";
        Console.Write("İlk yönetici şifresi: ");
        string password = ReadPassword();
        Console.Write("Şifre tekrar: ");
        if (!string.Equals(password, ReadPassword(), StringComparison.Ordinal))
            throw new DatabaseSetupException("Şifreler eşleşmedi. Kurulum değişiklikleri geri alındı; yeniden deneyin.");
        AdminStore.ValidateNewAccount(username, password);
        return new InitialAdminAccount(username, password);
    });
    Console.WriteLine($"Kurulum tamamlandı. Uygulanan betik: {result.AppliedMigrations}.");
    Console.WriteLine(result.AdminCreated ? "İlk yönetici güvenli parola hash'iyle oluşturuldu." : "Mevcut yönetici hesapları korundu.");
    return 0;
}
catch (Exception ex) when (ex is DatabaseConfigurationException or DatabaseSetupException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
catch (SqlException ex) when (ex.Number == 51011)
{
    Console.Error.WriteLine("Bu veritabanı mevcut eski şemayı içeriyor. Kurulum uygulanmadı; mevcut veritabanı ayarlarıyla uygulamayı açın.");
    return 1;
}
catch (ArgumentException)
{
    Console.Error.WriteLine("Kullanıcı adı 1–50, şifre 6–1024 karakter olmalıdır. Kurulum değişiklikleri geri alındı.");
    return 1;
}
catch (Exception ex)
{
    LoginFailure failure = LoginDiagnostics.Record(ex, "database-setup");
    Console.Error.WriteLine("Kurulum tamamlanamadı. Hata kodu: " + failure.Code);
    return 1;
}

static string ReadPassword()
{
    // Redirected input is intended for automation; interactive terminal entry is masked.
    if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
    var password = new StringBuilder();
    while (true)
    {
        ConsoleKeyInfo key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return password.ToString();
        }
        if (key.Key == ConsoleKey.Backspace && password.Length > 0)
        {
            password.Length--;
            Console.Write("\b \b");
        }
        else if (!char.IsControl(key.KeyChar))
        {
            password.Append(key.KeyChar);
            Console.Write('*');
        }
    }
}
