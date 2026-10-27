using System.Runtime.InteropServices;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class ContactResponsiveTests
{
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ReflowPreservesReadOnlyNumbersSelectionAndCopyFeedbackWithoutUsingRealClipboard(UserRole role)
    {
        var copied = new List<string>(); bool clipboardBusy = false;
        using var form = new Contact(number => { if (clipboardBusy) throw new ExternalException("Test clipboard busy"); copied.Add(number); })
        {
            Session = new("preview-user", role), PreviewOnly = true,
            PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false
        };
        var cards = AppTheme.Descendants(form).OfType<ContactCard>().OrderBy(c => c.TabIndex).ToArray();
        Assert.That(cards.Length, Is.EqualTo(2));
        for (int i = 0; i < cards.Length; i++)
        {
            cards[i].Text = "Örnek iletişim kişisi " + (i + 1);
            cards[i].Phone.Text = "+90 (555) 000 00 0" + (i + 1);
        }
        ResponsiveTestWindow.Show(form, new Size(800, 400)); AppTheme.Apply(form, classicOverride: true);
        var defaultSize = form.Size;
        var workspace = AppTheme.Descendants(form).OfType<ContactWorkspace>().Single();
        var phones = cards.Select(c => c.Phone).ToArray();
        var numbers = phones.Select(p => p.Text).ToArray();
        foreach (var phone in phones) phone.SelectAll();
        var hint = AppTheme.Descendants(form).OfType<Label>().Single(c => c.Name == "contactCopyHint");
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 740, 780 }).ToArray();
        var columnCounts = new HashSet<int>();
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            ResponsiveTestWindow.Resize(form, new Size(width, 600));
            form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, $"Page overflow at {width}");
            columnCounts.Add(cards.GroupBy(c => c.Top).Max(g => g.Count()));
            Assert.That(cards[0].Bounds.IntersectsWith(cards[1].Bounds), Is.False);
            foreach (var card in cards)
            {
                Assert.That(card.Left, Is.GreaterThanOrEqualTo(0));
                Assert.That(card.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width));
                Assert.That(card.ClientRectangle.Contains(card.Phone.Bounds), Is.True);
                Assert.That(card.ClientRectangle.Contains(card.Copy.Bounds), Is.True);
                Assert.That(card.Phone.ReadOnly && card.Phone.TabStop && card.Phone.CanSelect, Is.True);
                Assert.That(card.Copy.Enabled && card.Copy.TabStop, Is.True);
                var text = TextRenderer.MeasureText(card.Phone.Text, card.Phone.Font, Size.Empty, TextFormatFlags.SingleLine);
                Assert.That(text.Width + form.LogicalToDeviceUnits(8), Is.LessThanOrEqualTo(card.Phone.ClientSize.Width));
                Assert.That(card.Phone.SelectedText, Is.EqualTo(card.Phone.Text));
            }
            Assert.That(phones.Select(p => p.Text), Is.EqualTo(numbers));
            AssertHintFits(hint);
        }
        Assert.That(columnCounts, Is.EquivalentTo(new[] { 1, 2 }));
        ResponsiveTestWindow.Resize(form, new Size(560, 420)); form.PerformLayout(); Application.DoEvents();
        foreach (var control in cards.SelectMany(c => new Control[] { c.Phone, c.Copy }))
        {
            control.Focus(); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), Is.True, control.Name);
        }
        cards[1].Copy.PerformClick();
        Assert.That(copied, Is.EqualTo(new[] { numbers[1] }));
        Assert.That(hint.Text, Does.Contain(cards[1].Text).And.Contain("kopyalandı")); AssertHintFits(hint);
        clipboardBusy = true; cards[0].Copy.PerformClick();
        Assert.That(copied.Count, Is.EqualTo(1));
        Assert.That(hint.Text, Does.Contain("Pano şu anda kullanılamıyor")); AssertHintFits(hint);
        Assert.That(phones.Select(p => p.Text), Is.EqualTo(numbers));
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False);
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        Assert.That(cards.All(c => workspace.ClientRectangle.Contains(c.Bounds)), Is.True);
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False);
            Assert.That(phones.Select(p => p.Text), Is.EqualTo(numbers));
            AssertHintFits(hint);
        }
    }

    private static void AssertHintFits(Label hint)
    {
        Assert.That(hint.Parent!.ClientRectangle.Contains(hint.Bounds), Is.True);
        var text = TextRenderer.MeasureText(hint.Text, hint.Font, new Size(hint.Width, 0), TextFormatFlags.WordBreak);
        Assert.That(text.Height, Is.LessThanOrEqualTo(hint.Height));
    }
}
