using System.Data;

namespace WinFormsApp2;

internal sealed partial class AccountService
{
    private async Task<string> VerifyCurrentPasswordAsync(UserSession user, string password, AccountAccess access, CancellationToken token)
    {
        if (string.IsNullOrEmpty(password) || password.Length > PasswordHasher.MaxPasswordLength) throw new ArgumentException("Mevcut parolanızı girin.");
        var credential = await VerifyCredentialAsync(user.Username, password, user, access, token).ConfigureAwait(false);
        if (credential == null) throw new UnauthorizedAccessException(LoginAttemptPolicy.ReauthenticationFailure);
        return credential.Hash;
    }

    public async Task ChangePasswordAsync(UserSession user, string currentPassword, string newPassword, string confirmation, CancellationToken token = default)
    {
        PasswordPolicy.Validate(newPassword, confirmation);
        string oldHash = await VerifyCurrentPasswordAsync(user, currentPassword, AccountAccess.ChangePassword, token).ConfigureAwait(false);
        if (await Task.Run(() => PasswordHasher.Verify(newPassword, oldHash), token).ConfigureAwait(false)) throw new ArgumentException("Yeni parola mevcut paroladan farklı olmalıdır.");
        string newHash = await Task.Run(() => PasswordHasher.Hash(newPassword), token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.LockAccountChangesAsync(work, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.ChangePassword, token).ConfigureAwait(false);
        using var update = work.Command(@"UPDATE dbo.admin SET password_hash=@hash,password=N'',MustChangePassword=0,
            PasswordResetExpiresUtc=NULL,SessionEpoch=NEWID() WHERE username=@name AND SessionVersion=@version");
        update.Text("@hash", newHash, 256); update.Text("@name", user.Username);
        update.Parameters.Add("@version", SqlDbType.Binary, 8).Value = user.SecurityVersion!.ToArray();
        if (await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new SessionRevokedException();
        await LoginThrottle.ClearAsync(work, user.SecurityEpoch, token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.PasswordChange", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Account.PasswordChange");
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(UserSession user, AccountTarget selected, string adminPassword, CancellationToken token = default)
    {
        user.DemandAdmin();
        if (string.IsNullOrWhiteSpace(selected.Username) || selected.Username.Length > 50 || selected.Version is not { Length: 8 } || selected.Epoch == Guid.Empty)
            throw new ArgumentException("Hesap listesini yenileyip sıfırlanacak hesabı yeniden seçin.");
        byte[] expectedVersion = selected.Version.ToArray();
        await VerifyCurrentPasswordAsync(user, adminPassword, AccountAccess.Admin, token).ConfigureAwait(false);
        string temporary = PasswordPolicy.Temporary();
        string hash = await Task.Run(() => PasswordHasher.Hash(temporary), token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.LockAccountChangesAsync(work, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        string canonical;
        using (var target = work.Command(@"SELECT username,IsActive FROM dbo.admin WITH(UPDLOCK,HOLDLOCK)
            WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch"))
        {
            target.Text("@name", selected.Username); target.Parameters.Add("@version", SqlDbType.Binary, 8).Value = expectedVersion;
            target.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = selected.Epoch;
            using var reader = await target.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap seçiminizden sonra değişmiş. Listeyi yenileyip hesabı yeniden seçin.");
            canonical = reader.GetString(0);
            if (!reader.GetBoolean(1)) throw new InvalidOperationException("Pasif hesabın parolası sıfırlanamaz. Hesabın durumunu kontrol edin.");
            if (string.Equals(canonical, user.Username, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Kendi hesabınız için Parolamı değiştir ekranını kullanın.");
            if (await reader.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap tekil değil.");
        }
        using var update = work.Command(@"UPDATE dbo.admin SET password_hash=@hash,password=N'',MustChangePassword=1,
            PasswordResetExpiresUtc=DATEADD(minute,30,SYSUTCDATETIME()),SessionEpoch=NEWID() WHERE username=@name");
        update.Text("@hash", hash, 256); update.Text("@name", canonical);
        if (await update.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException("Hesap tekil değil.");
        await LoginThrottle.ClearAsync(work, selected.Epoch, token).ConfigureAwait(false);
        using var expiry = work.Command("SELECT PasswordResetExpiresUtc FROM dbo.admin WHERE username=@name"); expiry.Text("@name", canonical);
        var expires = DateTime.SpecifyKind((DateTime)(await expiry.ExecuteScalarAsync(token).ConfigureAwait(false))!, DateTimeKind.Utc);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.PasswordReset", token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Account.PasswordReset");
        return new(canonical, temporary, expires); // Secret is returned only after a successful commit.
    }
}
