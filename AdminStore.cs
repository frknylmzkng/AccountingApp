using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal sealed class AdminStore
    {
        private readonly string connectionString;

        public AdminStore(string connectionString) => this.connectionString = connectionString;

        public DataTable GetUsers()
        {
            using var connection = new SqlConnection(connectionString);
            using var adapter = new SqlDataAdapter("SELECT username FROM dbo.admin ORDER BY username", connection);
            var table = new DataTable();
            adapter.Fill(table);
            return table;
        }

        public void MigrateLegacyPasswords()
        {
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                using (var migrationLock = new SqlCommand(@"
                    DECLARE @result int;
                    EXEC @result = sys.sp_getapplock @Resource=N'AccountingApp.PasswordMigration.v1',
                        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000;
                    IF @result < 0 THROW 51001, N'Parola geçişi başka bir işlem tarafından kullanılıyor. Yeniden deneyin.', 1;",
                    connection, transaction))
                    migrationLock.ExecuteNonQuery();

                // A separate column distinguishes old plaintext from hashes without relying on a password prefix.
                using (var schema = new SqlCommand(@"
                    IF COL_LENGTH(N'dbo.admin', N'password_hash') IS NULL
                        ALTER TABLE dbo.admin ADD password_hash nvarchar(256) NULL;", connection, transaction))
                    schema.ExecuteNonQuery();

                var legacyAccounts = new List<(string Username, string Password)>();
                using (var select = new SqlCommand(@"
                    SELECT username, password FROM dbo.admin WITH (UPDLOCK, HOLDLOCK)
                    WHERE password_hash IS NULL ORDER BY username", connection, transaction))
                using (var reader = select.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (reader.IsDBNull(0) || reader.IsDBNull(1))
                            throw new InvalidOperationException("Eski yönetici hesabının parola bilgisi eksik.");
                        legacyAccounts.Add((reader.GetString(0), reader.GetString(1)));
                    }
                }

                foreach (var account in legacyAccounts)
                {
                    string hash = PasswordHasher.Hash(account.Password);
                    using var update = new SqlCommand(@"
                        UPDATE dbo.admin SET password_hash = @hash, password = N''
                        WHERE username = @username AND password_hash IS NULL", connection, transaction);
                    update.Parameters.AddWithValue("@username", account.Username);
                    update.Parameters.Add("@hash", SqlDbType.NVarChar, PasswordHasher.MaxEncodedLength).Value = hash;
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidOperationException("Yönetici kullanıcı adları benzersiz olmalıdır. Parola geçişi geri alındı.");
                }
            });
        }

        public bool Authenticate(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50 ||
                string.IsNullOrEmpty(password) || password.Length > PasswordHasher.MaxPasswordLength)
                return false;

            MigrateLegacyPasswords();
            string? hash;
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using var throttleSchema = new SqlCommand("SELECT OBJECT_ID(N'dbo.LoginThrottle')", connection);
                if (throttleSchema.ExecuteScalar() is not DBNull and not null)
                    return new AccountService(connectionString).AuthenticateAsync(username.Trim(), password).GetAwaiter().GetResult() is { MustChangePassword: false };
                using var stateSchema = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.admin',N'IsActive') IS NULL THEN 0 ELSE 1 END", connection);
                bool hasState = Convert.ToInt32(stateSchema.ExecuteScalar()) == 1;
                using var resetSchema = new SqlCommand("SELECT CASE WHEN COL_LENGTH(N'dbo.admin',N'MustChangePassword') IS NULL THEN 0 ELSE 1 END", connection);
                bool hasReset = Convert.ToInt32(resetSchema.ExecuteScalar()) == 1;
                using var select = new SqlCommand(
                    "SELECT TOP (2) password_hash FROM dbo.admin WHERE username = @username" + (hasState ? " AND IsActive=1" : "") + (hasReset ? " AND MustChangePassword=0" : ""), connection);
                select.Parameters.AddWithValue("@username", username.Trim());
                using var reader = select.ExecuteReader();
                if (!reader.Read() || reader.IsDBNull(0)) return false;
                hash = reader.GetString(0);
                if (reader.Read()) return false;
            }
            // A malformed hash is rejected; the legacy column is never an authentication fallback.
            return PasswordHasher.Verify(password, hash);
        }

        public void Add(string username, string password)
        {
            username = ValidateNewAccount(username, password);
            string hash = PasswordHasher.Hash(password);
            MigrateLegacyPasswords();
            DatabaseTransaction.Run(connectionString, (connection, transaction) =>
            {
                using var check = new SqlCommand(@"
                    SELECT COUNT(*) FROM admin WITH (UPDLOCK, HOLDLOCK) WHERE username = @username",
                    connection, transaction);
                check.Parameters.AddWithValue("@username", username);
                if (Convert.ToInt32(check.ExecuteScalar()) > 0)
                    throw new InvalidOperationException("Bu kullanıcı adı zaten kayıtlı!");

                using var insert = new SqlCommand(
                    "INSERT INTO dbo.admin (username, password, password_hash) VALUES (@username, N'', @hash)", connection, transaction);
                insert.Parameters.AddWithValue("@username", username);
                insert.Parameters.Add("@hash", SqlDbType.NVarChar, PasswordHasher.MaxEncodedLength).Value = hash;
                insert.ExecuteNonQuery();
            });
        }

        internal static string ValidateNewAccount(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Trim().Length > 50)
                throw new ArgumentException("Kullanıcı adı 1–50 karakter olmalıdır.", nameof(username));
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6 ||
                password.Length > PasswordHasher.MaxPasswordLength)
                throw new ArgumentException("Şifre 6–1024 karakter olmalıdır.", nameof(password));

            return username.Trim();
        }

        // Legacy synchronous callers use the same authorization and selected-account guards.
        public void Delete(UserSession user, AccountTarget? selected) =>
            new AccountService(connectionString).DeleteAsync(user, selected, default).GetAwaiter().GetResult();
    }
}
