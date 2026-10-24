using System.Data;
using System.Drawing;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, NonParallelizable]
public class GuidePureTests
{
    private string directory = "";
    [SetUp] public void Setup() { directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "GuideScratch_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TearDown] public void Cleanup() { Directory.Delete(directory, true); }
    [Test, Category("Calculations")]
    public void WeightedAverageUsesDecimalWeightsAndEmptyIsZero()
    {
        Assert.That(Calculations.WeightedAverage([(10m, 2m), (20m, 3m)]), Is.EqualTo(16m));
        Assert.That(Calculations.WeightedAverage([]), Is.Zero);
        Assert.Throws<ArgumentException>(() => Calculations.WeightedAverage([(1m, -1m)]));
        Assert.That(Calculations.LeaveDays(new(2024, 2, 28), new(2024, 3, 1)), Is.EqualTo(3));
        Assert.That(Calculations.LeaveDays(DateTime.Today, DateTime.Today), Is.EqualTo(1));
        Assert.Throws<ArgumentException>(() => Calculations.LeaveDays(DateTime.Today, DateTime.Today.AddDays(-1)));
    }
    [TestCase("=HYPERLINK(\"evil\")")]
    [TestCase("  +1+1")]
    [TestCase("@SUM(A1)")]
    [TestCase("-1+cmd")]
    [Category("Exports")]
    public void CsvNeutralizesTextFormulas(string text) => Assert.That(ReportExport.CsvCell(text), Does.StartWith("\"'"));
    [Test, Category("Exports")]
    public void CsvRoundTripsTurkishQuotesSemicolonAndNewlineWithBom()
    {
        var data = new DataTable(); data.Columns.Add("Adı"); data.Columns.Add("Tutar", typeof(decimal)); data.Rows.Add("Çağrı; \"Öztürk\"\r\nİzin", -15.50m);
        string path = Path.Combine(directory, "rapor.csv"); ReportExport.Csv(data, path);
        Assert.That(File.ReadAllBytes(path).Take(3), Is.EqualTo(new byte[] { 239, 187, 191 }));
        Assert.That(File.ReadAllText(path), Does.Contain("\"Çağrı; \"\"Öztürk\"\"\r\nİzin\";\"-15,50\""));
    }
    [Test, Category("Exports")]
    public void ExcelPreservesTextAndHasCurrencySumAndStyledHeader()
    {
        var data = new DataTable(); data.Columns.Add("Adı"); data.Columns.Add("PARA", typeof(decimal)); data.Rows.Add("=1+1", 1.25m); data.Rows.Add("Özge", 2.75m);
        string path = Path.Combine(directory, "rapor.xlsx"); ReportExport.Excel(data, path);
        using var book = new XLWorkbook(path); var sheet = book.Worksheet(1);
        Assert.That(sheet.Cell("A2").GetString(), Is.EqualTo("=1+1")); Assert.That(sheet.Cell("A2").HasFormula, Is.False);
        Assert.That(sheet.Cell("B4").FormulaA1, Is.EqualTo("SUM(B2:B3)")); Assert.That(sheet.Cell("B4").GetValue<decimal>(), Is.EqualTo(4m));
        Assert.That(sheet.Cell("B2").Style.NumberFormat.Format, Does.Contain("TL")); Assert.That(sheet.Cell("A1").Style.Font.Bold, Is.True);
    }
    [Test, Category("Drafts")]
    public void DraftIsEncryptedAndBoundToItsScope()
    {
        string path = Path.Combine(directory, "person.draft"); const string tc = "11111111111";
        DraftStore.Save(path, "user|db|form", new Dictionary<string, string> { ["TC"] = tc });
        Assert.That(Encoding.UTF8.GetString(File.ReadAllBytes(path)), Does.Not.Contain(tc));
        Assert.That(DraftStore.Load<Dictionary<string, string>>(path, "user|db|form")!["TC"], Is.EqualTo(tc));
        Assert.Throws<CryptographicException>(() => DraftStore.Load<Dictionary<string, string>>(path, "otheruser|db|form"));
    }
    [Test, Category("Backup")]
    public void RetentionOnlyRemovesOwnBackupsAndKeepsNewestFourteen()
    {
        string prefix = BackupService.Prefix("GuideTest");
        for (int i = 0; i < 18; i++) File.WriteAllText(Path.Combine(directory, prefix + i.ToString("D4") + ".bak"), "test");
        string unrelated = Path.Combine(directory, BackupService.Prefix("OtherDB") + "0000.bak"); File.WriteAllText(unrelated, "other");
        File.WriteAllText(Path.Combine(directory, "notes.txt"), "keep"); BackupService.Prune(directory, prefix, 14);
        Assert.That(Directory.GetFiles(directory, prefix + "*.bak"), Has.Length.EqualTo(14)); Assert.That(File.Exists(unrelated), Is.True);
        Assert.That(File.Exists(Path.Combine(directory, prefix + "0003.bak")), Is.False); Assert.That(File.Exists(Path.Combine(directory, prefix + "0017.bak")), Is.True);
        Assert.Throws<ArgumentException>(() => BackupService.Prune(directory, "*", 14));
    }
    [Test, Category("Profiles")]
    public void SqlExpressProfileUsesIntegratedSecurityAndNoAttach()
    {
        string path = Path.Combine(directory, "express.json"); File.WriteAllText(path, """{"Database":{"Server":".\\SQLEXPRESS","Name":"AccountingAppGuide","FilePath":""}}""");
        var builder = new SqlConnectionStringBuilder(DatabaseSettings.Load(path).CreateConnectionString());
        Assert.That(builder.AttachDBFilename, Is.Empty); Assert.That(builder.InitialCatalog, Is.EqualTo("AccountingAppGuide")); Assert.That(builder.IntegratedSecurity, Is.True); Assert.That(builder.TrustServerCertificate, Is.False);
    }
    [Test, Category("Profiles")]
    public void DataDirectoryResolvesBeforeOpeningDatabase()
    {
        string path = Path.Combine(directory, "local.json"); File.WriteAllText(path, """{"Database":{"Server":"(LocalDB)\\MSSQLLocalDB","FilePath":"|DataDirectory|\\people.mdf"}}""");
        Assert.That(DatabaseSettings.Load(path).FilePath, Is.EqualTo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "people.mdf")));
    }
    [Test, Category("Preferences")]
    public void PreferencesPersistThemeAndFallBackToIsolatedRegistryKey()
    {
        string key = @"Software\AccountingAppTests\" + Guid.NewGuid().ToString("N");
        try
        {
            new UserPreferences { DarkTheme = true, BackupFolder = "Türkçe klasör", MirrorFolder = "mirror" }.Save(directory, key);
            Assert.That(UserPreferences.Load(directory, key).DarkTheme, Is.True);
            File.WriteAllText(Path.Combine(directory, "preferences.json"), "{invalid");
            var restored = UserPreferences.Load(directory, key); Assert.That(restored.BackupFolder, Is.EqualTo("Türkçe klasör")); Assert.That(restored.MirrorFolder, Is.EqualTo("mirror"));
        }
        finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(key, false); }
    }
    [Test, Category("Appearance"), Apartment(ApartmentState.STA)]
    public void GridLayoutPersistsWidthOrderVisibilityWithoutPersonalData()
    {
        using var form = new Form(); using var grid = new DataGridView { Name = "People" }; form.Controls.Add(grid);
        grid.Columns.Add("TC", "TC"); grid.Columns.Add("AD", "AD"); grid.Columns.Add("PARA", "PARA");
        grid.Columns["AD"]!.Width = 215; grid.Columns["AD"]!.DisplayIndex = 0; grid.Columns["TC"]!.Visible = false;
        string path = Path.Combine(directory, "grid.json"); GridPreferences.Save(grid, path);
        grid.Columns["AD"]!.Width = 80; grid.Columns["TC"]!.Visible = true; GridPreferences.Restore(grid, path);
        Assert.That(grid.Columns["AD"]!.Width, Is.EqualTo(215)); Assert.That(grid.Columns["AD"]!.DisplayIndex, Is.Zero); Assert.That(grid.Columns["TC"]!.Visible, Is.False);
        File.WriteAllText(path, "{broken"); Assert.DoesNotThrow(() => GridPreferences.Restore(grid, path));
    }
    [Test, Category("Audit")]
    public void NLogNeverWritesExceptionMessages()
    {
        string sensitive = "private-password-" + Guid.NewGuid().ToString("N"); string id = AppLog.Failure(new InvalidOperationException(sensitive), "Test.Sanitized"); NLog.LogManager.Flush();
        using var stream = new FileStream(Path.Combine(UserPreferences.DirectoryPath, "logs", "application.log"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var reader = new StreamReader(stream); string log = reader.ReadToEnd();
        Assert.That(log, Does.Contain(id)); Assert.That(log, Does.Not.Contain(sensitive));
    }
    [TestCase(UserRole.Viewer, false, false)]
    [TestCase(UserRole.PersonnelManager, true, false)]
    [TestCase(UserRole.Admin, true, true)]
    [Category("Roles")]
    public void RolePolicy(UserRole role, bool edit, bool admin)
    {
        var user = new UserSession("test", role); Assert.That(user.CanEdit, Is.EqualTo(edit));
        if (edit) Assert.DoesNotThrow(user.DemandEdit); else Assert.Throws<UnauthorizedAccessException>(user.DemandEdit);
        if (admin) Assert.DoesNotThrow(user.DemandAdmin); else Assert.Throws<UnauthorizedAccessException>(user.DemandAdmin);
    }
    [Test, Category("Printing"), Apartment(ApartmentState.STA)]
    public void BothPaperSizesAndMultiPageRenderingWorkWithoutPrinting()
    {
        var report = new PrintReport("Ödeme kapanışı", Enumerable.Range(0, 80).Select(i => $"Personel {i}: 150,50 TL").ToArray(), "test-reference", 0);
        using var a4 = PaymentPrinting.Create(report, false); using var thermal = PaymentPrinting.Create(report, true);
        Assert.That(a4.DefaultPageSettings.PaperSize.Width, Is.EqualTo(827)); Assert.That(thermal.DefaultPageSettings.PaperSize.Width, Is.EqualTo(315));
        using var image = new Bitmap(827, 1169); using var g = Graphics.FromImage(image);
        int offset = 0, page = 0; bool more;
        do { int previous = offset; offset = PaymentPrinting.Draw(g, new Rectangle(30, 30, 260, 1050), report, offset, ++page, null, out more); Assert.That(offset, Is.GreaterThan(previous)); } while (more && page < 20);
        Assert.That(offset, Is.EqualTo(80)); Assert.That(page, Is.GreaterThan(1));
        Assert.That(PaymentPrinting.Mask("11111111111"), Is.EqualTo("*******1111"));
    }
}

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Appearance")]
public class GuideUiTests
{
    [TestCase(typeof(Genel))]
    [TestCase(typeof(Joker))]
    [TestCase(typeof(Admin))]
    [TestCase(typeof(Search))]
    [TestCase(typeof(Summary))]
    [TestCase(typeof(PaymentsForm))]
    [TestCase(typeof(SettingsForm))]
    [TestCase(typeof(LeavePlanForm))]
    public void ScreensRenderWithToolbarAndStatusWithoutDatabaseAccess(Type type)
    {
        using var form = (SessionForm)Activator.CreateInstance(type)!;
        form.Session = new("test-admin", UserRole.Admin); form.PersistGridLayout = false; form.PreviewOnly = true;
        form.ShowInTaskbar = false; form.Opacity = 0; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000); form.Show();
        var table = new DataTable(); table.Columns.Add("TC", typeof(long)); foreach (string col in new[] { "AD", "SOYAD", "IBAN", "GRUP" }) table.Columns.Add(col);
        table.Columns.Add("PARA", typeof(decimal)); table.Columns.Add("IZINBASLANGIC", typeof(DateTime)); table.Columns.Add("IZINBITIS", typeof(DateTime)); table.Columns.Add("IzinSuresi", typeof(int));
        table.Rows.Add(11111111111L, "Ada", "Örnek", "TR000000000000000000000000", "Genel", 1500.50m, DateTime.Today, DateTime.Today.AddDays(2), 3); table.AcceptChanges();
        if (form is Genel or Joker or Search or Summary) foreach (var grid in AppTheme.Descendants(form).OfType<DataGridView>()) { grid.BindingContext = new BindingContext(); grid.DataSource = table.Copy(); grid.ClearSelection(); }
        string root = TestContext.CurrentContext.TestDirectory;
        while (!File.Exists(Path.Combine(root, "WinFormsApp2.csproj"))) root = Path.GetDirectoryName(root) ?? throw new DirectoryNotFoundException();
        string folder = Path.Combine(root, "artifacts", "analysis", "ui-preview"); Directory.CreateDirectory(folder);
        if (form is PaymentsForm) foreach (var field in AppTheme.Descendants(form).OfType<TextBox>()) field.Text = "150,50";
        foreach (bool dark in new[] { false, true })
        {
            AppTheme.Apply(form, dark); using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(folder, type.Name + (dark ? "-dark.png" : "-light.png")));
        }
        Assert.That(form.Controls.OfType<ToolStrip>().Count(s => s is not StatusStrip), Is.EqualTo(1)); Assert.That(form.Controls.OfType<StatusStrip>().Single().Items.Count, Is.EqualTo(3));
        Assert.That(form.Controls.OfType<ToolStrip>().All(c => c.Visible), Is.True);
        foreach (var field in AppTheme.Descendants(form).OfType<TextBox>())
        { Assert.That(field.Visible, Is.True); Assert.That(field.Width, Is.GreaterThan(50)); Assert.That(field.Parent!.ClientRectangle.Contains(field.Bounds), Is.True, field.Name); }
    }
    [Test]
    public void ViewerCannotNavigateToAdminOrSettingsAndLogoutDropsRole()
    {
        using var context = new AccountingApplicationContext(page => page == AppPage.Login ? new Giris(() => Task.CompletedTask, (_, _) => Task.FromResult(true)) { Opacity = 0, ShowInTaskbar = false } : new SessionForm { Opacity = 0, ShowInTaskbar = false });
        Assert.That(context.StartSession("viewer", UserRole.Viewer), Is.True); Assert.That(context.Navigate(AppPage.Admin), Is.False); Assert.That(context.Navigate(AppPage.Settings), Is.False);
        Assert.That(context.Navigate(AppPage.Payments), Is.True); context.Logout(); Assert.That(context.Session, Is.Null);
    }
    [Test]
    public void ThemeSetsThirtySixPixelHeadersAndPlaceholders()
    {
        using var form = new Form(); var grid = new DataGridView(); var text = new TextBox { Name = "TC_txt" }; form.Controls.AddRange([grid, text]); AppTheme.Apply(form);
        Assert.That(grid.ColumnHeadersHeight, Is.EqualTo(36)); Assert.That(grid.AllowUserToOrderColumns, Is.True); Assert.That(text.PlaceholderText, Does.Contain("11"));
    }
}
