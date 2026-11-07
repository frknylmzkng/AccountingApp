namespace WinFormsApp2;

internal sealed partial class AccountService
{
    internal async Task<bool> UnlockSessionAsync(UserSession expected, string password, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, expected).UnlockAsync(password, token).ConfigureAwait(false);
        if (string.IsNullOrEmpty(password) || password.Length > PasswordHasher.MaxPasswordLength) return false;
        var access = expected.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read;
        var credential = await VerifyCredentialAsync(expected.Username, password, expected, access, token).ConfigureAwait(false);
        if (credential == null) return false;
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, expected, access, token).ConfigureAwait(false);
        await new PersonnelService(connectionString, expected).AuditAsync(work, "Session.Unlock", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); return true;
    }
}
