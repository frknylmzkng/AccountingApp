using System.Data;
using System.Security.Cryptography;

namespace WinFormsApp2;

internal enum AccountAccess { Read, Edit, Admin, ChangePassword }
internal sealed class SessionRevokedException() : UnauthorizedAccessException(Notice)
{
    internal const string Notice = "Hesabınız veya yetkiniz değiştiği için oturumunuz sona erdi. Yeniden giriş yapın.";
}

internal static class AccountAuthorization
{
    // Held inside the use-case transaction: revocation waits for authorized writes
    // to finish, and no new write can rely on an old in-memory role afterwards.
    internal static async Task DemandAsync(IUnitOfWork work, UserSession user, AccountAccess access, CancellationToken token)
    {
        if (access == AccountAccess.Edit) user.DemandEdit();
        if (access == AccountAccess.Admin) user.DemandAdmin();
        if (user.SecurityVersion is not { Length: 8 } || user.SecurityEpoch == Guid.Empty) throw new SessionRevokedException();
        byte[] expected = user.SecurityVersion.ToArray();
        using var cmd = work.Command(@"SELECT TOP(2) RoleName,IsActive,SessionVersion,SessionEpoch,MustChangePassword,
            CASE WHEN PasswordResetExpiresUtc>SYSUTCDATETIME() THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END ResetValid
            FROM dbo.admin WITH(HOLDLOCK) WHERE username=@name");
        cmd.Text("@name", user.Username);
        using var reader = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
        if (!await reader.ReadAsync(token).ConfigureAwait(false) || !reader.GetBoolean(1) ||
            reader.GetString(0) != user.Role.ToString() || reader.IsDBNull(2) ||
            !CryptographicOperations.FixedTimeEquals(expected, reader.GetFieldValue<byte[]>(2)) ||
            reader.GetGuid(3) != user.SecurityEpoch) throw new SessionRevokedException();
        bool mustChange = reader.GetBoolean(4);
        if (mustChange != user.MustChangePassword || (mustChange && !reader.GetBoolean(5))) throw new SessionRevokedException();
        if (mustChange && access != AccountAccess.ChangePassword) throw new PasswordChangeRequiredException();
        if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new SessionRevokedException();
    }
    internal static async Task ValidateAsync(string connectionString, UserSession user, AccountAccess access, CancellationToken token)
    {
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await DemandAsync(work, user, access, token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false);
    }
    internal static async Task<bool> IsCurrentAsync(string connectionString, UserSession user, CancellationToken token)
    {
        try { await ValidateAsync(connectionString, user, user.MustChangePassword ? AccountAccess.ChangePassword : AccountAccess.Read, token).ConfigureAwait(false); return true; }
        catch (SessionRevokedException) { return false; }
    }
    internal static async Task LockAccountChangesAsync(IUnitOfWork work, CancellationToken token)
    {
        using var cmd = work.Command(@"DECLARE @r int; EXEC @r=sys.sp_getapplock @Resource=N'AccountingApp.AccountManagement.v1',
            @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=30000;
            IF @r<0 THROW 51024,N'Hesap işlemi kilidi alınamadı. Yeniden deneyin.',1;");
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
