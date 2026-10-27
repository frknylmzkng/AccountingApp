using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class JokerResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ReflowPreservesSortedSelectionUnfinishedFieldsAndVersionAndKeepsCommandsReachable(UserRole role)
    {
        using var form = new Joker
        {
            Session = new("preview-user", role), PreviewOnly = true,
            PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false
        };
        ResponsiveTestWindow.Show(form, new Size(1040, 650)); AppTheme.Apply(form, classicOverride: true);
        var defaultSize = form.Size;
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        using var data = Records();
        grid.BindingContext = new BindingContext(); grid.DataSource = data;
        data.DefaultView.Sort = "TC DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<JokerPersonnelWorkspace>().Single();
        var editor = workspace.Controls.OfType<PersonnelEditor>().Single();
        var records = workspace.Controls.Find("jokerRecords", false).Single();
        var fields = editor.Controls.OfType<TextBox>().ToArray();
        fields.Single(c => c.Name == "ad_txt").Text = "Henüz kaydedilmemiş ad";
        fields.Single(c => c.Name == "IBAN_txt").Text = "TR00 0000 0000 0000 0000 0000 00";
        var unfinished = fields.ToDictionary(c => c.Name, c => c.Text);
        var version = RecordSelection.OriginalVersion(grid);
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 660, 940 }).ToArray();
        var columnCounts = new HashSet<int>();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 734));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            columnCounts.Add(fields.GroupBy(c => c.Top).Max(g => g.Count()));
            foreach (var panel in new Control[] { editor, records })
            {
                Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
            }
            Assert.That(editor.Bounds.IntersectsWith(records.Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name);
            foreach (var button in editor.Controls.OfType<Button>())
            {
                var text = TextRenderer.MeasureText(button.Text.Replace("&", ""), button.Font);
                Assert.That(text.Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width));
            }
            Assert.That(grid.DataSource, Is.SameAs(data));
            Assert.That(grid.SelectedRows.Count, Is.EqualTo(1));
            Assert.That(RecordSelection.TryGetTc(grid, out long tc) && tc == 22222222222L, Is.True);
            Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(version));
            Assert.That(fields.All(f => f.Text == unfinished[f.Name]), Is.True);
        }
        Assert.That(columnCounts, Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Select(c => c.DataPropertyName),
            Is.EquivalentTo(new[] { "TC", "AD", "SOYAD", "IBAN", "PARA", "GRUP" }));
        Assert.That(editor.Controls.OfType<Button>().Where(b => b.Name != "jokerClear").All(b => b.Enabled == (role != UserRole.Viewer)), Is.True);
        Assert.That(AppTheme.Descendants(form).OfType<DateTimePicker>(), Is.Empty);

        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "jokerShowList").PerformClick();
        Application.DoEvents(); Assert.That(workspace.ClientRectangle.Contains(records.Bounds), Is.True);
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "jokerShowEditor").PerformClick();
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
        Assert.That(fields.All(f => f.Text == unfinished[f.Name]), Is.True);
        editor.Controls.OfType<Button>().Single(b => b.Name == "jokerClear").PerformClick();
        Assert.That(fields.All(f => f.Text.Length == 0), Is.True);
        Assert.That(grid.SelectedRows.Count, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => RecordSelection.OriginalVersion(grid));
        Assert.That(data.Rows.Cast<DataRow>().All(r => r.RowState == DataRowState.Unchanged), Is.True);
    }

    private static DataTable Records()
    {
        var data = new DataTable(); data.Columns.Add("TC", typeof(long));
        foreach (string name in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) data.Columns.Add(name);
        data.Columns.Add("PARA", typeof(decimal)); data.Columns.Add("IZINBASLANGIC", typeof(DateTime)); data.Columns.Add("IZINBITIS", typeof(DateTime));
        data.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[]));
        data.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Joker", 1500m, DBNull.Value, DBNull.Value, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 });
        data.Rows.Add(22222222222L, "Ece", "Örnek", "TR000000000000000000000000", "Joker", 2500m, DBNull.Value, DBNull.Value, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 });
        data.AcceptChanges(); return data;
    }
}
