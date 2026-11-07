using System.Globalization;

namespace WinFormsApp2.RegressionTests;

// Existing service scenarios deliberately select the current row. Stale-selection
// regressions pass an explicit earlier snapshot instead of using this helper.
internal static class PaymentTestSelections
{
    internal static async Task<Guid> PayCurrentAsync(this PersonnelService service, PersonnelGroup group, long tc,
        decimal amount, Guid requestId, CancellationToken token = default)
    {
        var rows = await service.ListAsync(group, token, tc.ToString(CultureInfo.InvariantCulture));
        byte[]? version = rows.Rows.Count == 0 ? null : ((byte[])rows.Rows[0][PersonnelConcurrency.ColumnName]).ToArray();
        return await service.PayAsync(group, tc, amount, requestId, token, version);
    }
}
