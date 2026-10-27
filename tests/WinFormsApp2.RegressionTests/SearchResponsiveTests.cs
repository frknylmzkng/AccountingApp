using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class SearchResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ReflowPreservesFiltersResultsAndSortedSelectionAndKeepsBothSectionsReachable(UserRole role)
    {
        using var form = new Search
        {
            Session = new("preview-user", role), PreviewOnly = true,
            PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false
        };
        ResponsiveTestWindow.Show(form, new Size(1120, 650)); AppTheme.Apply(form, classicOverride: true);
        var defaultSize = form.Size;
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        using var data = Records(); grid.BindingContext = new BindingContext(); grid.DataSource = data;
        data.DefaultView.Sort = "TC DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<SearchWorkspace>().Single();
        var filters = workspace.Controls.OfType<SearchFilterPanel>().Single();
        var results = workspace.Controls.Find("searchResults", false).Single();
        var fields = filters.Controls.OfType<TextBox>().ToArray();
        fields.Single(c => c.Name == "txtTC").Text = "111";
        fields.Single(c => c.Name == "txtAd").Text = "Henüz aranmamış ad";
        fields.Single(c => c.Name == "txtSoyad").Text = "Örnek";
        var terms = fields.ToDictionary(c => c.Name, c => c.Text);
        var version = RecordSelection.OriginalVersion(grid);
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 660, 940 }).ToArray();
        var columnCounts = new HashSet<int>();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 734));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            columnCounts.Add(fields.GroupBy(c => c.Top).Max(g => g.Count()));
            foreach (var panel in new Control[] { filters, results })
            {
                Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
            }
            Assert.That(filters.Bounds.IntersectsWith(results.Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name);
            foreach (var button in filters.Controls.OfType<Button>())
            {
                var text = TextRenderer.MeasureText(button.Text.Replace("&", ""), button.Font);
                Assert.That(text.Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width));
            }
            Assert.That(grid.DataSource, Is.SameAs(data));
            Assert.That(grid.SelectedRows.Count, Is.EqualTo(1));
            Assert.That(RecordSelection.TryGetTc(grid, out long tc) && tc == 22222222222L, Is.True);
            Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(version));
            Assert.That(fields.All(f => f.Text == terms[f.Name]), Is.True);
        }
        Assert.That(columnCounts, Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.That(grid.ReadOnly, Is.True);
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Where(RecordSelection.IsInternalColumn).All(c => !c.Visible), Is.True);
        Assert.That(filters.Controls.OfType<Button>().All(b => b.Enabled && b.TabStop), Is.True);
        Assert.That(form.AcceptButton, Is.SameAs(filters.Controls.OfType<Button>().Single(b => b.Name == "Ara")));
        Assert.That(fields.Single(c => c.Name == "txtTC").MaxLength, Is.EqualTo(11));

        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "searchShowResults").PerformClick();
        Application.DoEvents(); Assert.That(workspace.ClientRectangle.Contains(results.Bounds), Is.True);
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "searchShowFilters").PerformClick();
        Application.DoEvents(); Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        foreach (var field in fields)
        {
            field.Focus(); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(field.RectangleToScreen(field.ClientRectangle))), Is.True, field.Name);
        }
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False);
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False);
            Assert.That(fields.All(f => f.Parent!.ClientRectangle.Contains(f.Bounds)), Is.True);
        }
        Assert.That(fields.All(f => f.Text == terms[f.Name]), Is.True);
        filters.Controls.OfType<Button>().Single(b => b.Name == "searchClear").PerformClick();
        Assert.That(fields.All(f => f.Text.Length == 0), Is.True);
        Assert.That(fields.Single(f => f.Name == "txtTC").Focused, Is.True);
        Assert.That(grid.DataSource, Is.SameAs(data), "Clearing filters must not execute a new query.");
        Assert.That(grid.SelectedRows.Count, Is.EqualTo(1));
        Assert.That(data.Rows.Cast<DataRow>().All(r => r.RowState == DataRowState.Unchanged), Is.True);
    }

    private static DataTable Records()
    {
        var data = new DataTable(); data.Columns.Add("TC", typeof(long));
        foreach (string name in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) data.Columns.Add(name);
        data.Columns.Add("PARA", typeof(decimal)); data.Columns.Add("IZINBASLANGIC", typeof(DateTime)); data.Columns.Add("IZINBITIS", typeof(DateTime));
        data.Columns.Add("IzinSuresi", typeof(int)); data.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[])); data.Columns.Add(LeaveTarget.ColumnName, typeof(byte[]));
        data.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500m, new DateTime(2026, 10, 28), new DateTime(2026, 10, 30), 3, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 });
        data.Rows.Add(22222222222L, "Ece", "Örnek", "TR000000000000000000000000", "Joker", 2500m, DBNull.Value, DBNull.Value, DBNull.Value, new byte[] { 0, 0, 0, 0, 0, 0, 0, 3 }, DBNull.Value);
        data.AcceptChanges(); return data;
    }
}
