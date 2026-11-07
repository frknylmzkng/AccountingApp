using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[SetUpFixture]
public sealed class TestApplicationStorage
{
    private string directory = "";

    [OneTimeSetUp]
    public void SetUp()
    {
        directory = Path.Combine(Path.GetTempPath(), "AccountingAppTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        UserPreferences.TestDirectory = directory;
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        UserPreferences.TestDirectory = null;
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
