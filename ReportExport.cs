using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace WinFormsApp2;

internal static class ReportExport
{
    private const string SourceColumn = "AccountingApp.SourceColumn";
    private const string BalanceFirstRows = "AccountingApp.BalanceFirstRows";
    private static string SourceName(DataColumn column) => column.ExtendedProperties[SourceColumn] as string ?? column.ColumnName;
    private static string SourceName(DataGridViewColumn column) => string.IsNullOrEmpty(column.DataPropertyName) ? column.Name : column.DataPropertyName;
    public static DataTable Snapshot(DataGridView grid)
    {
        var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible && !RecordSelection.IsInternalColumn(c)).OrderBy(c => c.DisplayIndex).ToArray();
        var result = new DataTable("Rapor");
        foreach (var c in columns)
        {
            var column = result.Columns.Add(c.HeaderText, c.ValueType ?? typeof(string));
            column.ExtendedProperties[SourceColumn] = SourceName(c);
        }
        var rows = grid.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow && r.Visible).ToArray();
        foreach (var row in rows) result.Rows.Add(columns.Select(c => row.Cells[c.Index].Value ?? DBNull.Value).ToArray());
        var balance = columns.FirstOrDefault(c => SourceName(c) == "PARA" && c.ValueType == typeof(decimal));
        static object? SourceValue(DataGridView grid, DataGridViewRow row, string name)
        {
            if (row.DataBoundItem is DataRowView bound && bound.Row.Table.Columns.Contains(name)) return bound.Row[name];
            var column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => SourceName(c) == name);
            return column == null ? null : row.Cells[column.Index].Value;
        }
        if (balance != null && rows.All(r => SourceValue(grid, r, "TC") is not null and not DBNull))
            result.ExtendedProperties[BalanceFirstRows] = MarkFirstBalances(rows.Select(r =>
                (Convert.ToString(SourceValue(grid, r, "TC"), CultureInfo.InvariantCulture) + "|" + Convert.ToString(SourceValue(grid, r, "GRUP"), CultureInfo.InvariantCulture), r.Cells[balance.Index].Value)));
        return result;
    }
    private static bool[] MarkFirstBalances(IEnumerable<(string Key, object? Balance)> rows)
    {
        var seen = new Dictionary<string, decimal?>(); var first = new List<bool>();
        foreach (var (key, value) in rows)
        {
            decimal? balance = value is null or DBNull ? null : Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            bool added = seen.TryAdd(key, balance);
            if (!added && seen[key] != balance) throw new InvalidOperationException("Aynı personelin bakiye satırları tutarsız. Listeyi yenileyip raporu yeniden oluşturun.");
            first.Add(added);
        }
        return first.ToArray();
    }
    private static bool[]? PersonnelBalanceRows(DataTable table)
    {
        if (table.ExtendedProperties[BalanceFirstRows] is bool[] rows)
        {
            if (rows.Length != table.Rows.Count) throw new InvalidOperationException("Rapor satırları değişmiş. Raporu yeniden oluşturun.");
            return rows;
        }
        var balance = table.Columns.Cast<DataColumn>().FirstOrDefault(c => SourceName(c) == "PARA" && c.DataType == typeof(decimal));
        var tc = table.Columns.Cast<DataColumn>().FirstOrDefault(c => SourceName(c) == "TC");
        var group = table.Columns.Cast<DataColumn>().FirstOrDefault(c => SourceName(c) == "GRUP");
        if (balance == null || tc == null) return null;
        return MarkFirstBalances(table.AsEnumerable().Select(r =>
            (Convert.ToString(r[tc], CultureInfo.InvariantCulture) + "|" + (group == null ? "" : Convert.ToString(r[group], CultureInfo.InvariantCulture)), (object?)r[balance])));
    }
    internal static string CsvCell(object? value)
    {
        string text = value is DateTime date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.GetCultureInfo("tr-TR")) ?? "";
        // Spreadsheet applications interpret formula-like text even inside quoted CSV cells.
        if (value is string && text.TrimStart() is { Length: > 0 } trimmed && "=+-@".Contains(trimmed[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    public static void Csv(DataTable table, string path)
    {
        table = WithoutInternalColumns(table);
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.NewLine = "\r\n"; writer.WriteLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => CsvCell(c.ColumnName))));
        foreach (DataRow row in table.Rows) writer.WriteLine(string.Join(";", row.ItemArray.Select(CsvCell)));
    }
    public static void Excel(DataTable table, string path)
    {
        table = WithoutInternalColumns(table);
        var balanceRows = PersonnelBalanceRows(table);
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Personel raporu");
        bool distinctBalance = balanceRows?.Any(first => !first) == true;
        const string totalsSheet = "_ToplamHesabi";
        if (distinctBalance)
        {
            var helper = workbook.AddWorksheet(totalsSheet); helper.Visibility = XLWorksheetVisibility.VeryHidden;
            helper.Cell(1, 1).Value = "Tekil personel bakiyesi (rapor anı)";
            var balance = table.Columns.Cast<DataColumn>().First(c => SourceName(c) == "PARA" && c.DataType == typeof(decimal));
            // A separate snapshot keeps the total stable when users sort leave rows in Excel.
            // Only amounts are stored here; hidden TC/group fields are never exported.
            for (int r = 0; r < balanceRows!.Length; r++)
                if (balanceRows[r] && table.Rows[r][balance] is decimal value) helper.Cell(r + 2, 1).Value = value;
        }
        for (int c = 0; c < table.Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = table.Columns[c].ColumnName;
            for (int r = 0; r < table.Rows.Count; r++)
            {
                object value = table.Rows[r][c]; var cell = sheet.Cell(r + 2, c + 1);
                if (value == DBNull.Value) continue;
                if (value is decimal d) cell.Value = d;
                else if (value is int n) cell.Value = n;
                else if (value is long l) cell.Value = l.ToString(CultureInfo.InvariantCulture); // preserve identifiers exactly
                else if (value is DateTime date) { cell.Value = date; cell.Style.DateFormat.Format = "dd.mm.yyyy"; }
                else cell.Value = value.ToString() ?? "";
            }
            if (table.Columns[c].DataType == typeof(decimal))
            {
                sheet.Column(c + 1).Style.NumberFormat.Format = "#,##0.00 \"TL\";[Red]-#,##0.00 \"TL\"";
                if (table.Rows.Count > 0)
                {
                    string range = $"{sheet.Cell(2, c + 1).Address}:{sheet.Cell(table.Rows.Count + 1, c + 1).Address}";
                    var total = sheet.Cell(table.Rows.Count + 2, c + 1);
                    if (distinctBalance && SourceName(table.Columns[c]) == "PARA")
                    {
                        total.FormulaA1 = $"SUM('{totalsSheet}'!A2:A{table.Rows.Count + 1})";
                        total.GetComment().AddText("Rapor anındaki personel bakiyesi TC ve grup bazında bir kez toplanır. İzin satırları korunur; satır sıralaması toplamı değiştirmez.");
                    }
                    else total.FormulaA1 = $"SUM({range})";
                }
            }
        }
        if (table.Columns.Count > 0)
        {
            var header = sheet.Range(1, 1, 1, table.Columns.Count); header.Style.Fill.BackgroundColor = XLColor.FromHtml("#192E50"); header.Style.Font.FontColor = XLColor.White; header.Style.Font.Bold = true;
            for (int r = 2; r <= table.Rows.Count + 1; r++) if (r % 2 == 0) sheet.Range(r, 1, r, table.Columns.Count).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF1FA");
            sheet.Range(1, 1, table.Rows.Count + 2, table.Columns.Count).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            sheet.Row(table.Rows.Count + 2).Style.Font.Bold = true; sheet.SheetView.FreezeRows(1); sheet.Columns().AdjustToContents(10, 55);
            sheet.Range(1, 1, table.Rows.Count + 1, table.Columns.Count).SetAutoFilter();
        }
        workbook.SaveAs(path);
    }
    public static void Open(string path) => Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
    private static DataTable WithoutInternalColumns(DataTable table)
    {
        if (!table.Columns.Cast<DataColumn>().Any(c => RecordSelection.IsInternalColumnName(SourceName(c)))) return table;
        var copy = table.Copy();
        foreach (var column in copy.Columns.Cast<DataColumn>().Where(c => RecordSelection.IsInternalColumnName(SourceName(c))).ToArray()) copy.Columns.Remove(column);
        return copy;
    }
}
