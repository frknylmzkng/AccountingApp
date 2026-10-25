using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, NonParallelizable, Category("Passwords")]
public class PasswordFeatureTests
{
    [Test, Apartment(ApartmentState.STA)]
    public void EscapeCancelsPasswordResetAndClearsAdministratorSecret()
    {
        using var reset = new PasswordResetDialog(new("test-admin", UserRole.Admin), new("test-target", new byte[8], Guid.NewGuid())) { PreviewOnly = true, Opacity = 0, ShowInTaskbar = false };
        reset.Show(); var secret = AppTheme.Descendants(reset).OfType<TextBox>().Single(); secret.Text = "Only-an-example-secret";
        object[] arguments = [new Message(), Keys.Escape];
        var handled = typeof(PasswordResetDialog).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(reset, arguments);
        Assert.That(handled, Is.EqualTo(true)); Assert.That(reset.Visible, Is.False);
        Assert.That(reset.DialogResult, Is.EqualTo(DialogResult.Cancel)); Assert.That(reset.Result, Is.Null); Assert.That(secret.Text, Is.Empty);
    }
    [TestCase(0)]
    [TestCase(14)]
    [TestCase(1025)]
    public void NewPasswordLengthLimitsAreEnforced(int length) => Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string('a', length), new string('a', length)));
    [TestCase(15)]
    [TestCase(1024)]
    public void PasswordLengthBoundariesAndPassphrasesAreAllowed(int length) => Assert.DoesNotThrow(() => PasswordPolicy.Validate(new string('a', length), new string('a', length)));
    [Test]
    public void ConfirmationIsExactAndWhitespaceIsNotTrimmed()
    {
        const string password = " Parola cümlesi 2026! ";
        Assert.DoesNotThrow(() => PasswordPolicy.Validate(password, password));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(password, password.Trim()));
        Assert.Throws<ArgumentException>(() => PasswordPolicy.Validate(new string(' ', 20), new string(' ', 20)));
    }
    [Test]
    public void TemporaryPasswordsUseIndependentCryptoRandomValuesAndResultDoesNotPrintSecret()
    {
        var values = Enumerable.Range(0, 20).Select(_ => PasswordPolicy.Temporary()).ToArray();
        Assert.That(values.Distinct().Count(), Is.EqualTo(20));
        foreach (string value in values) Assert.That(Convert.FromBase64String(value), Has.Length.EqualTo(24));
        var result = new PasswordResetResult("test", values[0], DateTime.UtcNow.AddMinutes(30));
        Assert.That(result.ToString(), Does.Not.Contain(values[0]));
    }
    [Test, Apartment(ApartmentState.STA)]
    public void PasswordScreensMaskEverySecretAndViewerCanUseOwnChangeScreen()
    {
        using var change = new PasswordChangeForm { Session = new("viewer", UserRole.Viewer), PreviewOnly = true };
        using var reset = new PasswordResetDialog(new("admin", UserRole.Admin), new("target", new byte[8], Guid.NewGuid()));
        const string temporary = "Only-a-test-secret-never-a-user-password";
        using var display = new TemporaryPasswordDialog(new("target", temporary, DateTime.UtcNow.AddMinutes(30)));
        foreach (var form in new Form[] { change, reset, display })
        {
            form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show();
            foreach (var field in AppTheme.Descendants(form).OfType<TextBox>())
            {
                Assert.That(field.UseSystemPasswordChar, Is.True); Assert.That(field.MaxLength, Is.EqualTo(PasswordHasher.MaxPasswordLength));
                Assert.That(field.Parent!.ClientRectangle.Contains(field.Bounds), Is.True, field.Name);
            }
            foreach (var button in AppTheme.Descendants(form).OfType<Button>())
                Assert.That(button.Parent!.ClientRectangle.Contains(button.Bounds), Is.True, button.Text);
            foreach (var control in AppTheme.Descendants(form).Where(c => c is TextBox or Button or CheckBox))
            {
                var bounds = control.RectangleToScreen(control.ClientRectangle);
                for (var parent = control.Parent; parent != null; parent = parent.Parent)
                    Assert.That(parent.RectangleToScreen(parent.ClientRectangle).Contains(bounds), Is.True, control.Name + " is clipped by " + parent.GetType().Name);
            }
            string evidence = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../../artifacts/analysis/password-security-ui"));
            Directory.CreateDirectory(evidence);
            form.Refresh(); Application.DoEvents();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(evidence, form.GetType().Name + ".png"));
        }
        Assert.That(AppTheme.Descendants(change).OfType<Button>().Single(b => b.Text == "Parolayı değiştir").Enabled, Is.True);
        var secret = AppTheme.Descendants(display).OfType<TextBox>().Single(); Assert.That(secret.ReadOnly, Is.True);
        display.Close(); Assert.That(secret.Text, Is.Empty);
    }
}
