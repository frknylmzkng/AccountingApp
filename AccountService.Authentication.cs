using System.Security.Cryptography;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class AccountService
{
    private sealed record Credential(string Hash, UserSession User, bool Active, bool ResetValid)
    {
        internal bool Eligible => Active && (!User.MustChangePassword || ResetValid);
    }
    // Missing, inactive and locked accounts still do password work. The UI uses
    // one rejection message and never exposes whether an account exists/was locked.
    private static readonly string DummyCredentialHash = PasswordHasher.Hash(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    private static Task DummyVerifyAsync(string password, CancellationToken token) => Task.Run(() => PasswordHasher.Verify(password, DummyCredentialHash), token);

    private static async Task<Credential?> ReadCredentialAsync(SqlConnection connection, string username, CancellationToken token)
    {
        using var cmd = new SqlCommand(@"SELECT TOP(2) password_hash,RoleName,IsActive,SessionVersion,username,SessionEpoch,MustChangePassword,
            CASE WHEN PasswordResetExpiresUtc>SYSUTCDATETIME() THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END ResetValid
            FROM dbo.admin WHERE username=@username", connection);
        cmd.Text("@username", username);
        using var r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await r.ReadAsync(token).ConfigureAwait(false) || r.IsDBNull(0) || !Enum.TryParse<UserRole>(r.GetString(1), out var role) || !Enum.IsDefined(role)) return null;
        var user = new UserSession(r.GetString(4), role)
        {
            SecurityVersion = r.GetFieldValue<byte[]>(3).ToArray(), SecurityEpoch = r.GetGuid(5), MustChangePassword = r.GetBoolean(6)
        };
        var result = new Credential(r.GetString(0), user, r.GetBoolean(2), r.GetBoolean(7));
        return await r.ReadAsync(token).ConfigureAwait(false) ? null : result;
    }

    private async Task<Credential?> VerifyCredentialAsync(string username, string password, UserSession? expectedSession, AccountAccess access, CancellationToken token)
    {
        Credential? initial;
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            initial = await ReadCredentialAsync(connection, username, token).ConfigureAwait(false);
        }
        if (initial?.Eligible != true)
        {
            if (expectedSession != null) throw new SessionRevokedException();
            await DummyVerifyAsync(password, token).ConfigureAwait(false);
            AppLog.Event("Login.Rejected"); return null;
        }
        await using var gate = await LoginAttemptGate.OpenAsync(connectionString, initial.User.SecurityEpoch, token).ConfigureAwait(false);
        var credential = await ReadCredentialAsync(gate.Connection, username, token).ConfigureAwait(false);
        if (credential?.Eligible != true || credential.User.SecurityEpoch != initial.User.SecurityEpoch)
        {
            if (expectedSession != null) throw new SessionRevokedException();
            await DummyVerifyAsync(password, token).ConfigureAwait(false);
            AppLog.Event("Login.Rejected"); return null;
        }
        if (expectedSession == null) access = credential.User.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read;
        bool locked;
        await using (var check = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false))
        {
            await AccountAuthorization.DemandAsync(check, expectedSession ?? credential.User, access, token).ConfigureAwait(false);
            locked = await LoginThrottle.IsLockedAsync(check, credential.User.SecurityEpoch, token).ConfigureAwait(false);
            await check.CommitAsync(token).ConfigureAwait(false);
        }
        // The gate remains held, but all SQL transactions/row locks are closed.
        bool verified = await Task.Run(() => PasswordHasher.Verify(password, locked ? DummyCredentialHash : credential.Hash), token).ConfigureAwait(false);
        if (locked) { AppLog.Event("Login.Rejected"); return null; }

        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, credential.User, access, token).ConfigureAwait(false);
        if (await LoginThrottle.IsLockedAsync(work, credential.User.SecurityEpoch, token).ConfigureAwait(false)) return null;
        if (!verified)
        {
            bool newlyLocked = await LoginThrottle.FailureAsync(work, credential.User.SecurityEpoch, token).ConfigureAwait(false);
            await work.CommitAsync(token).ConfigureAwait(false);
            AppLog.Event(newlyLocked ? "Login.Locked" : "Login.Rejected"); return null;
        }
        await LoginThrottle.ClearAsync(work, credential.User.SecurityEpoch, token).ConfigureAwait(false);
        if (expectedSession == null)
            await new PersonnelService(connectionString, credential.User).AuditAsync(work, "Login.Success", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
        if (expectedSession == null) AppLog.Event("Login.Success");
        return credential;
    }
}
