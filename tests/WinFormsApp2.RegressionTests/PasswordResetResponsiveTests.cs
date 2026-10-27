using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class PasswordResetResponsiveTests
{
    internal static string LongUsername => new string('u', 120) + "&hedef";

    [TestCase(0, false), TestCase(0, true), TestCase(1, false), TestCase(1, true), TestCase(2, false), TestCase(2, true)]
    public void ResetReflowPreservesTargetMaskedInputAndBusyLocks(int theme, bool longName)
    {
        var user = new UserSession("preview-admin", UserRole.Admin) { SecurityVersion = [0, 0, 0, 0, 0, 0, 0, 1], SecurityEpoch = Guid.NewGuid() };
        var target = new AccountTarget(longName ? LongUsername : "ornek-hedef", [0, 0, 0, 0, 0, 0, 0, 2], Guid.NewGuid());
        using var form = new PasswordResetDialog(user, target) { PreviewOnly = true, PersistGridLayout = false, Opacity = 0, ShowInTaskbar = false };
        form.Show(); AppTheme.Apply(form, classicOverride: theme == 0, darkOverride: theme == 2);
        var workspace = AppTheme.Descendants(form).OfType<SecretDialogWorkspace>().Single();
        var info = workspace.Controls.OfType<SecretDialogInfoCard>().Single(); var entry = workspace.Controls.OfType<SecretDialogEntryCard>().Single();
        var account = info.Controls.OfType<Label>().Single(c => c.Name.EndsWith("Account"));
        var field = entry.Controls.OfType<TextBox>().Single();
        field.Text = " " + new string('x', PasswordHasher.MaxPasswordLength - 2) + " "; field.SelectionStart = 4; field.SelectionLength = 3;
        string input = field.Text; var targetVersion = target.Version.ToArray(); Guid targetEpoch = target.Epoch;
        var submit = (Button)form.AcceptButton!; var cancel = (Button)form.CancelButton!;
        var controls = AppTheme.Descendants(form).ToArray();
        var widths = Widths(); bool paired = false, stacked = false;
        for (int phase = 0; phase < 2; phase++)
        {
            TaskCompletionSource? gate = null; Task<bool>? operation = null;
            if (phase == 1)
            {
                gate = new();
                operation = (Task<bool>)typeof(SessionForm).GetMethod("RunOperationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, [new Func<CancellationToken, Task>(_ => gate.Task), null])!;
                Assert.That(form.IsOperationRunning, Is.True);
            }
            var enabled = AppTheme.Descendants(form).Where(c => c is Button or TextBox).ToDictionary(c => c, c => c.Enabled);
            foreach (int width in widths.Concat(widths.Reverse()))
            {
                form.Size = form.LogicalToDeviceUnits(new Size(width, 740)); form.PerformLayout(); Application.DoEvents();
                AssertLayout(form, workspace, info, entry); paired |= info.Top == entry.Top; stacked |= info.Bottom < entry.Top;
                Assert.That(account.Text, Is.EqualTo("Hedef hesap: " + target.Username)); Assert.That(account.UseMnemonic, Is.False);
                Assert.That(field.Text, Is.EqualTo(input)); Assert.That((field.SelectionStart, field.SelectionLength), Is.EqualTo((4, 3)));
                Assert.That(field.UseSystemPasswordChar && field.MaxLength == PasswordHasher.MaxPasswordLength && !field.ReadOnly, Is.True);
                Assert.That(enabled.All(p => p.Key.Enabled == p.Value), Is.True); Assert.That(submit.Enabled && cancel.Enabled, Is.EqualTo(phase == 0));
                Assert.That(AppTheme.Descendants(form), Is.EquivalentTo(controls)); Assert.That(form.Session, Is.SameAs(user));
                Assert.That(target.Version, Is.EqualTo(targetVersion)); Assert.That(target.Epoch, Is.EqualTo(targetEpoch));
                Assert.That(form.Result, Is.Null); Assert.That(form.SessionInvalidated, Is.False);
            }
            if (gate != null)
            {
                gate.SetResult(); Application.DoEvents(); Assert.That(operation!.IsCompletedSuccessfully && operation.Result, Is.True);
                Assert.That(!form.IsOperationRunning && submit.Enabled && cancel.Enabled && field.Enabled, Is.True);
            }
        }
        Assert.That(paired && stacked, Is.True);
        form.Size = form.LogicalToDeviceUnits(new Size(560, 420)); form.PerformLayout(); Application.DoEvents();
        field.Focus(); Application.DoEvents(); AssertFieldVisible(workspace, field); AssertLayout(form, workspace, info, entry);
        Assert.That(form.SelectNextControl(field, true, true, true, true), Is.True); Assert.That(cancel.Focused, Is.True);
        Assert.That(form.SelectNextControl(cancel, true, true, true, true), Is.True); Assert.That(submit.Focused, Is.True);
        Assert.That(form.FormBorderStyle == FormBorderStyle.Sizable && form.MaximizeBox && !form.MinimizeBox, Is.True);
        Assert.That(form.MainMenuStrip, Is.Null);
        object[] args = [new Message(), Keys.Escape];
        Assert.That(typeof(PasswordResetDialog).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, args), Is.EqualTo(true));
        Assert.That(form.DialogResult, Is.EqualTo(DialogResult.Cancel)); Assert.That(field.Text, Is.Empty); Assert.That(form.Result, Is.Null);
        // The pending task exercises only the UI lock. Reset/submit is never invoked.
    }

    [TestCase(0, false), TestCase(0, true), TestCase(1, false), TestCase(1, true), TestCase(2, false), TestCase(2, true)]
    public void TemporaryResultReflowPreservesVisibilityReadOnlySecretAndExpiry(int theme, bool visible)
    {
        const string sample = "Only-an-example-temporary-password";
        var result = new PasswordResetResult(LongUsername, sample, new DateTime(2026, 10, 27, 18, 0, 0, DateTimeKind.Utc));
        using var form = new TemporaryPasswordDialog(result) { Opacity = 0, ShowInTaskbar = false };
        form.Show(); AppTheme.Apply(form, classicOverride: theme == 0, darkOverride: theme == 2);
        var workspace = AppTheme.Descendants(form).OfType<SecretDialogWorkspace>().Single();
        var info = workspace.Controls.OfType<SecretDialogInfoCard>().Single(); var entry = workspace.Controls.OfType<SecretDialogEntryCard>().Single();
        var account = info.Controls.OfType<Label>().Single(c => c.Name.EndsWith("Account"));
        var detail = info.Controls.OfType<Label>().Single(c => c.Name.EndsWith("Details")); string description = detail.Text;
        var field = entry.Controls.OfType<TextBox>().Single(); var show = entry.Controls.OfType<CheckBox>().Single(); show.Checked = visible;
        field.SelectionStart = 4; field.SelectionLength = 3; var controls = AppTheme.Descendants(form).ToArray();
        var widths = Widths(); bool paired = false, stacked = false;
        foreach (int width in widths.Concat(widths.Reverse()))
        {
            form.Size = form.LogicalToDeviceUnits(new Size(width, 740)); form.PerformLayout(); Application.DoEvents();
            AssertLayout(form, workspace, info, entry); paired |= info.Top == entry.Top; stacked |= info.Bottom < entry.Top;
            Assert.That(account.Text, Is.EqualTo("Hesap: " + LongUsername)); Assert.That(account.UseMnemonic, Is.False);
            Assert.That(detail.Text, Is.EqualTo(description)); Assert.That(description, Does.Contain(result.ExpiresUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss")));
            Assert.That(field.Text, Is.EqualTo(sample)); Assert.That((field.SelectionStart, field.SelectionLength), Is.EqualTo((4, 3)));
            Assert.That(field.ReadOnly && field.MaxLength == PasswordHasher.MaxPasswordLength, Is.True);
            Assert.That(show.Checked, Is.EqualTo(visible)); Assert.That(field.UseSystemPasswordChar, Is.EqualTo(!visible));
            Assert.That(AppTheme.Descendants(form), Is.EquivalentTo(controls));
        }
        Assert.That(paired && stacked, Is.True);
        form.Size = form.LogicalToDeviceUnits(new Size(560, 420)); form.PerformLayout(); Application.DoEvents();
        field.Focus(); Application.DoEvents(); AssertFieldVisible(workspace, field);
        workspace.ScrollControlIntoView(show); Application.DoEvents(); AssertFieldVisible(workspace, show);
        AssertLayout(form, workspace, info, entry);
        Assert.That(form.SelectNextControl(field, true, true, true, true), Is.True); Assert.That(show.Focused, Is.True);
        Assert.That(form.SelectNextControl(show, true, true, true, true), Is.True); Assert.That(((Button)form.AcceptButton!).Focused, Is.True);
        Assert.That(form.AcceptButton, Is.SameAs(form.CancelButton)); Assert.That(form.FormBorderStyle == FormBorderStyle.Sizable && form.MaximizeBox && !form.MinimizeBox, Is.True);
        show.Checked = !visible; Assert.That(field.UseSystemPasswordChar, Is.EqualTo(visible)); Assert.That(field.Text, Is.EqualTo(sample));
        ((Button)form.AcceptButton!).PerformClick(); Assert.That(field.Text, Is.Empty); Assert.That(form.Visible, Is.False);
        // This result is synthetic. No reset, clipboard write, transmission or database action runs.
    }

    private static int[] Widths() => Enumerable.Range(0, 24).Select(i => 560 + i * 60).Concat(new[] { 940, 1160 }).ToArray();
    private static void AssertFieldVisible(SecretDialogWorkspace workspace, Control field) =>
        Assert.That(workspace.ClientRectangle.Contains(workspace.RectangleToClient(field.RectangleToScreen(field.ClientRectangle))), Is.True, field.Name);
    private static void AssertLayout(Form form, SecretDialogWorkspace workspace, SecretDialogInfoCard info, SecretDialogEntryCard entry)
    {
        Assert.That(workspace.HorizontalScroll.Visible, Is.False);
        Assert.That(info.Bounds.IntersectsWith(entry.Bounds), Is.False);
        foreach (var panel in new Control[] { info, entry })
        { Assert.That(panel.Left, Is.GreaterThanOrEqualTo(0)); Assert.That(panel.Right, Is.LessThanOrEqualTo(workspace.ClientSize.Width)); }
        foreach (var control in AppTheme.Descendants(form).Where(c => c is Button or TextBox or Label or CheckBox))
            Assert.That(control.Parent!.ClientRectangle.Contains(control.Bounds), Is.True, control.Name);
        foreach (var label in info.Controls.OfType<Label>().Concat(entry.Controls.OfType<Label>()))
            Assert.That(TextRenderer.MeasureText(label.Text, label.Font, new Size(label.Width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height, Is.LessThanOrEqualTo(label.Height), label.Name);
        foreach (var button in AppTheme.Descendants(form).OfType<Button>())
        {
            var bounds = button.RectangleToScreen(button.ClientRectangle);
            for (var parent = button.Parent; parent != null; parent = parent.Parent)
                Assert.That(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds), Is.True, "Action always visible");
        }
    }
}
