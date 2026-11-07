using System.Security.Cryptography;
using System.Text.Json;

namespace WinFormsApp2.RegressionTests;

internal static class BackupFixtures
{
    internal static BackupScope Scope(string name) => new(name, Guid.Parse("14111111-1111-1111-1111-111111111111"), "FixtureServer", @"C:\Fixture\" + name + ".mdf");
    // Filesystem-only fixtures, never submitted to SQL RESTORE.
    internal static void Write(string path, BackupScope scope, string content)
    {
        File.WriteAllText(path, content);
        var bytes = File.ReadAllBytes(path);
        var metadata = new BackupManifest(2, scope.FamilyId, scope.Key, scope.Catalog, scope.Source, scope.DataFile,
            Guid.NewGuid(), bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
        File.WriteAllText(path + BackupScope.ManifestSuffix, JsonSerializer.Serialize(metadata));
    }

    internal static string Seal(string folder, BackupScope scope, string name)
    {
        using var stage = new BackupVault.Stage();
        string raw = Path.Combine(stage.Folder, scope.Prefix + name + ".bak");
        Write(raw, scope, "retention fixture");
        return BackupVault.SealAsync(raw, folder, scope, default).GetAwaiter().GetResult();
    }

    internal static void EnableRetention(string folder)
    {
        BackupVault.ExportKeys(Path.Combine(folder, Guid.NewGuid().ToString("N") + ".abkey"), "Test-only recovery password");
    }
}
