using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("LocalDb"), NonParallelizable]
public class DatabaseSetupTests
{
    private const string MasterConnection = @"Data Source=(LocalDB)\MSSQLLocalDB;Initial Catalog=master;Integrated Security=True;Encrypt=False;Pooling=False";
    private string directory = "";
    private DatabaseSettings settings = null!;
    private const string TestPassword = " Güçlü test şifresi 123! ";
    private static InitialAdminAccount Account() => new("setup-admin", TestPassword);

    [SetUp]
    public void SelectIsolatedDatabase()
    {
        string name = "AccountingAppSetupRegression_" + Guid.NewGuid().ToString("N");
        directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "SetupDatabases", "it's a test folder", name);
        Directory.CreateDirectory(directory);
        settings = new DatabaseSettings(@"(LocalDB)\MSSQLLocalDB", name, Path.Combine(directory, "Test.mdf"), 15);
    }

    [TearDown]
    public void DropOnlyThisTestsDatabase()
    {
        SqlConnection.ClearAllPools();
        using var connection = new SqlConnection(MasterConnection);
        connection.Open();
        using var drop = new SqlCommand($@"
            IF DB_ID(N'{settings.Name}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{settings.Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{settings.Name}];
            END;", connection);
        drop.ExecuteNonQuery();
        Directory.Delete(directory, recursive: true);
    }

    [Test]
    public void FreshInstallCreatesUsableSchemaAndHashedInitialAdmin()
    {
        var result = new DatabaseInstaller(settings).Install(Account);
        Assert.That(result, Is.EqualTo(new DatabaseSetupResult(true, 1, true)));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.SchemaMigrations"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT password FROM dbo.admin"), Is.EqualTo(""));
        Assert.That(PasswordHasher.Verify(TestPassword, (string)Scalar("SELECT password_hash FROM dbo.admin")!), Is.True);
        Assert.That(new AdminStore(settings.CreateConnectionString()).Authenticate("setup-admin", TestPassword), Is.True);
        var store = new PersonnelStore(settings.CreateConnectionString());
        var person = new PersonnelDetails(11111111111L, "Ada", "Test", "TR000000000000000000000000", 1500.50m);
        store.Add(PersonnelGroup.General, person);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        store.SaveLeave(person.Tc, new LeavePeriod(new DateTime(2026, 1, 10), new DateTime(2026, 1, 12)));
        Assert.That(Scalar("SELECT IzinSuresi FROM dbo.izinler"), Is.EqualTo(3));
        store.Update(PersonnelGroup.General, person with { Amount = 2000m });
        Assert.That(Scalar("SELECT IzinSuresi FROM dbo.izinler"), Is.EqualTo(3));
        store.Delete(PersonnelGroup.General, person.Tc);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.Zero);
        Assert.Throws<SqlException>(() => Execute("UPDATE dbo.admin SET password=N'plaintext'"));
    }

    [Test]
    public void ReinstallPreservesExistingAccountWithoutAskingForAnotherPassword()
    {
        var installer = new DatabaseInstaller(settings);
        installer.Install(Account);
        object? hash = Scalar("SELECT password_hash FROM dbo.admin");
        var result = installer.Install(() => throw new Exception("Must not prompt again"));
        Assert.That(result, Is.EqualTo(new DatabaseSetupResult(false, 0, false)));
        Assert.That(Scalar("SELECT password_hash FROM dbo.admin"), Is.EqualTo(hash));
    }

    [Test]
    public async Task ConcurrentInstallationsCreateOnlyOneSchemaAndInitialAdmin()
    {
        int prompts = 0;
        InitialAdminAccount Request() { Interlocked.Increment(ref prompts); return Account(); }
        var results = await Task.WhenAll(
            Task.Run(() => new DatabaseInstaller(settings).Install(Request)),
            Task.Run(() => new DatabaseInstaller(settings).Install(Request)));
        Assert.That(results.Count(r => r.DatabaseCreated), Is.EqualTo(1));
        Assert.That(results.Count(r => r.AdminCreated), Is.EqualTo(1));
        Assert.That(prompts, Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.SchemaMigrations"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
    }

    [Test]
    public void FailedMigrationRollsBackSchemaAndHistoryAndCanBeRetried()
    {
        var migrations = DatabaseInstaller.LoadMigrations().Concat(new[]
        {
            new DatabaseMigration(2, "CREATE TABLE dbo.ShouldRollBack(Id int); THROW 51012, 'Deliberate migration failure', 1;")
        }).ToArray();
        Assert.Throws<SqlException>(() => new DatabaseInstaller(settings, migrations).Install(Account));
        Assert.That(Scalar("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"), Is.Zero);
        Assert.That(new DatabaseInstaller(settings).Install(Account).AdminCreated, Is.True);
    }

    [Test]
    public void InvalidInitialAdminRollsBackEntireSchema()
    {
        Assert.Throws<ArgumentException>(() => new DatabaseInstaller(settings).Install(() => new("admin", "short")));
        Assert.That(Scalar("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0"), Is.Zero);
    }

    [Test]
    public void ChangedMigrationIsRejectedWithoutChangingAccount()
    {
        new DatabaseInstaller(settings).Install(Account);
        var changed = new[] { new DatabaseMigration(1, DatabaseInstaller.LoadMigrations()[0].Script + "\n-- modified") };
        Assert.Throws<DatabaseSetupException>(() => new DatabaseInstaller(settings, changed).Install(Account));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
    }

    [Test]
    public void PlatformLineEndingChangesDoNotInvalidateAnAppliedMigration()
    {
        new DatabaseInstaller(settings).Install(Account);
        var sameScripts = DatabaseInstaller.LoadMigrations().Select(m => m with
        {
            Script = m.Script.Replace("\r\n", "\n").Replace("\n", "\r\n")
        }).ToArray();
        var result = new DatabaseInstaller(settings, sameScripts).Install(() => throw new Exception("Must not prompt"));
        Assert.That(result.AppliedMigrations, Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
    }

    [Test]
    public void ExistingFileCannotBeOverwritten()
    {
        File.WriteAllText(settings.FilePath, "must remain unchanged");
        Assert.Throws<DatabaseSetupException>(() => new DatabaseInstaller(settings).Install(Account));
        Assert.That(File.ReadAllText(settings.FilePath), Is.EqualTo("must remain unchanged"));
    }

    [Test]
    public void LegacyUnnamedAttachmentCannotBeInitializedByAccident()
    {
        Assert.Throws<DatabaseSetupException>(() => new DatabaseInstaller(settings with { Name = null }).Install(Account));
        Assert.That(File.Exists(settings.FilePath), Is.False);
    }

    [Test]
    public void ExistingDatabaseNameCannotBeRedirectedToAnotherFile()
    {
        new DatabaseInstaller(settings).Install(Account);
        Assert.Throws<DatabaseSetupException>(() => new DatabaseInstaller(settings with
        {
            FilePath = Path.Combine(directory, "Other.mdf")
        }).Install(Account));
        Assert.That(File.Exists(Path.Combine(directory, "Other.mdf")), Is.False);
    }

    [Test]
    public void UnmanagedExistingSchemaIsNotAdoptedOrAltered()
    {
        new DatabaseInstaller(settings).Install(Account);
        Execute("DROP TABLE dbo.SchemaMigrations");
        Assert.Throws<SqlException>(() => new DatabaseInstaller(settings).Install(Account));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
        Assert.That(Scalar("SELECT OBJECT_ID(N'dbo.SchemaMigrations')"), Is.EqualTo(DBNull.Value));
    }

    [Test]
    public void UnknownFutureSchemaIsRejected()
    {
        new DatabaseInstaller(settings).Install(Account);
        Execute("INSERT INTO dbo.SchemaMigrations(Version,ScriptSha256) VALUES(99,REPLICATE('0',64))");
        Assert.Throws<DatabaseSetupException>(() => new DatabaseInstaller(settings).Install(Account));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
    }

    [Test]
    public async Task ConsoleSetupAndPowerShellWrapperWorkWithoutPrintingPassword()
    {
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "WinFormsApp2.sln"))) root = Directory.GetParent(root)!.FullName;
        string configuration = Path.Combine(directory, "appsettings.json");
        File.WriteAllText(configuration, JsonSerializer.Serialize(new
        {
            Database = new { settings.Server, settings.Name, settings.FilePath, settings.ConnectTimeoutSeconds }
        }));
        string build = AppContext.BaseDirectory.Contains(Path.DirectorySeparatorChar + "Release" + Path.DirectorySeparatorChar)
            ? "Release" : "Debug";
        string executable = Path.Combine(root, "tools", "DatabaseSetup", "bin", build, "net10.0-windows", "AccountingApp.DatabaseSetup.exe");
        var setup = new ProcessStartInfo(executable);
        setup.ArgumentList.Add("--config");
        setup.ArgumentList.Add(configuration);
        string output = await Run(setup, "setup-admin\n" + TestPassword + "\n" + TestPassword + "\n");
        Assert.That(output, Does.Not.Contain(TestPassword));
        Assert.That(new AdminStore(settings.CreateConnectionString()).Authenticate("setup-admin", TestPassword), Is.True);

        var wrapper = new ProcessStartInfo("powershell.exe");
        foreach (string argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            Path.Combine(root, "database", "Install-Database.ps1"), "-ConfigurationPath", configuration, "-SetupExecutable", executable })
            wrapper.ArgumentList.Add(argument);
        await Run(wrapper, "");
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.admin"), Is.EqualTo(1));
    }

    private static async Task<string> Run(ProcessStartInfo start, string input)
    {
        start.UseShellExecute = false;
        start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
        start.StandardInputEncoding = start.StandardOutputEncoding = start.StandardErrorEncoding = new UTF8Encoding(false);
        using var process = Process.Start(start)!;
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> errors = process.StandardError.ReadToEndAsync();
            await process.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.That(process.ExitCode, Is.Zero, await errors);
            return await output;
        }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
    }

    private object? Scalar(string sql)
    {
        using var connection = new SqlConnection(settings.CreateConnectionString());
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        return command.ExecuteScalar();
    }

    private void Execute(string sql)
    {
        using var connection = new SqlConnection(settings.CreateConnectionString());
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
