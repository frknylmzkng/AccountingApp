using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class LeavePlanResponsiveTests
{
    [TestCase(UserRole.Admin, 0), TestCase(UserRole.Admin, 1), TestCase(UserRole.Admin, 2)]
    [TestCase(UserRole.PersonnelManager, 0), TestCase(UserRole.PersonnelManager, 1), TestCase(UserRole.PersonnelManager, 2)]
    [TestCase(UserRole.Viewer, 0), TestCase(UserRole.Viewer, 1), TestCase(UserRole.Viewer, 2)]
    public void ReflowPreservesDatesMultiSelectionAndOriginalPersonAndLeaveVersions(UserRole role, int periodCase)
    {
        using var form = new LeavePlanForm { Session = new("preview-user", role), PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        ResponsiveTestWindow.Show(form, new Size(1120, 680)); AppTheme.Apply(form, classicOverride: true); var defaultSize = form.Size;
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        using var data = Records();
        // Current display values must never replace original identity/version snapshots.
        data.Rows[0]["TC"] = 33333333333L; data.Rows[0][PersonnelConcurrency.ColumnName] = Version(8);
        data.Rows[0][LeaveTarget.ColumnName] = Version(9); data.Rows[0]["IZINBASLANGIC"] = new DateTime(2026, 12, 1);
        grid.BindingContext = new BindingContext(); grid.DataSource = data; data.DefaultView.Sort = "TC DESC";
        grid.ClearSelection(); foreach (DataGridViewRow row in grid.Rows) row.Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<LeavePlanWorkspace>().Single();
        var editor = workspace.Controls.OfType<LeavePlanEditor>().Single();
        var records = workspace.Controls.Find("leavePlanRecords", false).Single();
        var start = editor.Controls.OfType<DateTimePicker>().Single(c => c.Name == "leavePlanStart");
        var end = editor.Controls.OfType<DateTimePicker>().Single(c => c.Name == "leavePlanEnd");
        var duration = editor.Controls.OfType<Label>().Single(c => c.Name == "leavePlanDuration");
        var count = editor.Controls.OfType<Label>().Single(c => c.Name == "SelectedLeavePeople");
        var save = editor.Controls.OfType<Button>().Single();
        start.Value = new DateTime(2026, 11, 10); end.Value = start.Value.AddDays(periodCase == 0 ? 4 : periodCase == 1 ? 0 : -1);
        string expectedDuration = periodCase == 2 ? "Tarih aralığını kontrol edin." : "Süre: " + (periodCase == 0 ? 5 : 1) + " gün";
        Assert.That(duration.Text, Is.EqualTo(expectedDuration)); Assert.That(count.Text, Is.EqualTo("Seçili kişi: 2"));
        var originalTargets = Snapshots(grid); var originalRows = Values(data);
        var controls = AppTheme.Descendants(form).ToArray();
        var enabled = controls.Where(c => c is Button or DateTimePicker or DataGridView).ToDictionary(c => c, c => c.Enabled);
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 660, 940 }).ToArray();
        var arrangements = new HashSet<string>();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 780)); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, "Page overflow " + width);
            foreach (var panel in new Control[] { editor, records })
            { Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width)); }
            Assert.That(editor.Bounds.IntersectsWith(records.Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is Button or DateTimePicker or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name + " at " + width);
            foreach (var label in editor.Controls.OfType<Label>())
                Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(label.Height), label.Name);
            arrangements.Add(start.Top != end.Top ? "stacked" : duration.Top < end.Bottom ? "three" : "two");
            Assert.That(grid.DataSource, Is.SameAs(data)); Assert.That(grid.SelectedRows.Count, Is.EqualTo(3));
            Assert.That(Snapshots(grid), Is.EqualTo(originalTargets)); Assert.That(Values(data), Is.EqualTo(originalRows));
            Assert.That(start.Value, Is.EqualTo(new DateTime(2026, 11, 10))); Assert.That(end.Value, Is.EqualTo(start.Value.AddDays(periodCase == 0 ? 4 : periodCase == 1 ? 0 : -1)));
            Assert.That(count.Text, Is.EqualTo("Seçili kişi: 2")); Assert.That(duration.Text, Is.EqualTo(expectedDuration));
            Assert.That(enabled.All(p => p.Key.Enabled == p.Value), Is.True);
            Assert.That(AppTheme.Descendants(form), Is.EquivalentTo(controls));
        }
        Assert.That(arrangements, Is.EquivalentTo(new[] { "stacked", "two", "three" }));
        Assert.That(save.Enabled, Is.EqualTo(role != UserRole.Viewer)); Assert.That(grid.MultiSelect && grid.ReadOnly, Is.True);
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Any(RecordSelection.IsInternalColumn), Is.False);
        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "leavePlanShowList").PerformClick(); Application.DoEvents();
        Assert.That(workspace.ClientRectangle.Contains(records.Bounds), Is.True);
        AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "leavePlanShowEditor").PerformClick(); Application.DoEvents();
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        foreach (var control in new Control[] { start, end, save })
        {
            workspace.ScrollControlIntoView(control); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), Is.True, control.Name);
        }
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.HorizontalScroll.Visible || workspace.VerticalScroll.Visible, Is.False);
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            foreach (var label in editor.Controls.OfType<Label>())
                Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(label.Height));
            Assert.That(Snapshots(grid), Is.EqualTo(originalTargets)); Assert.That(count.Text, Is.EqualTo("Seçili kişi: 2"));
        }
        grid.ClearSelection(); Assert.That(count.Text, Is.EqualTo("Seçili kişi: 0"));
        grid.Rows[0].Selected = true; Assert.That(count.Text, Is.EqualTo("Seçili kişi: 1"));
        Assert.That(LeaveTarget.FromSelection(grid).Single().Tc, Is.EqualTo(11111111111L));
        Assert.That(Values(data), Is.EqualTo(originalRows)); Assert.That(enabled.All(p => p.Key.Enabled == p.Value), Is.True);
        // Save is not invoked: no plan confirmation, query, database write or preferences save.
    }

    private static string Snapshots(DataGridView grid) => string.Join("|", LeaveTarget.FromSelection(grid).OrderBy(t => t.Tc)
        .Select(t => t.Tc + ":" + Convert.ToHexString(t.PersonVersion) + ":" + (t.LeaveVersion == null ? "absent" : Convert.ToHexString(t.LeaveVersion))));
    private static string Values(DataTable data) => string.Join("|", data.Rows.Cast<DataRow>().Select(r => r.RowState + ":" + string.Join(";", data.Columns.Cast<DataColumn>().SelectMany(c => new[] { DataRowVersion.Original, DataRowVersion.Current }.Select(v => r[c, v] is byte[] b ? Convert.ToHexString(b) : Convert.ToString(r[c, v], System.Globalization.CultureInfo.InvariantCulture))))));
    private static byte[] Version(byte value) => [0, 0, 0, 0, 0, 0, 0, value];
    internal static DataTable Records()
    {
        var data = new DataTable(); data.Columns.Add("TC", typeof(long)); data.Columns.Add("AD"); data.Columns.Add("SOYAD"); data.Columns.Add("GRUP");
        data.Columns.Add("IZINBASLANGIC", typeof(DateTime)); data.Columns.Add("IZINBITIS", typeof(DateTime)); data.Columns.Add("IzinSuresi", typeof(int));
        data.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[])); data.Columns.Add(LeaveTarget.ColumnName, typeof(byte[]));
        data.Rows.Add(11111111111L, "Ada", "Örnek", "Genel", new DateTime(2026, 10, 1), new DateTime(2026, 10, 3), 3, Version(1), Version(2)); data.AcceptChanges(); data.ImportRow(data.Rows[0]);
        data.Rows.Add(22222222222L, "Ece", "Örnek", "Genel", DBNull.Value, DBNull.Value, DBNull.Value, Version(3), DBNull.Value); data.AcceptChanges(); return data;
    }
}
