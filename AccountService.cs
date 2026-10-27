using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed partial class AccountService(string connectionString)
{
    public async Task<UserSession?> AuthenticateAsync(string username, string password, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50 || string.IsNullOrEmpty(password) || password.Length > PasswordHasher.MaxPasswordLength) return null;
        try { return (await VerifyCredentialAsync(username.Trim(), password, null, AccountAccess.Read, token).ConfigureAwait(false))?.User; }
        catch (SessionRevokedException) { return null; } // Account changed during verification.
    }
    public async Task<DataTable> ListAsync(UserSession user, CancellationToken token)
    {
        user.DemandAdmin(); await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        using var cmd = work.Command("SELECT username,RoleName,IsActive,MustChangePassword,PasswordResetExpiresUtc,SessionVersion AS AccountVersion,SessionEpoch AS AccountEpoch FROM dbo.admin ORDER BY username");
        var result = await cmd.TableAsync(token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); return result;
    }
    public async Task AddAsync(UserSession user, string username, string password, UserRole role, CancellationToken token)
    {
        user.DemandAdmin(); username = AdminStore.ValidateNewAccount(username, password);
        PasswordPolicy.Validate(password, password);
        if (!Enum.IsDefined(role)) throw new ArgumentException("Geçersiz rol.");
        string hash = await Task.Run(() => PasswordHasher.Hash(password), token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.LockAccountChangesAsync(work, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        using var check = work.Command("SELECT COUNT(*) FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name"); check.Text("@name", username);
        if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0) throw new InvalidOperationException("Bu kullanıcı adı zaten kayıtlı.");
        using var cmd = work.Command("INSERT INTO dbo.admin(username,password,password_hash,RoleName) VALUES(@name,N'',@hash,@role)"); cmd.Text("@name", username); cmd.Text("@hash", hash, 256); cmd.Text("@role", role.ToString(), 24); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.Add", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Account.Add");
    }
    public Task DeleteAsync(UserSession user, AccountTarget? selected, CancellationToken token) => ChangeAccountAsync(user, selected, null, token);
    public Task SetActiveAsync(UserSession user, AccountTarget? selected, bool active, CancellationToken token) => ChangeAccountAsync(user, selected, active, token);

    private async Task ChangeAccountAsync(UserSession user, AccountTarget? selected, bool? active, CancellationToken token)
    {
        user.DemandAdmin();
        var target = AccountTarget.RequireSnapshot(selected);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.LockAccountChangesAsync(work, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        using var admins = work.Command("SELECT COUNT(*) FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE RoleName=N'Admin' AND IsActive=1 AND MustChangePassword=0"); int count = Convert.ToInt32(await admins.ExecuteScalarAsync(token).ConfigureAwait(false));
        Guid accountEpoch;
        string canonical;
        // The selected incarnation and version must match before any account, throttle or audit write.
        using var role = work.Command("SELECT RoleName,IsActive,MustChangePassword,SessionEpoch,username FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch");
        role.Text("@name", target.Username); role.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.Version;
        role.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = target.Epoch;
        using (var r = await role.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException(AccountTarget.ConflictMessage);
            string value = r.GetString(0); bool wasActive = r.GetBoolean(1); bool ready = !r.GetBoolean(2);
            accountEpoch = r.GetGuid(3);
            canonical = r.GetString(4);
            if (await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap tekil değil.");
            if (string.Equals(canonical, user.Username, StringComparison.OrdinalIgnoreCase) && active != true)
                throw new InvalidOperationException("Etkin oturumun hesabı silinemez veya pasife alınamaz.");
            if (value == "Admin" && wasActive && ready && active != true && count <= 1) throw new InvalidOperationException("En az bir etkin ve parolası tamamlanmış yönetici hesabı kalmalıdır.");
            if (active == wasActive) return;
        }
        using var cmd = work.Command(active.HasValue ? "UPDATE dbo.admin SET IsActive=@active WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch" : "DELETE FROM dbo.admin WHERE username=@name AND SessionVersion=@version AND SessionEpoch=@epoch");
        cmd.Text("@name", canonical); cmd.Parameters.Add("@version", SqlDbType.Binary, 8).Value = target.Version; cmd.Parameters.Add("@epoch", SqlDbType.UniqueIdentifier).Value = target.Epoch;
        if (active.HasValue) cmd.Parameters.Add("@active", SqlDbType.Bit).Value = active.Value;
        if (await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false) != 1) throw new InvalidOperationException(AccountTarget.ConflictMessage);
        if (!active.HasValue) await LoginThrottle.ClearAsync(work, accountEpoch, token).ConfigureAwait(false);
        string action = active.HasValue ? active.Value ? "Account.Activate" : "Account.Deactivate" : "Account.Delete";
        await new PersonnelService(connectionString, user).AuditAsync(work, action, token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event(action);
    }
}
