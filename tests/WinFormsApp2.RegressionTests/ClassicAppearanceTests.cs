using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class ClassicAppearanceTests
{
    [Test]
    public void SummaryDoesNotMultiplyCountsOrBalancesWhenLeaveRowsRepeat()
    {
        using var form = new Summary { Session = new("preview-user", UserRole.Admin), PreviewOnly = true, PersistGridLayout = false, ShowInTaskbar = false, Opacity = 0 };
        form.Show();
        var table = new System.Data.DataTable(); table.Columns.Add("TC", typeof(long));
        foreach (string name in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) table.Columns.Add(name);
        table.Columns.Add("PARA", typeof(decimal)); table.Columns.Add("IZINBASLANGIC", typeof(DateTime)); table.Columns.Add("IZINBITIS", typeof(DateTime)); table.Columns.Add("IzinSuresi", typeof(int));
        table.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500m, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(1), 7);
        table.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500m, DateTime.Today.AddDays(5), DateTime.Today.AddDays(7), 3);
        table.Rows.Add(22222222222L, "Ece", "Örnek", "TR000000000000000000000001", "Joker", 2500m, DBNull.Value, DBNull.Value, DBNull.Value);
        table.AcceptChanges(); form.DisplaySummary(table);
        string Value(string name) => AppTheme.Descendants(form).OfType<Label>().Single(l => l.Name == name).Text;
        Assert.That(Value("personelLbl"), Is.EqualTo("1")); Assert.That(Value("jokerLabel"), Is.EqualTo("1"));
        Assert.That(Value("toplamLbl"), Is.EqualTo("2")); Assert.That(Value("bugunLbl"), Is.EqualTo("1"));
        Assert.That(Value("TotalBalance"), Is.EqualTo($"{4000m:N2} TL")); Assert.That(Value("AverageBalance"), Is.EqualTo($"{2000m:N2} TL"));
        Assert.That(AppTheme.Descendants(form).OfType<DataGridView>().Single().Rows.Count, Is.EqualTo(2));
    }
    private static void Render(Form form, string name)
    {
        form.Opacity = 0; form.ShowInTaskbar = false; form.Show();
        AppTheme.Apply(form, classicOverride: true); form.PerformLayout();
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "WinFormsApp2.csproj"))) root = Path.GetDirectoryName(root)!;
        string folder = Path.Combine(root, "artifacts", "analysis", "ui-preview"); Directory.CreateDirectory(folder);
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, name + "-classic.png"));
    }
    [Test]
    public void LoginLayoutMasksPasswordAndKeepsLongFailureNoticeInsideWindow()
    {
        using var login = new Giris(() => Task.CompletedTask, (_, _) => Task.FromResult(false));
        Render(login, "Giris");
        foreach (var field in AppTheme.Descendants(login).OfType<TextBox>())
        {
            Assert.That(field.Width, Is.GreaterThan(300));
            Assert.That(field.Parent!.ClientRectangle.Contains(field.Bounds), Is.True, field.Name);
        }
        var password = AppTheme.Descendants(login).OfType<TextBox>().Single(t => t.Name == "password_txt");
        Assert.That(password.UseSystemPasswordChar, Is.True); Assert.That(password.MaxLength, Is.EqualTo(1024));
        var status = AppTheme.Descendants(login).OfType<Label>().Single(l => l.Name == "statusLabel");
        var measured = TextRenderer.MeasureText(LoginAttemptPolicy.FailureMessage, status.Font, new Size(status.Width, 0), TextFormatFlags.WordBreak);
        Assert.That(measured.Height + status.Padding.Vertical, Is.LessThanOrEqualTo(status.Height));
    }
    [TestCase(UserRole.Admin), TestCase(UserRole.PersonnelManager), TestCase(UserRole.Viewer)]
    public void ClassicDashboardHasKeyboardAccessAndRoleAwareTreeNavigation(UserRole role)
    {
        using var context = new AccountingApplicationContext(page => page switch
        {
            AppPage.Login => new Giris(() => Task.CompletedTask, (_, _) => Task.FromResult(false)) { Opacity = 0, ShowInTaskbar = false },
            AppPage.Dashboard => new Dashboard { Opacity = 0, ShowInTaskbar = false, PreviewOnly = true },
            _ => new SessionForm { Opacity = 0, ShowInTaskbar = false, PreviewOnly = true }
        });
        Assert.That(context.StartSession("preview-user", role), Is.True);
        var dashboard = (Dashboard)context.MainForm!; Render(dashboard, "Dashboard-" + role);
        var buttons = AppTheme.Descendants(dashboard).OfType<Button>().ToArray();
        foreach (var button in buttons)
            Assert.That(button.TabStop && button.Parent!.ClientRectangle.Contains(button.Bounds), Is.True, button.Text + ": " + button.Bounds + " / " + button.Parent!.ClientRectangle);
        var tree = AppTheme.Descendants(dashboard).OfType<TreeView>().Single();
        var pages = tree.Nodes.Cast<TreeNode>().SelectMany(n => n.Nodes.Cast<TreeNode>()).Select(n => n.Tag).ToArray();
        Assert.That(pages.Contains(AppPage.Admin), Is.EqualTo(role == UserRole.Admin));
        Assert.That(pages.Contains(AppPage.Settings), Is.EqualTo(role == UserRole.Admin));
        var menu = dashboard.MainMenuStrip!;
        Assert.That(menu.Items.Cast<ToolStripItem>().All(item => item.Bounds.Right <= menu.Width), Is.True);
        dashboard.ClientSize = new Size(1600, 900); dashboard.PerformLayout();
        Assert.That(buttons.All(b => b.Height <= 48 * dashboard.DeviceDpi / 96f), Is.True, "Büyütülmüş pencerede işlem düğmeleri gereksiz yere uzamamalı.");
        Assert.That(buttons.All(b => b.Parent!.ClientRectangle.Contains(b.Bounds)), Is.True);
        if (role == UserRole.Admin) Render(dashboard, "Dashboard-wide");
        tree.SelectedNode = tree.Nodes[0].Nodes[0];
        Application.DoEvents();
        Assert.That(context.CurrentPage, Is.EqualTo(AppPage.General)); Assert.That(dashboard.IsDisposed, Is.True);
    }
}
