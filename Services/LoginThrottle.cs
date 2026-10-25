using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal static class LoginAttemptPolicy
{
    internal const int MaximumFailures = 5;
    internal const int ObservationMinutes = 15;
    internal const int LockMinutes = 5;
    internal const string FailureMessage = "Giriş yapılamadı. Bilgilerinizi kontrol edin; art arda hatalı deneme yaptıysanız 5 dakika bekleyip yeniden deneyin.";
    internal const string ReauthenticationFailure = "Mevcut parola doğrulanamadı. Art arda hatalı deneme yaptıysanız 5 dakika bekleyip yeniden deneyin.";
}

// A database-scoped, per-account gate serializes attempts across processes without
// holding a SQL transaction or row lock during the expensive password calculation.
internal sealed class LoginAttemptGate : IAsyncDisposable
{
    private readonly SqlConnection connection;
    private readonly string resource;
    internal SqlConnection Connection => connection;
    private LoginAttemptGate(SqlConnection connection, string resource) { this.connection = connection; this.resource = resource; }

    internal static async Task<LoginAttemptGate> OpenAsync(string connectionString, Guid epoch, CancellationToken token)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            string resource = "AccountingApp.Login.v1:" + epoch.ToString("N");
            using var command = new SqlCommand(@"DECLARE @r int;
                EXEC @r=sys.sp_getapplock @Resource=@resource,@LockMode='Exclusive',@LockOwner='Session',@LockTimeout=10000;
                SELECT @r;", connection);
            command.Text("@resource", resource, 100);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(token).ConfigureAwait(false)) < 0)
                throw new InvalidOperationException("Giriş doğrulaması meşgul. Biraz bekleyip yeniden deneyin.");
            return new(connection, resource);
        }
        catch
        {
            // Cancellation can race the grant. Never return a possibly locked
            // physical connection to the pool without releasing its session lock.
            SqlConnection.ClearPool(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            using var release = new SqlCommand("EXEC sys.sp_releaseapplock @Resource=@resource,@LockOwner='Session'", connection) { CommandTimeout = 5 };
            release.Text("@resource", resource, 100);
            await release.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            SqlConnection.ClearPool(connection);
            AppLog.Failure(ex, "Login.GateRelease");
        }
        finally { await connection.DisposeAsync().ConfigureAwait(false); }
    }
}

internal static class LoginThrottle
{
    internal static async Task<bool> IsLockedAsync(IUnitOfWork work, Guid epoch, CancellationToken token)
    {
        using var cmd = work.Command("SELECT COUNT(*) FROM dbo.LoginThrottle WITH(UPDLOCK,HOLDLOCK) WHERE AccountEpoch=@epoch AND LockedUntilUtc>SYSUTCDATETIME()");
        cmd.Parameters.Add("@epoch", System.Data.SqlDbType.UniqueIdentifier).Value = epoch;
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false)) != 0;
    }

    internal static async Task<bool> FailureAsync(IUnitOfWork work, Guid epoch, CancellationToken token)
    {
        using var cmd = work.Command(@"DECLARE @now datetime2=SYSUTCDATETIME();
            DELETE FROM dbo.LoginThrottle WHERE AccountEpoch=@epoch
                AND (WindowStartedUtc<=DATEADD(minute,-@window,@now) OR LockedUntilUtc<=@now);
            UPDATE dbo.LoginThrottle SET FailureCount=FailureCount+1,
                LockedUntilUtc=CASE WHEN FailureCount+1>=@maximum THEN DATEADD(minute,@duration,@now) END
                WHERE AccountEpoch=@epoch;
            IF @@ROWCOUNT=0 INSERT dbo.LoginThrottle(AccountEpoch,FailureCount,WindowStartedUtc) VALUES(@epoch,1,@now);
            SELECT CASE WHEN LockedUntilUtc>@now THEN 1 ELSE 0 END FROM dbo.LoginThrottle WHERE AccountEpoch=@epoch;");
        cmd.Parameters.Add("@epoch", System.Data.SqlDbType.UniqueIdentifier).Value = epoch;
        cmd.Parameters.AddWithValue("@window", LoginAttemptPolicy.ObservationMinutes);
        cmd.Parameters.AddWithValue("@maximum", LoginAttemptPolicy.MaximumFailures);
        cmd.Parameters.AddWithValue("@duration", LoginAttemptPolicy.LockMinutes);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(token).ConfigureAwait(false)) == 1;
    }

    internal static async Task ClearAsync(IUnitOfWork work, Guid epoch, CancellationToken token)
    {
        using var cmd = work.Command("DELETE FROM dbo.LoginThrottle WHERE AccountEpoch=@epoch");
        cmd.Parameters.Add("@epoch", System.Data.SqlDbType.UniqueIdentifier).Value = epoch;
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
