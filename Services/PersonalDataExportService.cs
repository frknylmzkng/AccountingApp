using System.Data;

namespace WinFormsApp2;

internal sealed class PersonalDataExportService(string connectionString, UserSession user)
{
    public async Task ExportAsync(DataTable data, string destination, bool excel, CancellationToken token = default)
    {
        user.DemandFullDataExport();
        string path = Path.GetFullPath(destination);
        string extension = excel ? ".xlsx" : ".csv";
        if (!string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Dosya uzantısı " + extension + " olmalıdır.");
        using var snapshot = data.Copy();
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, ".AccountingApp-export-" + Guid.NewGuid().ToString("N") + extension);
        if (DesktopConnection.IsRemote(connectionString))
        {
            try
            {
                byte[] bytes = await RemoteApi.For(connectionString, user).ExportAsync(snapshot, excel, token).ConfigureAwait(false);
                await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return;
        }
        await using var work = await SqlUnitOfWork.OpenAsync(connectionString, token).ConfigureAwait(false);
        await AccountAuthorization.DemandAsync(work, user, AccountAccess.FullDataExport, token).ConfigureAwait(false);
        try
        {
            // Keep the live account read lock through file publication. Revoking
            // a grant waits for an already-authorized export to finish.
            using (var audit = work.Command(@"INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,ReasonSource,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
                VALUES(@actor,@action,N'ReportExport',@id,N'Export',N'System',@details,
                TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application')"))
            {
                audit.Text("@actor", user.Username); audit.Text("@action", excel ? "Export.Excel" : "Export.Csv", 64);
                audit.Text("@id", Guid.NewGuid().ToString("D"), 100);
                audit.Text("@details", System.Text.Json.JsonSerializer.Serialize(new {
                    Report = snapshot.ExtendedProperties["Export.ReportTitle"]?.ToString() ?? snapshot.TableName,
                    Scope = snapshot.ExtendedProperties["Export.ReportScope"]?.ToString(),
                    Columns = snapshot.Columns.Cast<System.Data.DataColumn>().Select(c => c.ColumnName).ToArray(),
                    Rows = snapshot.Rows.Count, Format = excel ? "Excel" : "CSV" }), -1);
                await audit.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
            await Task.Run(() => { if (excel) ReportExport.Excel(snapshot, temporary); else ReportExport.Csv(snapshot, temporary); }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); File.Move(temporary, path, overwrite: true);
            await work.CommitAsync(token).ConfigureAwait(false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
