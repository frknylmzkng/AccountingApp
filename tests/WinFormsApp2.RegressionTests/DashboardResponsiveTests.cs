using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class DashboardResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ResizingReflowsActionsWithoutClippingAndKeepsLastActionReachable(UserRole role)
    {
        using var form = new Dashboard
        {
            Session = new("preview-user", role), PreviewOnly = true,
            PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false
        };
        ResponsiveTestWindow.Show(form, new Size(1040, 640)); AppTheme.Apply(form, classicOverride: true);
        var workspace = AppTheme.Descendants(form).OfType<DashboardWorkspace>().Single();
        var cards = workspace.Controls.OfType<DashboardActionCard>().OrderBy(c => c.TabIndex).ToArray();
        Assert.That(cards.Length, Is.EqualTo(8));
        var columnCounts = new HashSet<int>();
        // Exercise both resize directions and the widths around the navigation
        // and column transitions, rather than checking a few static screenshots.
        var widths = Enumerable.Range(0, 47).Select(i => 560 + i * 30).ToArray();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 600));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Horizontal overflow at {width}");
            columnCounts.Add(cards.GroupBy(c => c.Top).Max(g => g.Count()));
            foreach (var card in cards)
            {
                Assert.That(card.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(card.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
                Assert.That(card.ClientRectangle.Contains(card.Action.Bounds), Is.True);
                Assert.That(card.ClientRectangle.Contains(card.Description.Bounds), Is.True);
                var text = TextRenderer.MeasureText(card.Description.Text, card.Description.Font,
                    new Size(card.Description.Width, 0), TextFormatFlags.WordBreak);
                Assert.That(text.Height, Is.LessThanOrEqualTo(card.Description.Height), card.Description.Text);
            }
            for (int i = 0; i < cards.Length; i++)
                for (int j = i + 1; j < cards.Length; j++)
                    Assert.That(cards[i].Bounds.IntersectsWith(cards[j].Bounds), Is.False);
        }
        Assert.That(columnCounts, Is.EquivalentTo(new[] { 1, 2, 3, 4 }));
        Assert.That(AppTheme.Descendants(workspace).OfType<TreeView>().Single().Visible, Is.False);
        Assert.That(workspace.VerticalScroll.Visible, Is.True);
        workspace.ScrollControlIntoView(cards.Last()); Application.DoEvents();
        Assert.That(workspace.ClientRectangle.Contains(cards.Last().Bounds), Is.True);
        Assert.That(cards.Last().Action.CanSelect, Is.True);
        Assert.That(cards.All(c => c.Action.TabStop), Is.True);
        ResponsiveTestWindow.Resize(form, new Size(1920, 1080));
        form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.HorizontalScroll.Visible || workspace.VerticalScroll.Visible, Is.False);
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        Assert.That(AppTheme.Descendants(workspace).OfType<TreeView>().Single().Visible, Is.True);
        Assert.That(cards.All(c => workspace.ClientRectangle.Contains(c.Bounds)), Is.True);
    }
}
