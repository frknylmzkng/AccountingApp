using System.Security.Cryptography;
using System.Text.Json;

namespace WinFormsApp2.RegressionTests;

internal static class BackupFixtures
{
    internal static BackupScope Scope(string name) => new(name, Guid.Parse("14111111-1111-1111-1111-111111111111"), "FixtureServer", @"C:\Fixture\" + name + ".mdf");
    // Filesystem-only retention fixture; never submitted to SQL RESTORE.
    internal static void Write(string path, BackupScope scope, string content)
    {
        File.WriteAllText(path, content);
        var bytes = File.ReadAllBytes(path);
        var metadata = new BackupManifest(2, scope.FamilyId, scope.Key, scope.Catalog, scope.Source, scope.DataFile,
            Guid.NewGuid(), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        File.WriteAllText(path + BackupScope.ManifestSuffix, JsonSerializer.Serialize(metadata));
    }
}
