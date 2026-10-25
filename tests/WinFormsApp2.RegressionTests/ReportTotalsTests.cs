using System.Data;
using System.IO.Compression;
using ClosedXML.Excel;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Exports")]
public class ReportTotalsTests
{
    private string directory = "";
    [SetUp] public void Setup() { directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Totals_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TearDown] public void Cleanup() { Directory.Delete(directory, true); }
    private static DataTable People()
    {
        var table = new DataTable(); table.Columns.Add("TC", typeof(long)); table.Columns.Add("GRUP");
        table.Columns.Add("PARA", typeof(decimal)); table.Columns.Add("IZINBASLANGIC", typeof(DateTime)); table.Columns.Add("Other", typeof(decimal));
        table.Rows.Add(11111111111L, "Genel", 1000m, new DateTime(2026, 1, 1), 1m);
        table.Rows.Add(11111111111L, "Genel", 1000m, new DateTime(2026, 2, 1), 2m);
        table.Rows.Add(22222222222L, "Genel", 1000m, new DateTime(2026, 3, 1), 3m);
        table.AcceptChanges(); return table;
    }
    [Test]
    public void MultipleLeavesKeepEveryRowButBalanceCountsEachPersonOnce()
    {
        var data = People(); string path = Path.Combine(directory, "people.xlsx"); ReportExport.Excel(data, path);
        using var book = new XLWorkbook(path); var sheet = book.Worksheet(1);
        Assert.That(sheet.Cell("C5").GetValue<decimal>(), Is.EqualTo(2000m));
        Assert.That(sheet.Cell("E5").GetValue<decimal>(), Is.EqualTo(6m));
        Assert.That(sheet.Cell("C2").GetValue<decimal>(), Is.EqualTo(1000m));
        Assert.That(sheet.Cell("C3").GetValue<decimal>(), Is.EqualTo(1000m));
        Assert.That(sheet.Cell("D3").GetDateTime(), Is.EqualTo(new DateTime(2026, 2, 1)));
        Assert.That(data.Rows.Count, Is.EqualTo(3));
    }
    [Test]
    public void PersonnelGroupsAreSeparateAndEqualAmountsAreNotDeduplicatedByValue()
    {
        var data = People(); data.Rows[2]["TC"] = 11111111111L; data.Rows[2]["GRUP"] = "Joker";
        string path = Path.Combine(directory, "groups.xlsx"); ReportExport.Excel(data, path);
        using var book = new XLWorkbook(path);
        Assert.That(book.Worksheet(1).Cell("C5").GetValue<decimal>(), Is.EqualTo(2000m));
    }
    [Test]
    public void SortingExportedLeaveRowsDoesNotChangePersonnelTotal()
    {
        var data = People(); data.Rows[2]["PARA"] = 2000m; data.Rows[2]["IZINBASLANGIC"] = new DateTime(2026, 1, 15);
        string path = Path.Combine(directory, "sortable.xlsx"); ReportExport.Excel(data, path);
        using var book = new XLWorkbook(path); var sheet = book.Worksheet(1);
        Assert.That(sheet.Cell("C5").GetValue<decimal>(), Is.EqualTo(3000m));
        sheet.Range("A2:E4").Sort(4, XLSortOrder.Ascending);
        book.RecalculateAllFormulas();
        Assert.That(sheet.Cell("C5").GetValue<decimal>(), Is.EqualTo(3000m));
    }
    [Test]
    public void HiddenIdentityAndLocalizedHeadersStillGiveCorrectTotalsWithoutExportingIdentity()
    {
        using var form = new Form { ShowInTaskbar = false, Opacity = 0, Location = new Point(-30000, -30000), StartPosition = FormStartPosition.Manual };
        using var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = false };
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "hiddenIdentity", DataPropertyName = "TC", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "hiddenGroup", DataPropertyName = "GRUP", Visible = false });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "balance", DataPropertyName = "PARA", HeaderText = "Kalan tutar", ValueType = typeof(decimal) });
        grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "leave", DataPropertyName = "IZINBASLANGIC", HeaderText = "İzin başlangıcı", ValueType = typeof(DateTime) });
        form.Controls.Add(grid); grid.DataSource = People(); form.Show();
        grid.Sort(grid.Columns["leave"]!, System.ComponentModel.ListSortDirection.Descending);
        var snapshot = ReportExport.Snapshot(grid);
        Assert.That(snapshot.Columns.Count, Is.EqualTo(2)); Assert.That(snapshot.Rows.Count, Is.EqualTo(3));
        string path = Path.Combine(directory, "hidden.xlsx"); ReportExport.Excel(snapshot, path);
        using (var book = new XLWorkbook(path)) Assert.That(book.Worksheet(1).Cell("A5").GetValue<decimal>(), Is.EqualTo(2000m));
        using var zip = ZipFile.OpenRead(path);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal)))
        {
            using var reader = new StreamReader(entry.Open()); string xml = reader.ReadToEnd();
            Assert.That(xml, Does.Not.Contain("11111111111")); Assert.That(xml, Does.Not.Contain("22222222222"));
        }
    }
    [Test]
    public void InconsistentRepeatedBalanceFailsInsteadOfPublishingMisleadingTotal()
    {
        var data = People(); data.Rows[1]["PARA"] = 900m;
        string path = Path.Combine(directory, "inconsistent.xlsx");
        Assert.Throws<InvalidOperationException>(() => ReportExport.Excel(data, path));
        Assert.That(File.Exists(path), Is.False);
    }
    [Test]
    public void EmptyPersonnelReportAndGenericDecimalReportRemainValid()
    {
        var empty = People(); empty.Clear(); ReportExport.Excel(empty, Path.Combine(directory, "empty.xlsx"));
        var generic = new DataTable(); generic.Columns.Add("PARA", typeof(decimal)); generic.Rows.Add(1000m); generic.Rows.Add(1000m);
        string path = Path.Combine(directory, "generic.xlsx"); ReportExport.Excel(generic, path);
        using var book = new XLWorkbook(path); Assert.That(book.Worksheet(1).Cell("A4").GetValue<decimal>(), Is.EqualTo(2000m));
        Assert.That(book.Worksheets.Count, Is.EqualTo(1));
    }
}
