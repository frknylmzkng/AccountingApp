using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class PasswordChangeResponsiveTests
{
    [TestCase(UserRole.Admin, false), TestCase(UserRole.Admin, true)]
    [TestCase(UserRole.PersonnelManager, false), TestCase(UserRole.PersonnelManager, true)]
    [TestCase(UserRole.Viewer, false), TestCase(UserRole.Viewer, true)]
    public void ReflowPreservesMaskedSecretsSessionAndBusyLocks(UserRole role, bool temporary)
    {
        var session = new UserSession("preview-user", role) { MustChangePassword = temporary, SecurityVersion = [0, 0, 0, 0, 0, 0, 0, 1], SecurityEpoch = Guid.NewGuid() };
        using var form = new PasswordChangeForm { Session = session, PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        ResponsiveTestWindow.Show(form, new Size(840, 560)); AppTheme.Apply(form, classicOverride: true); var defaultSize = form.Size;
        var workspace = AppTheme.Descendants(form).OfType<PasswordChangeWorkspace>().Single();
        var editor = workspace.Controls.OfType<PasswordChangeEditor>().Single(); var rules = workspace.Controls.OfType<PasswordChangeRules>().Single();
        var fields = editor.Controls.OfType<TextBox>().OrderBy(c => c.TabIndex).ToArray();
        var save = (Button)form.AcceptButton!; var actions = AppTheme.Descendants(form).OfType<PasswordChangeActions>().Single();
        var notice = editor.Controls.OfType<Label>().Single(c => c.Name == "passwordChangeNotice");
        var policy = rules.Controls.OfType<Label>().Single();
        Assert.That(notice.Text, Does.StartWith(temporary ? "Geçici parolayla giriş yaptınız." : "Parola değişince bütün açık oturumlarınız sona erecek."));
        fields[0].Text = " Only an example current password "; fields[1].Text = fields[2].Text = new string('x', PasswordHasher.MaxPasswordLength - 2) + "  ";
        foreach (var field in fields) { field.SelectionStart = 4; field.SelectionLength = 3; }
        var expected = fields.ToDictionary(c => c, c => (c.Text, c.SelectionStart, c.SelectionLength));
        string noticeText = notice.Text;
        var allControls = AppTheme.Descendants(form).ToArray();
        var widths = Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 660, 1040 }).ToArray();
        bool paired = false, stacked = false, inline = false, above = false;
        for (int phase = 0; phase < 2; phase++)
        {
            TaskCompletionSource? gate = null; Task<bool>? operation = null;
            if (phase == 1)
            {
                gate = new();
                // Exercise the real UI busy lock using a local pending task,
                // without invoking the password-change service or save handler.
                operation = (Task<bool>)typeof(SessionForm).GetMethod("RunOperationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, [new Func<CancellationToken, Task>(_ => gate.Task), null])!;
                Assert.That(form.IsOperationRunning, Is.True);
            }
            var enabled = AppTheme.Descendants(form).Where(c => c is Button or TextBox or ToolStrip).ToDictionary(c => c, c => c.Enabled);
            foreach (int width in widths.Concat(widths.Reverse()))
            {
                ResponsiveTestWindow.Resize(form, new Size(width, 740)); form.PerformLayout(); Application.DoEvents();
                Assert.That(workspace.HorizontalScroll.Visible, Is.False, "Page overflow " + width);
                paired |= editor.Top == rules.Top; stacked |= editor.Bottom < rules.Top;
                inline |= fields[0].Left > editor.LogicalToDeviceUnits(100); above |= fields[0].Left < editor.LogicalToDeviceUnits(20);
                Assert.That(editor.Bounds.IntersectsWith(rules.Bounds), Is.False);
                foreach (var panel in new Control[] { editor, rules })
                { Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width)); }
                foreach (var control in AppTheme.Descendants(form).Where(c => c is Button or TextBox or Label))
                    Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name + " at " + width);
                foreach (var label in editor.Controls.OfType<Label>().Concat(rules.Controls.OfType<Label>()).Concat(actions.Controls.OfType<Label>()))
                    Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(label.Height), label.Name);
                foreach (var field in fields)
                {
                    Assert.That(field.UseSystemPasswordChar && field.MaxLength == PasswordHasher.MaxPasswordLength, Is.True);
                    Assert.That(field.Text, Is.EqualTo(expected[field].Text));
                    Assert.That((field.SelectionStart, field.SelectionLength), Is.EqualTo((expected[field].SelectionStart, expected[field].SelectionLength)));
                }
                Assert.That(notice.Text, Is.EqualTo(noticeText)); Assert.That(policy.Text, Is.EqualTo(PasswordPolicy.Description));
                Assert.That(enabled.All(p => p.Key.Enabled == p.Value), Is.True);
                Assert.That(save.Enabled, Is.EqualTo(phase == 0), "Own-password change remains available for viewers and restricted sessions.");
                Assert.That(form.Session, Is.SameAs(session)); Assert.That(form.Session!.SecurityVersion, Is.EqualTo(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 }));
                Assert.That(AppTheme.Descendants(form), Is.EquivalentTo(allControls));
                var saveBounds = save.RectangleToScreen(save.ClientRectangle);
                for (var parent = save.Parent; parent != null; parent = parent.Parent)
                    Assert.That(parent.RectangleToScreen(parent.ClientRectangle).Contains(saveBounds), Is.True, "Save stays visible");
            }
            if (gate != null)
            {
                gate.SetResult(); Application.DoEvents();
                Assert.That(operation!.IsCompletedSuccessfully, Is.True); Assert.That(operation!.Result, Is.True);
                Assert.That(form.IsOperationRunning, Is.False); Assert.That(save.Enabled && fields.All(c => c.Enabled), Is.True);
            }
        }
        Assert.That(paired && stacked && inline && above, Is.True);
        ResponsiveTestWindow.Resize(form, new Size(560, 480)); form.PerformLayout(); Application.DoEvents();
        AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "passwordChangeShowRules").PerformClick(); Application.DoEvents();
        Assert.That(workspace.ClientRectangle.Contains(rules.Bounds), Is.True);
        AppTheme.Descendants(form).OfType<Button>().Single(c => c.Name == "passwordChangeShowFields").PerformClick(); Application.DoEvents();
        Assert.That(workspace.AutoScrollPosition, Is.EqualTo(Point.Empty));
        foreach (var field in fields)
        {
            field.Focus(); Application.DoEvents();
            Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(field.RectangleToScreen(field.ClientRectangle))), Is.True, field.Name);
        }
        Assert.That(form.SelectNextControl(fields[0], true, true, true, true), Is.True); Assert.That(fields[1].Focused, Is.True);
        Assert.That(form.SelectNextControl(fields[1], true, true, true, true), Is.True); Assert.That(fields[2].Focused, Is.True);
        Assert.That(form.SelectNextControl(fields[2], true, true, true, true), Is.True); Assert.That(save.Focused, Is.True);
        ResponsiveTestWindow.ResizeDevice(form, defaultSize); form.PerformLayout(); Application.DoEvents();
        Assert.That(workspace.HorizontalScroll.Visible || workspace.VerticalScroll.Visible, Is.False);
        var menu = form.MainMenuStrip!;
        Assert.That(menu.Items.Count, Is.EqualTo(temporary ? 1 : 2));
        Assert.That(menu.Items.Cast<ToolStripItem>().Any(c => c.Text?.Contains("Ana ekran") == true), Is.EqualTo(!temporary));
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, darkOverride: dark, classicOverride: false); form.PerformLayout(); Application.DoEvents();
            foreach (var field in fields) Assert.That(field.UseSystemPasswordChar && field.Text == expected[field].Text, Is.True);
            foreach (var label in new[] { notice, policy })
                Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak).Height, Is.LessThanOrEqualTo(label.Height));
        }
        form.Close(); Assert.That(fields.All(c => c.Text.Length == 0), Is.True, "Closing must still clear every secret.");
        // No password submission, logout, account write or credential persistence is invoked.
    }
}
