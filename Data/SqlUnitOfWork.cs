using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal interface IUnitOfWork : IAsyncDisposable
{
    SqlCommand Command(string sql);
    Task CommitAsync(CancellationToken token);
}

// One instance belongs to exactly one use case. Uncommitted work is always rolled back.
internal sealed class SqlUnitOfWork : IUnitOfWork
{
    private readonly SqlConnection connection;
    private readonly SqlTransaction transaction;
    private bool completed;
    private SqlUnitOfWork(SqlConnection connection, SqlTransaction transaction) { this.connection = connection; this.transaction = transaction; }
    public static async Task<SqlUnitOfWork> OpenAsync(string connectionString, CancellationToken token)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(token).ConfigureAwait(false);
            var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, token).ConfigureAwait(false);
            return new(connection, transaction);
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }
    public SqlCommand Command(string sql) => !completed ? new(sql, connection, transaction) : throw new InvalidOperationException("İşlem zaten tamamlandı.");
    public async Task CommitAsync(CancellationToken token)
    {
        if (completed) throw new InvalidOperationException("İşlem zaten tamamlandı.");
        await transaction.CommitAsync(token).ConfigureAwait(false); completed = true;
    }
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
            {
                try { await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException) { AppLog.Failure(ex, "Transaction.Rollback"); }
            }
        }
        finally { await transaction.DisposeAsync().ConfigureAwait(false); await connection.DisposeAsync().ConfigureAwait(false); }
    }
}

internal static class SqlValues
{
    public static void Tc(this SqlCommand command, long tc) => command.Parameters.Add("@tc", SqlDbType.BigInt).Value = tc;
    public static void Text(this SqlCommand command, string key, string value, int size = 50) => command.Parameters.Add(key, SqlDbType.NVarChar, size).Value = value;
    public static void Money(this SqlCommand command, string key, decimal value)
    { var p = command.Parameters.Add(key, SqlDbType.Decimal); p.Precision = 18; p.Scale = 2; p.Value = value; }
    public static async Task<DataTable> TableAsync(this SqlCommand command, CancellationToken token)
    {
        using var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false);
        var result = new DataTable();
        for (int i = 0; i < reader.FieldCount; i++) result.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
        while (await reader.ReadAsync(token).ConfigureAwait(false))
        { var values = new object[reader.FieldCount]; reader.GetValues(values); result.Rows.Add(values); }
        result.AcceptChanges(); // Bindings must retain immutable original identity for selected-record writes.
        return result;
    }
}

