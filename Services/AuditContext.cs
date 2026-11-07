namespace WinFormsApp2;

// Values belong to one async use case. Never keep actor/reason in global mutable state.
internal static class AuditContext
{
    private static readonly AsyncLocal<string?> currentReason = new();
    internal static string? Reason => currentReason.Value;
    internal static string ValidateReason(string reason)
    {
        reason = reason.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
        if (reason.Length is < 5 or > 500 || reason.Any(char.IsControl))
            throw new ArgumentException("Gerekçe 5–500 karakter olmalı ve kontrol karakteri içermemelidir.");
        return reason;
    }
    internal static IDisposable UseReason(string? reason)
    {
        string? previous = currentReason.Value;
        currentReason.Value = reason == null ? null : ValidateReason(reason);
        return new Scope(() => currentReason.Value = previous);
    }
    private sealed class Scope(Action close) : IDisposable { public void Dispose() => close(); }
    internal static async Task InitializeAsync(IUnitOfWork work, CancellationToken token)
    {
        using var cmd = work.Command(@"EXEC sys.sp_set_session_context @key=N'Audit.Actor',@value=NULL;
            EXEC sys.sp_set_session_context @key=N'Audit.ActorId',@value=NULL;
            EXEC sys.sp_set_session_context @key=N'Audit.Reason',@value=@reason;
            EXEC sys.sp_set_session_context @key=N'Audit.Correlation',@value=@correlation;");
        cmd.Parameters.Add("@reason", System.Data.SqlDbType.NVarChar, 500).Value = (object?)Reason ?? DBNull.Value;
        cmd.Parameters.Add("@correlation", System.Data.SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
    internal static async Task AuthorizeAsync(IUnitOfWork work, UserSession user, Guid accountId, CancellationToken token)
    {
        using var cmd = work.Command(@"EXEC sys.sp_set_session_context @key=N'Audit.Actor',@value=@actor;
            EXEC sys.sp_set_session_context @key=N'Audit.ActorId',@value=@accountId;");
        cmd.Text("@actor", user.Username);
        cmd.Parameters.Add("@accountId", System.Data.SqlDbType.UniqueIdentifier).Value = accountId;
        await cmd.ExecuteNonQueryAsync(token).ConfigureAwait(false);
    }
}
