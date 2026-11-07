using System.Data;
namespace WinFormsApp2;

// Applies before returning a service result, on the server in API mode.
// Keep types and identity/version columns, but never serialize forbidden values.
internal static class SensitiveData
{
    internal static DataTable Mask(DataTable data, UserSession user)
    {
        foreach (DataColumn c in data.Columns)
        {
            if ((c.ColumnName.Equals("IBAN", StringComparison.OrdinalIgnoreCase) && !user.CanViewIban) || (c.DataType == typeof(decimal) && !user.CanViewSalary))
            {
                bool readOnly = c.ReadOnly; c.ReadOnly = false;
                foreach (DataRow row in data.Rows) row[c] = DBNull.Value;
                c.ReadOnly = readOnly;
            }
        }
        data.AcceptChanges(); return data;
    }
    internal static void Hide(DataGridView grid, UserSession user)
    {
        foreach (DataGridViewColumn c in grid.Columns)
            if ((c.Name.Equals("IBAN", StringComparison.OrdinalIgnoreCase) && !user.CanViewIban) || (c.ValueType == typeof(decimal) && !user.CanViewSalary)) c.Visible = false;
    }
}
