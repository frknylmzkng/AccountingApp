using System.Data;
using System.Security.Cryptography;

namespace WinFormsApp2;

internal sealed class PasswordChangeRequiredException() : UnauthorizedAccessException("Devam etmeden önce parolanızı değiştirin.") { }
internal static class PasswordPolicy
{
    internal const int MinimumLength = 15;
    internal const string Description = "Yeni parola 15–1024 karakter olmalı. Boşluklar korunur; uzun bir parola cümlesi kullanabilirsiniz.";
    internal static void Validate(string password, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinimumLength || password.Length > PasswordHasher.MaxPasswordLength)
            throw new ArgumentException(Description);
        if (!string.Equals(password, confirmation, StringComparison.Ordinal)) throw new ArgumentException("Yeni parolalar eşleşmiyor.");
    }
    internal static string Temporary() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
}
internal sealed record AccountTarget(string Username, byte[] Version, Guid Epoch)
{
    internal const string VersionColumn = "AccountVersion";
    internal const string EpochColumn = "AccountEpoch";
    internal static AccountTarget FromGrid(DataGridView grid)
    {
        if (!RecordSelection.TryGetKey(grid, "username", out string name) || grid.SelectedRows[0].DataBoundItem is not DataRowView row ||
            !row.Row.Table.Columns.Contains(VersionColumn) || !row.Row.Table.Columns.Contains(EpochColumn) ||
            row.Row[VersionColumn, DataRowVersion.Original] is not byte[] { Length: 8 } version ||
            row.Row[EpochColumn, DataRowVersion.Original] is not Guid epoch || epoch == Guid.Empty)
            throw new InvalidOperationException("Hesap listesini yenileyip sıfırlanacak hesabı yeniden seçin.");
        return new(name, version.ToArray(), epoch);
    }
}
internal sealed class PasswordResetResult(string username, string temporaryPassword, DateTime expiresUtc)
{
    internal string Username { get; } = username;
    internal string TemporaryPassword { get; } = temporaryPassword;
    internal DateTime ExpiresUtc { get; } = expiresUtc;
    public override string ToString() => "Parola sıfırlama sonucu (gizli)";
}
