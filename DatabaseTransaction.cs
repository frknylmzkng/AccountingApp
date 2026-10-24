using System.Data;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2
{
    internal static class DatabaseTransaction
    {
        public static void Run(string connectionString, Action<SqlConnection, SqlTransaction> action)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            // Keep existence/uniqueness checks and their writes in the same isolation scope.
            using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
            try
            {
                action(connection, transaction);
                transaction.Commit();
            }
            catch
            {
                try
                {
                    transaction.Rollback();
                }
                catch (InvalidOperationException) { } // Server may already have rolled back.
                catch (SqlException) { } // Preserve the original failure if the connection is lost.
                throw;
            }
        }
    }
}
