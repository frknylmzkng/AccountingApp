using System.Data;
using System.Text.Json;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Category("Backup")]
public class BackupScopeTests
{
    [Test]
    public void SameCatalogAndClonedLineageHaveDifferentScopesAtDifferentSourcesOrPaths()
    {
        var original = BackupFixtures.Scope("Muhasebe");
        Assert.That((original with { FamilyId = Guid.NewGuid() }).Prefix, Is.Not.EqualTo(original.Prefix));
        Assert.That((original with { Source = "AnotherInstance" }).Prefix, Is.Not.EqualTo(original.Prefix));
        Assert.That((original with { DataFile = @"C:\Clone\Muhasebe.mdf" }).Prefix, Is.Not.EqualTo(original.Prefix));
        Assert.That((original with { Catalog = original.Catalog.ToUpperInvariant(), DataFile = original.DataFile.ToUpperInvariant() }).Prefix, Is.EqualTo(original.Prefix));
    }

    private static DataTable Header(BackupScope scope)
    {
        var table = new DataTable();
        table.Columns.Add("DatabaseName"); table.Columns.Add("BackupType", typeof(short));
        table.Columns.Add("FamilyGUID", typeof(Guid)); table.Columns.Add("BackupName"); table.Columns.Add("BackupSetGUID", typeof(Guid));
        table.Rows.Add(scope.Catalog, 1, scope.FamilyId, scope.BackupName, Guid.NewGuid()); return table;
    }

    [TestCase("family"), TestCase("source"), TestCase("catalog"), TestCase("type"), TestCase("multiple")]
    public void RenamingFileOrManifestCannotMakeForeignNativeHeaderValid(string change)
    {
        var scope = BackupFixtures.Scope("SameName"); using var header = Header(scope);
        Assert.That(scope.ValidateHeader(header), Is.SameAs(header.Rows[0]));
        switch (change)
        {
            case "family": header.Rows[0]["FamilyGUID"] = Guid.NewGuid(); break;
            case "source": header.Rows[0]["BackupName"] = (scope with { Source = "CloneServer" }).BackupName; break;
            case "catalog": header.Rows[0]["DatabaseName"] = "AnotherName"; break;
            case "type": header.Rows[0]["BackupType"] = 2; break;
            default: header.ImportRow(header.Rows[0]); break;
        }
        Assert.Throws<InvalidOperationException>(() => scope.ValidateHeader(header));
    }

    [Test]
    public void RetentionPreservesForeignRenamedLegacyUnmanifestedAndCorruptFiles()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "scope-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var own = BackupFixtures.Scope("SameName"); var other = own with { FamilyId = Guid.NewGuid() };
            for (int i = 0; i < 15; i++) BackupFixtures.Write(Path.Combine(root, own.Prefix + i.ToString("D4") + ".bak"), own, "owned");
            string foreign = Path.Combine(root, own.Prefix + "9000.bak"); BackupFixtures.Write(foreign, other, "foreign");
            string legacy = Path.Combine(root, "AccountingApp-1111111111111111-old.bak"); File.WriteAllText(legacy, "legacy");
            string noManifest = Path.Combine(root, own.Prefix + "9001.bak"); File.WriteAllText(noManifest, "unknown");
            string corrupt = Path.Combine(root, own.Prefix + "9002.bak"); BackupFixtures.Write(corrupt, own, "original"); File.WriteAllText(corrupt, "corrupt");
            BackupService.Prune(root, own.Prefix, 14);
            foreach (string file in new[] { foreign, legacy, noManifest, corrupt }) Assert.That(File.Exists(file), Is.True, file);
            Assert.That(File.Exists(Path.Combine(root, own.Prefix + "0000.bak")), Is.False);
            Assert.That(Directory.GetFiles(root, "*.bak"), Has.Length.EqualTo(18));
        }
        finally { Directory.Delete(root, true); }
    }

    [Test]
    public void ManifestBindsPayloadAndRequiresKnownVersionAndCompleteIdentity()
    {
        string root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "scope-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var scope = BackupFixtures.Scope("SameName"); string path = Path.Combine(root, scope.Prefix + "0000.bak");
            BackupFixtures.Write(path, scope, "backup"); Assert.That(scope.Owns(path), Is.True);
            string original = File.ReadAllText(path + BackupScope.ManifestSuffix);
            var metadata = JsonSerializer.Deserialize<BackupManifest>(original)!;
            foreach (var changed in new[] { metadata with { FormatVersion = 99 }, metadata with { Catalog = null! }, metadata with { BackupSetId = Guid.Empty } })
            {
                File.WriteAllText(path + BackupScope.ManifestSuffix, JsonSerializer.Serialize(changed)); Assert.That(scope.Owns(path), Is.False);
            }
            File.WriteAllText(path + BackupScope.ManifestSuffix, original); File.WriteAllText(path, "different payload");
            Assert.That(scope.Owns(path), Is.False);
        }
        finally { Directory.Delete(root, true); }
    }
}

public partial class GuideDatabaseTests
{
    [Test, Category("FullRestore")]
    public async Task RestoreRejectsWrongManifestBeforeSafetyBackupOrDatabaseChanges()
    {
        string folder = Path.Combine(directory, "wrong-scope"); var backup = new BackupService(connection, admin);
        string path = await backup.BackupAsync(folder); string metadataPath = path + BackupScope.ManifestSuffix;
        var metadata = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(metadataPath))!;
        File.WriteAllText(metadataPath, JsonSerializer.Serialize(metadata with { FamilyId = Guid.NewGuid() }));
        string safety = Path.Combine(folder, "safety"); int audits = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => backup.RestoreAsync(path, safety));
        Assert.That(Directory.Exists(safety), Is.False);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, admin, default), Is.True);
    }
}
