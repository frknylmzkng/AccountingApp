using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture]
public class LocalDbRuntimeTests
{
    [TestCase(@"(LocalDB)\MSSQLLocalDB", "MSSQLLocalDB")]
    [TestCase(@"(localdb)\My Test Instance", "My Test Instance")]
    [TestCase(@".\SQLEXPRESS", null)]
    [TestCase(@"(LocalDB)\.\SharedInstance", null)]
    public void StartsOnlyPrivateLocalDbInstances(string source, string? expected)
    {
        var b = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder { DataSource = source };
        Assert.That(LocalDbRuntime.InstanceName(b.ConnectionString), Is.EqualTo(expected));
    }
    [Test]
    public async Task UsesInstalledUtilityWithoutShellAndPassesNameAsSingleArgument()
    {
        bool called = false;
        await LocalDbRuntime.EnsureStartedAsync("My Test Instance", () => @"C:\Program Files\Microsoft SQL Server\150\Tools\Binn\SqlLocalDB.exe", (info, _) =>
        {
            called = true; Assert.That(info.UseShellExecute, Is.False); Assert.That(info.CreateNoWindow, Is.True);
            Assert.That(info.ArgumentList, Is.EqualTo(new[] { "start", "My Test Instance" }));
            Assert.That(info.WorkingDirectory, Is.EqualTo(@"C:\Program Files\Microsoft SQL Server\150\Tools\Binn"));
            return Task.FromResult(0);
        }, default);
        Assert.That(called, Is.True);
    }
    [Test]
    public async Task FailureIsReportedAndCanBeRetried()
    {
        await Assert.ThrowsAsync<LocalDbStartupException>(async () => await LocalDbRuntime.EnsureStartedAsync("MSSQLLocalDB", () => @"C:\safe\SqlLocalDB.exe", (_, _) => Task.FromResult(1), default));
        await LocalDbRuntime.EnsureStartedAsync("MSSQLLocalDB", () => @"C:\safe\SqlLocalDB.exe", (_, _) => Task.FromResult(0), default);
    }
    [Test]
    public async Task CancellationDoesNotLaunchHelper()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel(); bool launched = false;
        await Assert.ThatAsync(async () => await LocalDbRuntime.EnsureStartedAsync("MSSQLLocalDB", () => "unused", (_, _) => { launched = true; return Task.FromResult(0); }, canceled.Token), Throws.InstanceOf<OperationCanceledException>());
        Assert.That(launched, Is.False);
    }
}
