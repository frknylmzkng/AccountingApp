using System.Data;

namespace WinFormsApp2;

internal sealed class AuditHistoryService(string connectionString, UserSession user)
{
    internal async Task<DataTable> ReadAsync(long? beforeId = null, CancellationToken token = default)
    {
        if (DesktopConnection.IsRemote(connectionString)) return await RemoteApi.For(connectionString, user).TableAsync("audit" + (beforeId.HasValue ? "?before=" + beforeId.Value : ""), token).ConfigureAwait(false);
        user.DemandAdmin();
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.Admin, token).ConfigureAwait(false);
        using var cmd = work.Command(@"SELECT TOP(200) AuditId,CreatedUtc,Actor,Action,EntityType,RecordKey,PersonId,Operation,
            Reason,ReasonSource,OldValues,NewValues,ChangedFields,CorrelationId,ActorAccountId,SqlPrincipal,Source
            FROM dbo.AuditLogSafe WHERE @before IS NULL OR AuditId<@before ORDER BY AuditId DESC");
        cmd.Parameters.Add("@before", SqlDbType.BigInt).Value = (object?)beforeId ?? DBNull.Value;
        var rows = await cmd.TableAsync(token).ConfigureAwait(false);
        await work.CommitAsync(token).ConfigureAwait(false); return rows;
    }
}
