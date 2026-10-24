using System.Text.Json;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture]
public class DatabaseSettingsTests
{
    private string directory = "";
    private string path => Path.Combine(directory, "appsettings.json");

    [SetUp]
    public void CreateDirectory()
    {
        directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Configuration_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
    }

    [TearDown]
    public void RemoveDirectory() => Directory.Delete(directory, recursive: true);

    private void Write(object database) => File.WriteAllText(path, JsonSerializer.Serialize(new { Database = database }));

    [Test]
    public void RelativeFileIsResolvedFromConfigurationDirectoryAndConnectionRemainsProtected()
    {
        Write(new { Server = @"(LocalDB)\MSSQLLocalDB", Name = "Test;TrustServerCertificate=True", FilePath = "data/people.mdf" });
        var settings = DatabaseSettings.Load(path);
        var connection = new SqlConnectionStringBuilder(settings.CreateConnectionString(requireExistingFile: false));
        Assert.That(connection.AttachDBFilename, Is.EqualTo(Path.Combine(directory, "data", "people.mdf")));
        Assert.That(connection.InitialCatalog, Is.EqualTo("Test;TrustServerCertificate=True"));
        Assert.That(connection.IntegratedSecurity, Is.True);
        Assert.That(connection.Encrypt, Is.EqualTo(SqlConnectionEncryptOption.Mandatory));
        Assert.That(connection.TrustServerCertificate, Is.False);
        Assert.That(connection.Password, Is.Empty);
        Assert.That(connection.ConnectTimeout, Is.EqualTo(30));
    }

    [Test]
    public void LegacyAttachCanOmitDatabaseNameAndExpandUserProfile()
    {
        Write(new { Server = @"(LocalDB)\MSSQLLocalDB", Name = (string?)null, FilePath = @"%USERPROFILE%\Documents\Muhasebe2.mdf" });
        var settings = DatabaseSettings.Load(path);
        var connection = new SqlConnectionStringBuilder(settings.CreateConnectionString(requireExistingFile: false));
        Assert.That(connection.InitialCatalog, Is.Empty);
        Assert.That(connection.AttachDBFilename, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "Muhasebe2.mdf")));
    }

    [TestCase("master")]
    [TestCase("MSDB")]
    [TestCase("tempdb")]
    public void SystemDatabaseNamesAreRejected(string name)
    {
        Write(new { Server = @"(LocalDB)\MSSQLLocalDB", Name = name, FilePath = "test.mdf" });
        Assert.Throws<DatabaseConfigurationException>(() => DatabaseSettings.Load(path));
    }

    [TestCase(0)]
    [TestCase(121)]
    public void InvalidTimeoutIsRejected(int timeout)
    {
        Write(new { Server = @"(LocalDB)\MSSQLLocalDB", FilePath = "test.mdf", ConnectTimeoutSeconds = timeout });
        Assert.Throws<DatabaseConfigurationException>(() => DatabaseSettings.Load(path));
    }

    [TestCase("{}")]
    [TestCase("{broken")]
    [TestCase("{\"Database\":{\"Server\":\"remote\",\"FilePath\":\"test.mdf\"}}")]
    [TestCase("{\"Database\":{\"Server\":\"(LocalDB)\\\\MSSQLLocalDB\",\"FilePath\":\"test.ldf\"}}")]
    [TestCase("{\"Database\":{\"Server\":\"(LocalDB)\\\\MSSQLLocalDB\",\"FilePath\":\"%ACCOUNTINGAPP_UNKNOWN_VARIABLE%/test.mdf\"}}")]
    [TestCase("{\"Database\":{\"Server\":\"(LocalDB)\\\\MSSQLLocalDB\",\"FilePath\":\"test.mdf\",\"Password\":\"secret\"}}")]
    public void InvalidOrUnknownConfigurationDoesNotFallBackToAnotherDatabase(string json)
    {
        File.WriteAllText(path, json);
        Assert.Throws<DatabaseConfigurationException>(() => DatabaseSettings.Load(path));
    }

    [Test]
    public void MissingConfigurationDoesNotUseAHardcodedConnection()
    {
        Assert.Throws<DatabaseConfigurationException>(() => DatabaseSettings.Load(path));
    }

    [Test]
    public void InvalidConfigurationPathReturnsAConfigurationError()
    {
        Assert.Throws<DatabaseConfigurationException>(() => DatabaseSettings.Load("\0"));
    }

    [Test]
    public void ApplicationConnectionRefusesMissingFileWhileSetupCanBuildTheConnection()
    {
        Write(new { Server = @"(LocalDB)\MSSQLLocalDB", FilePath = "missing.mdf" });
        var settings = DatabaseSettings.Load(path);
        Assert.Throws<DatabaseConfigurationException>(() => settings.CreateConnectionString());
        Assert.DoesNotThrow(() => settings.CreateConnectionString(requireExistingFile: false));
        File.WriteAllText(settings.FilePath, "test placeholder");
        Assert.DoesNotThrow(() => settings.CreateConnectionString());
    }
}
