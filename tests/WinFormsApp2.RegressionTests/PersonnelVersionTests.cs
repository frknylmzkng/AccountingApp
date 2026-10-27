using System.Data;
using ClosedXML.Excel;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Personnel")]
public class PersonnelVersionTests
{
    private static readonly byte[] FirstVersion = [0, 0, 0, 0, 0, 0, 0, 1];
    private static readonly byte[] NextVersion = [0, 0, 0, 0, 0, 0, 0, 2];
    private static DataTable Records()
    {
        var table = new DataTable(); table.Columns.Add("TC", typeof(long)); table.Columns.Add("AD");
        table.Columns.Add("PARA", typeof(decimal)); table.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[]));
        table.Rows.Add(11111111111L, "Ada", 100m, FirstVersion.ToArray());
        table.Rows.Add(22222222222L, "Ayşe", 200m, NextVersion.ToArray());
        table.AcceptChanges(); return table;
    }
    private static DataGridView Bind(Form form, DataTable table)
    {
        var grid = new DataGridView { Dock = DockStyle.Fill }; RecordSelection.Configure(grid);
        form.Controls.Add(grid); grid.BindingContext = new BindingContext(); grid.DataSource = table; grid.ClearSelection();
        return grid;
    }
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(7)]
    [TestCase(9)]
    public async Task MissingOrMalformedVersionIsRejectedBeforeDatabaseAccess(int length)
    {
        var service = new PersonnelService("invalid connection string", new("operator", UserRole.PersonnelManager));
        var person = new PersonnelDetails(11111111111L, "Ada", "Test", "TR000000000000000000000000", 100m);
        byte[]? version = length < 0 ? null : new byte[length];
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.SaveAsync(PersonnelGroup.General, person, false, expectedVersion: version));
    }
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(7)]
    [TestCase(9)]
    public async Task DeleteRequiresAValidVersionBeforeDatabaseAccess(int length)
    {
        var service = new PersonnelService("invalid connection string", new("operator", UserRole.PersonnelManager));
        byte[]? version = length < 0 ? null : new byte[length];
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.DeleteAsync(PersonnelGroup.General, 11111111111L, version));
        Assert.That(error!.Message, Does.Contain("sürüm bilgisi eksik"));
        var store = new PersonnelStore("invalid connection string");
        Assert.Throws<InvalidOperationException>(() => store.Delete(PersonnelGroup.General, 11111111111L, version));
    }
    [Test]
    public void SelectedVersionComesFromOriginalRowAndReturnsDefensiveCopy()
    {
        using var form = new Form(); var table = Records(); using var grid = Bind(form, table);
        table.Rows[0][PersonnelConcurrency.ColumnName] = NextVersion.ToArray(); grid.Rows[0].Selected = true;
        var version = RecordSelection.OriginalVersion(grid);
        Assert.That(version, Is.EqualTo(FirstVersion)); version[7] = 99;
        Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(FirstVersion));
    }
    [Test]
    public void JokerVisibleColumnsPreserveOriginalIdentityAndVersionAfterSorting()
    {
        using var form = new Joker { PreviewOnly = true, PersistGridLayout = false, Session = new("preview-user", UserRole.Admin), Opacity = 0, ShowInTaskbar = false };
        form.Show();
        var table = Records(); table.Columns.Add("SOYAD"); table.Columns.Add("IBAN"); table.Columns.Add("GRUP");
        foreach (DataRow row in table.Rows) { row["SOYAD"] = "Örnek"; row["IBAN"] = "TR000000000000000000000000"; row["GRUP"] = "Joker"; }
        table.AcceptChanges();
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single(); grid.DataSource = table;
        table.DefaultView.Sort = "TC DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        Assert.That(RecordSelection.TryGetTc(grid, out long tc), Is.True); Assert.That(tc, Is.EqualTo(22222222222L));
        Assert.That(grid.Columns.Contains(PersonnelConcurrency.ColumnName), Is.False);
        table.Rows[1][PersonnelConcurrency.ColumnName] = FirstVersion.ToArray();
        Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(NextVersion));
    }
    [Test]
    public void UnversionedOrUnsavedSelectionCannotProduceAnUpdateToken()
    {
        using var form = new Form(); var table = Records(); using var grid = Bind(form, table);
        Assert.Throws<InvalidOperationException>(() => RecordSelection.OriginalVersion(grid));
        table.Rows.Add(33333333333L, "Local", 100m, FirstVersion.ToArray()); grid.Rows[2].Selected = true;
        Assert.Throws<InvalidOperationException>(() => RecordSelection.OriginalVersion(grid));
        grid.ClearSelection(); table.Columns.Remove(PersonnelConcurrency.ColumnName); grid.Rows[0].Selected = true;
        Assert.Throws<InvalidOperationException>(() => RecordSelection.OriginalVersion(grid));
    }
    [TestCase(null)]
    [TestCase("not-base64")]
    [TestCase("AAAAAAAAAAI=")]
    public void LegacyOrStaleDraftCannotAttachToFreshRowVersion(string? draftVersion)
    {
        using var form = new Form(); using var grid = Bind(form, Records());
        grid.Rows[0].Selected = true;
        Assert.That(RecordSelection.RestoreDraftSelection(grid, "11111111111", draftVersion), Is.False);
        Assert.That(grid.SelectedRows.Count, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => RecordSelection.OriginalVersion(grid));
    }
    [Test]
    public void MatchingDraftCanSelectAfterSortingButCannotRedirectToAnotherPerson()
    {
        using var form = new Form(); var table = Records(); using var grid = Bind(form, table);
        table.DefaultView.Sort = "TC DESC";
        Assert.That(RecordSelection.RestoreDraftSelection(grid, "11111111111", Convert.ToBase64String(FirstVersion)), Is.True);
        Assert.That(RecordSelection.TryGetTc(grid, out long tc), Is.True); Assert.That(tc, Is.EqualTo(11111111111L));
        Assert.That(RecordSelection.RestoreDraftSelection(grid, "22222222222", Convert.ToBase64String(FirstVersion)), Is.False);
        Assert.That(grid.SelectedRows.Count, Is.Zero);
    }
    [Test]
    public void VersionIsHiddenAndCannotBeRestoredOrExportedByColumnPreferences()
    {
        string directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Versions_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            using var form = new Form(); var table = Records(); using var grid = Bind(form, table);
            Assert.That(grid.Columns[PersonnelConcurrency.ColumnName]!.Visible, Is.False);
            string prefs = Path.Combine(directory, "grid.json");
            File.WriteAllText(prefs, """[{"Name":"RecordVersion","Width":150,"Index":0,"Visible":true}]""");
            GridPreferences.Restore(grid, prefs);
            Assert.That(grid.Columns[PersonnelConcurrency.ColumnName]!.Visible, Is.False);
            grid.Columns[PersonnelConcurrency.ColumnName]!.Visible = true;
            var snapshot = ReportExport.Snapshot(grid);
            Assert.That(snapshot.Columns.Contains(PersonnelConcurrency.ColumnName), Is.False);
            GridPreferences.Save(grid, prefs);
            Assert.That(File.ReadAllText(prefs), Does.Not.Contain(PersonnelConcurrency.ColumnName));

            string excel = Path.Combine(directory, "report.xlsx"), csv = Path.Combine(directory, "report.csv");
            ReportExport.Excel(table, excel); ReportExport.Csv(table, csv);
            using var book = new XLWorkbook(excel);
            Assert.That(book.Worksheet(1).Row(1).CellsUsed().Select(c => c.GetString()), Does.Not.Contain(PersonnelConcurrency.ColumnName));
            Assert.That(File.ReadAllText(csv), Does.Not.Contain(PersonnelConcurrency.ColumnName));
            Assert.That(table.Columns.Contains(PersonnelConcurrency.ColumnName), Is.True);
        }
        finally { Directory.Delete(directory, true); }
    }
}
