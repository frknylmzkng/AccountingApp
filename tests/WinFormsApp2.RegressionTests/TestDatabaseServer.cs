using Microsoft.Data.SqlClient;

namespace WinFormsApp2.RegressionTests;

internal static class TestDatabaseServer
{
    internal static string Server
    {
        get
        {
            string? instance = Environment.GetEnvironmentVariable("ACCOUNTINGAPP_TEST_LOCALDB_INSTANCE");
            if (string.IsNullOrWhiteSpace(instance)) return @"(LocalDB)\MSSQLLocalDB";
            if (!System.Text.RegularExpressions.Regex.IsMatch(instance, "\\AAccountingAppTestEngine_[0-9a-f]{32}\\z"))
                throw new InvalidOperationException("Test motoru GUID kapsamında olmalıdır.");
            return @"(LocalDB)\" + instance;
        }
    }
    internal static string Master => new SqlConnectionStringBuilder
    { DataSource = Server, InitialCatalog = "master", IntegratedSecurity = true, Encrypt = SqlConnectionEncryptOption.Optional, Pooling = false, ConnectTimeout = 30 }.ConnectionString;
}
