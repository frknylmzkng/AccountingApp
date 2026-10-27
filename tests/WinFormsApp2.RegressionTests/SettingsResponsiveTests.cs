using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class SettingsResponsiveTests
{
    [TestCase(0, 0), TestCase(0, 1), TestCase(0, 2)]
    [TestCase(1, 0), TestCase(1, 1), TestCase(1, 2)]
    [TestCase(2, 0), TestCase(2, 1), TestCase(2, 2)]
    public void ReflowPreservesUnsavedPathsThemesStatusAndDisabledActions(int theme, int statusCase)
    {
        using var form = new SettingsForm { Session = new("preview-admin", UserRole.Admin), PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        form.Show(); AppTheme.Apply(form, classicOverride: theme == 0, darkOverride: theme == 2);
        var workspace = AppTheme.Descendants(form).OfType<SettingsWorkspace>().Single();
        var cards = workspace.Controls.OfType<SettingsCard>().OrderBy(c => c.TabIndex).ToArray();
        var actions = workspace.Controls.OfType<SettingsActions>().Single();
        var fields = AppTheme.Descendants(form).OfType<TextBox>().ToArray();
        foreach (var field in fields) field.Text = @"C:\Örnek ayarlar\Kaydedilmemiş klasör\" + field.Name + @"\uzun-dosya-yolu.json";
        var classic = AppTheme.Descendants(form).OfType<CheckBox>().Single(c => c.Name == "classicTheme");
        var dark = AppTheme.Descendants(form).OfType<CheckBox>().Single(c => c.Name == "darkTheme");
        classic.Checked = false; dark.Checked = true; classic.Checked = theme == 0;
        Assert.That(dark.Enabled, Is.EqualTo(!classic.Checked));
        var status = AppTheme.Descendants(form).OfType<Label>().Single(c => c.Name == "backupStatus");
        status.Text = Status(statusCase);
        var originals = fields.ToDictionary(c => c, c => c.Text);
        var retry = AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "settingsRetryMirror");
        retry.Enabled = false; // Reflow must preserve an action disabled by the operation state.
        var enabled = AppTheme.Descendants(form).Where(c => c is TextBox or Button or CheckBox).ToDictionary(c => c, c => c.Enabled);
        var originalControls = AppTheme.Descendants(form).ToArray();
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 1040, 1600 }).ToArray();
        bool paired = false, stacked = false, inline = false, labelledAbove = false;
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            form.Size = form.LogicalToDeviceUnits(new Size(width, 900)); form.PerformLayout(); Application.DoEvents();
            Assert.That(workspace.HorizontalScroll.Visible, Is.False, "Page overflow " + width);
            paired |= cards[0].Top == cards[1].Top; stacked |= cards[0].Top < cards[1].Top;
            var panels = cards.Cast<Control>().Append(actions).ToArray();
            foreach (var panel in panels)
            { Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width)); }
            for (int i = 0; i < panels.Length; i++) for (int j = i + 1; j < panels.Length; j++) Assert.That(panels[i].Bounds.IntersectsWith(panels[j].Bounds), Is.False);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or CheckBox or Label))
                Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name + " at " + width);
            foreach (var button in actions.Controls.OfType<Button>())
                Assert.That(TextRenderer.MeasureText(button.Text, button.Font).Width + form.LogicalToDeviceUnits(12), Is.LessThanOrEqualTo(button.Width), button.Text);
            foreach (var choice in new[] { classic, dark })
                Assert.That(TextRenderer.MeasureText(choice.Text, choice.Font).Width + form.LogicalToDeviceUnits(28), Is.LessThanOrEqualTo(choice.Width));
            var firstCaption = cards[0].Controls.OfType<Label>().First();
            var firstField = cards[0].Controls.OfType<TextBox>().Single();
            inline |= firstField.Top - firstCaption.Top < form.LogicalToDeviceUnits(10);
            labelledAbove |= firstField.Top - firstCaption.Top > form.LogicalToDeviceUnits(20);
            Assert.That(originals.All(p => p.Key.Text == p.Value), Is.True);
            Assert.That(enabled.All(p => p.Key.Enabled == p.Value), Is.True);
            Assert.That(classic.Checked, Is.EqualTo(theme == 0)); Assert.That(dark.Checked, Is.True);
            Assert.That(status.Text, Is.EqualTo(Status(statusCase)));
            Assert.That(TextRenderer.MeasureText(status.Text, status.Font, new Size(status.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(status.Height));
            Assert.That(AppTheme.Descendants(form), Is.EquivalentTo(originalControls));
        }
        Assert.That(paired && stacked && inline && labelledAbove, Is.True);
        form.Size = form.LogicalToDeviceUnits(new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        foreach (var target in new[] { ("settingsShowBackups", cards[1]), ("settingsShowAppearance", cards[2]), ("settingsShowStatus", cards[3]) })
        {
            AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == target.Item1).PerformClick(); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.IntersectsWith(target.Item2.Bounds), Is.True);
        }
        foreach (var control in cards.SelectMany(c => c.Controls.Cast<Control>()).Concat(actions.Controls.Cast<Control>()).Where(c => c is TextBox or Button or CheckBox))
        {
            workspace.ScrollControlIntoView(control); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))), Is.True, control.Name);
        }
        AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "settingsShowConnection").PerformClick(); Application.DoEvents();
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        // Status updates must grow AND shrink the same label/card without resetting fields.
        status.Text = Status(2); form.PerformLayout(); Application.DoEvents(); int tall = cards[3].Height;
        status.Text = Status(1); form.PerformLayout(); Application.DoEvents(); Assert.That(cards[3].Height, Is.LessThan(tall));
        form.Size = new Size(1920, 1080); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.VerticalScroll.Visible || workspace.HorizontalScroll.Visible, Is.False);
        Assert.That(originals.All(p => p.Key.Text == p.Value) && enabled.All(p => p.Key.Enabled == p.Value), Is.True);
        form.Size = new Size(1096, 804); form.PerformLayout(); Application.DoEvents();
        status.Text = "Hazır"; int sameCardHeight = cards[3].Height;
        status.Text = "Yerel yedek doğrulandı.\nAynalama bekliyor.\nYerel yedek korunuyor.\nSonraki kontrolde yeniden denenecek.";
        Application.DoEvents();
        Assert.That(cards[3].Height, Is.EqualTo(sameCardHeight), "The neighbouring appearance card sets the row height.");
        Assert.That(TextRenderer.MeasureText(status.Text, status.Font, new Size(status.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(status.Height));
        // Navigation alone is invoked. No save, file dialog, backup, retry, restore or database operation.
    }

    [TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void NonAdministratorsStillCannotOpenSettings(UserRole role)
    {
        int settingsCreated = 0;
        using var context = new AccountingApplicationContext(page =>
        {
            if (page == AppPage.Settings) { settingsCreated++; return new SettingsForm { PreviewOnly = true, Opacity = 0, ShowInTaskbar = false }; }
            return page == AppPage.Login ? new Giris(() => Task.CompletedTask, (_, _) => Task.FromResult(false)) { Opacity = 0, ShowInTaskbar = false } : new SessionForm { PreviewOnly = true, Opacity = 0, ShowInTaskbar = false };
        });
        Assert.That(context.StartSession("preview-user", role), Is.True);
        Assert.That(context.Navigate(AppPage.Settings), Is.False); Assert.That(settingsCreated, Is.Zero);
    }

    internal static string Status(int index) => index switch
    {
        0 => "",
        1 => "Yerel yedek doğrulandı. Aynalama tamamlandı. Son kontrol: 27.10.2026 12:00.",
        _ => "Yerel yedek doğrulandı: 27.10.2026 12:00.\nAna dosya: C:\\Örnek yedekler\\AccountingApp-20261027-120000.bak\nAynalama bekliyor. Hedef klasöre erişilemiyor. Yerel yedek korunuyor; sonraki saatlik kontrolde veya Aynalamayı yeniden dene düğmesiyle tekrar denenecek.\nHedef: E:\\Örnek harici disk\\Muhasebe yedekleri\\AccountingApp-20261027-120000.bak\nBu örnek durum metni, uzun klasör adlarında ve birden fazla durum satırında açıklamanın tamamının okunabilmesini denetler."
    };
}
