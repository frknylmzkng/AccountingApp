using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal sealed class AccountService(string connectionString)
{
    public async Task<UserSession?> AuthenticateAsync(string username, string password, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50 || string.IsNullOrEmpty(password) || password.Length > PasswordHasher.MaxPasswordLength) return null;
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(token).ConfigureAwait(false);
        string? hash; UserRole role;
        using (var cmd = new SqlCommand("SELECT TOP(2) password_hash,RoleName FROM dbo.admin WHERE username=@username", connection))
        {
            cmd.Text("@username", username.Trim()); using var r = await cmd.ExecuteReaderAsync(token).ConfigureAwait(false);
            if (!await r.ReadAsync(token).ConfigureAwait(false) || r.IsDBNull(0) || !Enum.TryParse(r.GetString(1), out role) || !Enum.IsDefined(role)) return null;
            hash = r.GetString(0); if (await r.ReadAsync(token).ConfigureAwait(false)) return null;
        }
        bool verified = await Task.Run(() => PasswordHasher.Verify(password, hash), token).ConfigureAwait(false);
        if (!verified) { AppLog.Event("Login.Rejected"); return null; }
        var user = new UserSession(username.Trim(), role);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Login.Success", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false);
        AppLog.Event("Login.Success"); return user;
    }
    public async Task<DataTable> ListAsync(UserSession user, CancellationToken token)
    {
        user.DemandAdmin(); await using var c = new SqlConnection(connectionString); await c.OpenAsync(token).ConfigureAwait(false);
        using var cmd = new SqlCommand("SELECT username,RoleName FROM dbo.admin ORDER BY username", c); return await cmd.TableAsync(token).ConfigureAwait(false);
    }
    public async Task AddAsync(UserSession user, string username, string password, UserRole role, CancellationToken token)
    {
        user.DemandAdmin(); username = AdminStore.ValidateNewAccount(username, password);
        if (!Enum.IsDefined(role)) throw new ArgumentException("Geçersiz rol.");
        string hash = await Task.Run(() => PasswordHasher.Hash(password), token).ConfigureAwait(false);
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        using var check = work.Command("SELECT COUNT(*) FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name"); check.Text("@name", username);
        if (Convert.ToInt32(await check.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0) throw new InvalidOperationException("Bu kullanıcı adı zaten kayıtlı.");
        using var cmd = work.Command("INSERT INTO dbo.admin(username,password,password_hash,RoleName) VALUES(@name,N'',@hash,@role)"); cmd.Text("@name", username); cmd.Text("@hash", hash, 256); cmd.Text("@role", role.ToString(), 24); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.Add", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Account.Add");
    }
    public async Task DeleteAsync(UserSession user, string username, CancellationToken token)
    {
        user.DemandAdmin(); if (string.Equals(username, user.Username, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Etkin oturumun hesabı silinemez.");
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        using var admins = work.Command("SELECT COUNT(*) FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE RoleName=N'Admin'"); int count = Convert.ToInt32(await admins.ExecuteScalarAsync(token).ConfigureAwait(false));
        using var role = work.Command("SELECT RoleName FROM dbo.admin WITH(UPDLOCK,HOLDLOCK) WHERE username=@name"); role.Text("@name", username);
        using (var r = await role.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            if (!await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap bulunamadı.");
            string value = r.GetString(0); if (await r.ReadAsync(token).ConfigureAwait(false)) throw new InvalidOperationException("Hesap tekil değil.");
            if (value == "Admin" && count <= 1) throw new InvalidOperationException("En az bir yönetici hesabı kalmalıdır.");
        }
        using var cmd = work.Command("DELETE FROM dbo.admin WHERE username=@name"); cmd.Text("@name", username); await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        await new PersonnelService(connectionString, user).AuditAsync(work, "Account.Delete", token).ConfigureAwait(false); await work.CommitAsync(token).ConfigureAwait(false); AppLog.Event("Account.Delete");
    }
}
