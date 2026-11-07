using System.Data;
using System.Text.Json;
using Wire = AccountingApp.Contracts;
using WinFormsApp2;

namespace AccountingApp.Server;

internal static class WireTables
{
    internal static Wire.Table Encode(DataTable table)
    {
        using (table)
        {
            Wire.ColumnType Kind(Type type) => type == typeof(string) ? Wire.ColumnType.String : type == typeof(bool) ? Wire.ColumnType.Boolean :
                type == typeof(int) ? Wire.ColumnType.Int32 : type == typeof(long) ? Wire.ColumnType.Int64 : type == typeof(decimal) ? Wire.ColumnType.Decimal :
                type == typeof(DateTime) ? Wire.ColumnType.DateTime : type == typeof(Guid) ? Wire.ColumnType.Guid : type == typeof(byte[]) ? Wire.ColumnType.Binary :
                throw new InvalidOperationException("Unsupported API column type.");
            var columns = table.Columns.Cast<DataColumn>().Select(c => new Wire.Column(c.ColumnName, Kind(c.DataType), c.ReadOnly)).ToArray();
            var rows = table.Rows.Cast<DataRow>().Select(row => table.Columns.Cast<DataColumn>().Select(c =>
                JsonSerializer.SerializeToElement(row[c] switch { DBNull => null, long value => (object)value.ToString(System.Globalization.CultureInfo.InvariantCulture), var value => value })).ToArray()).ToArray();
            return new(columns, rows);
        }
    }
    internal static Wire.Page Encode(PersonnelPage page) => new(Encode(page.Rows), page.Total, page.Index, page.Size);
    internal static Wire.PrintData Encode(PrintReport report) => new(report.Title, report.Lines.ToArray(), report.Reference, report.Difference);
    internal static PersonnelGroup Group(Wire.PersonnelGroup group) => Enum.IsDefined(group) ? (PersonnelGroup)group : throw new ArgumentException("Invalid personnel group.");
    internal static PaymentPurpose Purpose(Wire.PaymentPurpose purpose) => Enum.IsDefined(purpose) ? (PaymentPurpose)purpose : throw new ArgumentException("Invalid payment purpose.");
    internal static PaymentRequest Payment(Wire.PaymentIntent intent) => new(intent.RequestId, Group(intent.Group), intent.Tc, intent.Amount, intent.ClosureId)
    { FormatVersion = intent.FormatVersion, PersonVersion = intent.PersonVersion?.ToArray(), PersonId = intent.PersonId, Purpose = Purpose(intent.Purpose), ProjectId = intent.ProjectId, AuditReason = intent.Reason };
    internal static Wire.PaymentIntent Payment(PaymentRequest intent) => new(intent.RequestId, (Wire.PersonnelGroup)intent.Group, intent.Tc, intent.Amount, intent.ClosureId,
        intent.FormatVersion, intent.PersonVersion?.ToArray(), intent.PersonId, (Wire.PaymentPurpose)intent.Purpose, intent.ProjectId, intent.AuditReason);
    internal static byte[] Version(byte[]? value) => value is { Length: 8 } ? value.ToArray() : throw new ArgumentException("An eight-byte row version is required.");
    internal static AccountTarget Target(Wire.AccountTarget target) => target == null
        ? throw new ArgumentException("Account target required.") : new(target.Username, Version(target.Version), target.Epoch);
}
