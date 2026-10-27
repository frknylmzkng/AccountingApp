using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class GeneralResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ReflowKeepsUnfinishedEditsAndSelectionAndAllowsAccessToEverySection(UserRole role)
    {
        using var form = new Genel
        {
            Session = new("preview-user", role), PreviewOnly = true,
            PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false
        };
        ResponsiveTestWindow.Show(form, new Size(1120, 650)); AppTheme.Apply(form, classicOverride: true);
        var defaultSize = form.Size;
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        using var data = Records();
        grid.BindingContext = new BindingContext(); grid.DataSource = data;
        grid.ClearSelection(); grid.Rows[0].Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<GeneralPersonnelWorkspace>().Single();
        var editor = workspace.Controls.OfType<PersonnelEditor>().Single();
        var leave = workspace.Controls.OfType<GeneralLeaveEditor>().Single();
        var records = workspace.Controls.Find("generalRecords", false).Single();
        var ad = AppTheme.Descendants(form).OfType<TextBox>().Single(c => c.Name == "ad_txt");
        ad.Text = "Henüz kaydedilmemiş ad";
        var dates = AppTheme.Descendants(form).OfType<DateTimePicker>().OrderBy(c => c.Name).ToArray();
        dates[0].Value = new DateTime(2026, 11, 1); dates[1].Value = new DateTime(2026, 11, 7);
        var version = RecordSelection.OriginalVersion(grid);
        var leaveVersion = LeaveTarget.FromSelection(grid).Single();
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 1136, 1140 }).ToArray();
        bool sawStacked = false, sawPaired = false;
        var fieldColumns = new HashSet<int>();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 734));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            sawStacked |= leave.Top > editor.Top; sawPaired |= leave.Top == editor.Top;
            fieldColumns.Add(editor.Controls.OfType<TextBox>().GroupBy(c => c.Top).Max(g => g.Count()));
            foreach (var panel in new Control[] { editor, leave, records })
            {
                Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
            }
            Assert.That(editor.Bounds.IntersectsWith(leave.Bounds) || editor.Bounds.IntersectsWith(records.Bounds) || leave.Bounds.IntersectsWith(records.Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or DateTimePicker or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, $"{control.Name}: {control.Bounds}");
            foreach (var button in editor.Controls.OfType<Button>())
            {
                var text = TextRenderer.MeasureText(button.Text.Replace("&", ""), button.Font);
                Assert.That(text.Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width));
            }
            Assert.That(grid.DataSource, Is.SameAs(data));
            Assert.That(grid.SelectedRows.Count, Is.EqualTo(1));
            Assert.That(RecordSelection.TryGetTc(grid, out long tc) && tc == 11111111111L, Is.True);
            Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(version));
            Assert.That(LeaveTarget.FromSelection(grid).Single().LeaveVersion, Is.EqualTo(leaveVersion.LeaveVersion));
            Assert.That(ad.Text, Is.EqualTo("Henüz kaydedilmemiş ad"));
            Assert.That(dates[0].Value.Date, Is.EqualTo(new DateTime(2026, 11, 1)));
            Assert.That(dates[1].Value.Date, Is.EqualTo(new DateTime(2026, 11, 7)));
        }
        Assert.That(sawStacked && sawPaired, Is.True);
        Assert.That(fieldColumns, Is.EquivalentTo(new[] { 1, 2, 3 }));
        Assert.That(editor.Controls.OfType<Button>().Where(b => b.Name != "temizle_btn").All(b => b.Enabled == (role != UserRole.Viewer)), Is.True);
        Assert.That(dates.All(d => d.Enabled == (role != UserRole.Viewer)), Is.True);
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Where(RecordSelection.IsInternalColumn).All(c => !c.Visible), Is.True);

        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "generalShowList").PerformClick();
        Application.DoEvents(); Assert.That(workspace.ClientRectangle.Contains(records.Bounds), Is.True);
        AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == "generalShowEditor").PerformClick();
        Application.DoEvents(); Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        foreach (var field in editor.Controls.OfType<TextBox>())
        {
            field.Focus(); Application.DoEvents();
            var bounds = workspace.RectangleToClient(field.RectangleToScreen(field.ClientRectangle));
            Assert.That(workspace.ClientRectangle.Contains(bounds), Is.True, field.Name + " inaccessible by keyboard");
        }
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False, "Growing must not keep the old stacked/scrolling layout.");
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False);
            var status = AppTheme.Descendants(leave).OfType<Label>().Single(c => c.Name == "izinDurumLabel");
            var text = TextRenderer.MeasureText(status.Text, status.Font, new Size(status.Width, 0), TextFormatFlags.WordBreak);
            Assert.That(text.Height, Is.LessThanOrEqualTo(status.Height));
        }
    }

    private static DataTable Records()
    {
        var data = new DataTable(); data.Columns.Add("TC", typeof(long));
        foreach (string name in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) data.Columns.Add(name);
        data.Columns.Add("PARA", typeof(decimal)); data.Columns.Add("IZINBASLANGIC", typeof(DateTime)); data.Columns.Add("IZINBITIS", typeof(DateTime));
        data.Columns.Add("IzinSuresi", typeof(int)); data.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[])); data.Columns.Add(LeaveTarget.ColumnName, typeof(byte[]));
        data.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500m, new DateTime(2026, 10, 28), new DateTime(2026, 10, 30), 3, new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }, new byte[] { 0, 0, 0, 0, 0, 0, 0, 2 });
        data.AcceptChanges(); return data;
    }
}
