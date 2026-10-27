using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class SummaryResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ReflowPreservesTotalsLeaveRowsAndSelectionAndKeepsEachSectionReachable(UserRole role)
    {
        using var form = Preview(role);
        Assert.That(form.BackColor, Is.Not.EqualTo(Color.Orange), "The previous orange design must not flash before Show.");
        form.Show(); AppTheme.Apply(form, classicOverride: true);
        var defaultSize = form.Size;
        using var data = Records(); form.DisplaySummary(data);
        var grid = AppTheme.Descendants(form).OfType<DataGridView>().Single();
        var view = (DataTable)grid.DataSource!;
        view.DefaultView.Sort = "TC DESC"; grid.Rows[0].Selected = true;
        var workspace = AppTheme.Descendants(form).OfType<SummaryWorkspace>().Single();
        var cards = workspace.Controls.OfType<SummaryMetricCard>().ToArray();
        var counts = cards.Where(c => c.Value.Name is not ("TotalBalance" or "AverageBalance")).ToArray();
        var amounts = cards.Except(counts).ToArray();
        var leaves = workspace.Controls.OfType<SummaryLeavePanel>().Single();
        var metrics = cards.ToDictionary(c => c.Value.Name, c => c.Value.Text);
        Assert.That(metrics["toplamLbl"], Is.EqualTo("3"));
        Assert.That(metrics["TotalBalance"], Is.EqualTo($"{6000m:N2} TL"));
        var version = RecordSelection.OriginalVersion(grid);
        var countColumns = new HashSet<int>(); var amountColumns = new HashSet<int>();
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 800, 1140 }).ToArray();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            form.Size = form.LogicalToDeviceUnits(new Size(width, 734));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            countColumns.Add(counts.GroupBy(c => c.Top).Max(g => g.Count()));
            amountColumns.Add(amounts.GroupBy(c => c.Top).Max(g => g.Count()));
            var panels = cards.Cast<Control>().Append(leaves).ToArray();
            foreach (var panel in panels)
            {
                Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
            }
            for (int i = 0; i < panels.Length; i++)
                for (int j = i + 1; j < panels.Length; j++) Assert.That(panels[i].Bounds.IntersectsWith(panels[j].Bounds), Is.False);
            foreach (var card in cards)
            {
                Assert.That(card.Value.Text, Is.EqualTo(metrics[card.Value.Name]));
                AssertValueFits(card);
            }
            Assert.That(grid.DataSource, Is.SameAs(view));
            Assert.That(view.Rows.Count, Is.EqualTo(3));
            Assert.That(RecordSelection.TryGetTc(grid, out long tc) && tc == 33333333333L, Is.True);
            Assert.That(RecordSelection.OriginalVersion(grid), Is.EqualTo(version));
        }
        Assert.That(countColumns, Is.EquivalentTo(new[] { 2, 4 }));
        Assert.That(amountColumns, Is.EquivalentTo(new[] { 1, 2 }));
        Assert.That(grid.ReadOnly, Is.True);
        Assert.That(grid.Columns.Cast<DataGridViewColumn>().Where(RecordSelection.IsInternalColumn).All(c => !c.Visible), Is.True);
        form.Size = form.LogicalToDeviceUnits(new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        void Jump(string name) { AppTheme.Descendants(form).OfType<Button>().Single(b => b.Name == name).PerformClick(); Application.DoEvents(); }
        Jump("summaryShowPeople"); Assert.That(counts.All(c => workspace.ClientRectangle.Contains(c.Bounds)), Is.True);
        Jump("summaryShowBalances"); Assert.That(amounts.All(c => workspace.ClientRectangle.Contains(c.Bounds)), Is.True);
        Jump("summaryShowLeaves"); Assert.That(workspace.ClientRectangle.Contains(leaves.Bounds), Is.True);
        var note = AppTheme.Descendants(leaves).OfType<Label>().Single();
        Assert.That(TextRenderer.MeasureText(note.Text, note.Font, new Size(note.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(note.Height));
        Assert.That(leaves.ClientRectangle.Contains(grid.Bounds), Is.True);
        form.Size = defaultSize; form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False);
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        Assert.That(cards.All(c => c.Value.Text == metrics[c.Value.Name]), Is.True);
        Assert.That(grid.DataSource, Is.SameAs(view));
        Assert.That(data.Rows.Cast<DataRow>().All(r => r.RowState == DataRowState.Unchanged), Is.True);
    }

    [Test]
    public void ExtremeAmountsRemainCompleteAcrossSizesAndThemes()
    {
        using var form = Preview(UserRole.Admin); form.Show();
        foreach (decimal amount in new[] { decimal.MaxValue, decimal.MinValue })
        {
            using var data = Records(); data.Rows.Clear();
            data.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", amount, DBNull.Value, DBNull.Value, new byte[8]);
            data.AcceptChanges(); form.DisplaySummary(data);
            foreach (var theme in new[] { (Classic: true, Dark: false), (Classic: false, Dark: false), (Classic: false, Dark: true) })
            foreach (int width in new[] { 560, 800, 860, 1136, 1920 })
            {
                AppTheme.Apply(form, darkOverride: theme.Dark, classicOverride: theme.Classic);
                form.Size = form.LogicalToDeviceUnits(new Size(width, 734)); form.PerformLayout(); Application.DoEvents();
                foreach (var card in AppTheme.Descendants(form).OfType<SummaryMetricCard>().Where(c => c.Value.Name is "TotalBalance" or "AverageBalance"))
                {
                    Assert.That(card.Value.Text, Is.EqualTo($"{amount:N2} TL"));
                    Assert.That(card.Value.AccessibleDescription, Is.EqualTo(card.Value.Text));
                    AssertValueFits(card);
                }
            }
        }
    }

    private static void AssertValueFits(SummaryMetricCard card)
    {
        Assert.That(card.ClientRectangle.Contains(card.Value.Bounds), Is.True);
        var text = TextRenderer.MeasureText(card.Value.Text, card.Value.Font, Size.Empty, TextFormatFlags.SingleLine);
        Assert.That(text.Width, Is.LessThanOrEqualTo(card.Value.ClientSize.Width), card.Text);
        Assert.That(text.Height, Is.LessThanOrEqualTo(card.Value.ClientSize.Height), card.Text);
    }
    private static Summary Preview(UserRole role) => new() { Session = new("preview-user", role), PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
    private static DataTable Records()
    {
        var data = new DataTable(); data.Columns.Add("TC", typeof(long));
        foreach (string name in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) data.Columns.Add(name);
        data.Columns.Add("PARA", typeof(decimal)); data.Columns.Add("IZINBASLANGIC", typeof(DateTime)); data.Columns.Add("IZINBITIS", typeof(DateTime));
        data.Columns.Add(PersonnelConcurrency.ColumnName, typeof(byte[]));
        void Row(long tc, string group, decimal amount, object start, object end, byte version) => data.Rows.Add(tc, "Ada", "Örnek", "TR000000000000000000000000", group, amount, start, end, new byte[] { 0, 0, 0, 0, 0, 0, 0, version });
        Row(11111111111L, "Genel", 1000m, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1), 1);
        Row(11111111111L, "Genel", 1000m, DateTime.Today.AddDays(5), DateTime.Today.AddDays(7), 1);
        Row(22222222222L, "Joker", 2000m, DBNull.Value, DBNull.Value, 2);
        Row(33333333333L, "Genel", 3000m, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5), 3);
        data.AcceptChanges(); return data;
    }
}
