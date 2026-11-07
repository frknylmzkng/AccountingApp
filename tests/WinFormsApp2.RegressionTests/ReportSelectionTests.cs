using System.Data;
using ClosedXML.Excel;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class ReportSelectionTests
{
    [Test, Category("Exports")]
    public void RemovingBalanceColumnFromMultiLeaveSnapshotDoesNotCreateInvalidHiddenTotal()
    {
        string path = Path.Combine(Path.GetTempPath(), "AccountingApp-report-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            using var grid = new DataGridView { AllowUserToAddRows = false };
            foreach (string name in new[] { "TC", "GRUP", "AD" }) grid.Columns.Add(name, name);
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PARA", HeaderText = "PARA", ValueType = typeof(decimal) });
            grid.Rows.Add("11111111111", "Genel", "Synthetic", 100m);
            grid.Rows.Add("11111111111", "Genel", "Synthetic", 100m);
            using var snapshot = ReportExport.Snapshot(grid);
            snapshot.Columns.Remove("TC"); snapshot.Columns.Remove("GRUP"); snapshot.Columns.Remove("PARA");
            snapshot.ExtendedProperties["Export.ReportTitle"] = "Seçilen ad listesi";
            snapshot.ExtendedProperties["Export.ReportScope"] = "Açık sayfa";
            ReportExport.Excel(snapshot, path);
            using var book = new XLWorkbook(path);
            Assert.That(book.Worksheet("Rapor").Cell(2, 1).GetString(), Is.EqualTo("Synthetic"));
            Assert.That(book.Worksheets.Any(s => s.Name == "_ToplamHesabi"), Is.False);
            Assert.That(book.Worksheet("Rapor bilgisi").Cell(1, 2).GetString(), Is.EqualTo("Seçilen ad listesi"));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
    [Test, Category("Exports")]
    public void UtcExportFieldsRetainTimeWhileBusinessDateStaysDateOnly()
    {
        string csv = Path.Combine(Path.GetTempPath(), "AccountingApp-report-" + Guid.NewGuid().ToString("N") + ".csv");
        string excel = Path.ChangeExtension(csv, ".xlsx");
        try
        {
            using var data = new DataTable(); data.Columns.Add("Gün", typeof(DateTime)); data.Columns.Add("Hareket UTC", typeof(DateTime));
            var time = new DateTime(2026, 10, 29, 9, 10, 11, DateTimeKind.Utc).AddTicks(1234567);
            data.Rows.Add(time.Date, time); ReportExport.Csv(data, csv); ReportExport.Excel(data, excel);
            Assert.That(File.ReadAllText(csv), Does.Contain("2026-10-29T09:10:11.1234567Z"));
            using var book = new XLWorkbook(excel);
            Assert.That(book.Worksheet(1).Cell(2, 2).Style.DateFormat.Format, Does.Contain("hh:mm:ss"));
            Assert.That(book.Worksheet(1).Cell(2, 1).Style.DateFormat.Format, Is.EqualTo("dd.mm.yyyy"));
        }
        finally { if (File.Exists(csv)) File.Delete(csv); if (File.Exists(excel)) File.Delete(excel); }
    }
}
