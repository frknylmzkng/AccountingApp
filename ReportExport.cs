using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace WinFormsApp2;

internal static class ReportExport
{
    public static DataTable Snapshot(DataGridView grid)
    {
        var columns = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Visible).OrderBy(c => c.DisplayIndex).ToArray();
        var result = new DataTable("Rapor");
        foreach (var c in columns) result.Columns.Add(c.HeaderText, c.ValueType ?? typeof(string));
        foreach (DataGridViewRow row in grid.Rows) if (!row.IsNewRow && row.Visible) result.Rows.Add(columns.Select(c => row.Cells[c.Index].Value ?? DBNull.Value).ToArray());
        return result;
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
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.NewLine = "\r\n"; writer.WriteLine(string.Join(";", table.Columns.Cast<DataColumn>().Select(c => CsvCell(c.ColumnName))));
        foreach (DataRow row in table.Rows) writer.WriteLine(string.Join(";", row.ItemArray.Select(CsvCell)));
    }
    public static void Excel(DataTable table, string path)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Personel raporu");
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
                if (table.Rows.Count > 0) sheet.Cell(table.Rows.Count + 2, c + 1).FormulaA1 = $"SUM({sheet.Cell(2, c + 1).Address}:{sheet.Cell(table.Rows.Count + 1, c + 1).Address})";
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
}
