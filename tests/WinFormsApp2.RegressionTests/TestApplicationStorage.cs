using Microsoft.Win32;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[SetUpFixture]
public sealed class TestApplicationStorage
{
    private string directory = "";
    private string registryPath = "";

    [OneTimeSetUp]
    public void SetUp()
    {
        string id = Guid.NewGuid().ToString("N");
        directory = Path.Combine(Path.GetTempPath(), "AccountingAppTest-" + id);
        registryPath = @"Software\AccountingAppTest-" + id;
        Directory.CreateDirectory(directory);
        UserPreferences.TestDirectory = directory;
        UserPreferences.TestRegistryPath = registryPath;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        UserPreferences.TestDirectory = null;
        UserPreferences.TestRegistryPath = null;
        if (registryPath.Length != 0)
            Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false);
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
