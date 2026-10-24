using System.Data;
using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA)]
public class RecordSelectionTests
{
    [TestCase(typeof(Admin))]
    [TestCase(typeof(Genel))]
    [TestCase(typeof(Joker))]
    [TestCase(typeof(Search))]
    [TestCase(typeof(Summary))]
    [TestCase(typeof(Contact))]
    public void EmbeddedNavigationImagesLoadOnTheUpdatedRuntime(Type formType)
    {
        using var form = (Form)Activator.CreateInstance(formType)!;
        var back = (Button)formType.GetField("back",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Assert.That(back.BackgroundImage, Is.Not.Null);
        Assert.That(back.BackgroundImage!.Width, Is.GreaterThan(0));
        using var rendered = new Bitmap(back.Width, back.Height);
        back.DrawToBitmap(rendered, new Rectangle(Point.Empty, rendered.Size));
    }

    [Test]
    public void PasswordFieldsAreMaskedAndUsernameLengthsAgree()
    {
        using var admin = new Admin();
        using var login = new Giris();
        TextBox Field(Form form, string name) => (TextBox)form.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Assert.Multiple(() =>
        {
            Assert.That(Field(admin, "txtPassword").UseSystemPasswordChar, Is.True);
            Assert.That(Field(login, "password_txt").UseSystemPasswordChar, Is.True);
            Assert.That(Field(admin, "txtPassword").MaxLength, Is.EqualTo(PasswordHasher.MaxPasswordLength));
            Assert.That(Field(admin, "txtUsername").MaxLength, Is.EqualTo(50));
            Assert.That(Field(login, "username_txt").MaxLength, Is.EqualTo(50));
        });
    }

    private static DataTable Records()
    {
        var table = new DataTable();
        table.Columns.Add("TC", typeof(long));
        table.Columns.Add("username", typeof(string));
        table.Rows.Add(11111111111L, "alpha");
        table.Rows.Add(22222222222L, "beta");
        table.AcceptChanges();
        return table;
    }

    private static DataGridView Bind(DataTable table)
    {
        var grid = new DataGridView { BindingContext = new BindingContext() };
        RecordSelection.Configure(grid);
        grid.DataSource = table;
        grid.ClearSelection();
        return grid;
    }

    [TestCase(typeof(Genel), "calisanView")]
    [TestCase(typeof(Joker), "jokerView")]
    [TestCase(typeof(Admin), "adminView")]
    [TestCase(typeof(Search), "dataGridViewAramaSonuclari")]
    public void ActualFormsPreventCellEditsAndAmbiguousSelections(Type formType, string fieldName)
    {
        using var form = (Form)Activator.CreateInstance(formType)!;
        var grid = (DataGridView)formType.GetField(fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        Assert.Multiple(() =>
        {
            Assert.That(grid.ReadOnly, Is.True);
            Assert.That(grid.AllowUserToAddRows, Is.False);
            Assert.That(grid.AllowUserToDeleteRows, Is.False);
            Assert.That(grid.MultiSelect, Is.False);
            Assert.That(grid.SelectionMode, Is.EqualTo(DataGridViewSelectionMode.FullRowSelect));
        });
    }

    [Test]
    public void NoSelectionCannotSupplyADeleteTarget()
    {
        using var grid = Bind(Records());
        Assert.That(RecordSelection.TryGetTc(grid, out _), Is.False);
    }

    [TestCase(typeof(Genel), "calisanView")]
    [TestCase(typeof(Joker), "jokerView")]
    public void ReloadDoesNotPopulateInputsUntilTheUserSelectsARecord(Type formType, string fieldName)
    {
        using var form = (Form)Activator.CreateInstance(formType)!;
        object Field(string name) => formType.GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        var grid = (DataGridView)Field(fieldName);
        var bindingFlag = formType.GetField("bindingRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var table = Records();
        foreach (string column in new[] { "AD", "SOYAD", "IBAN" }) table.Columns.Add(column);
        table.Columns.Add("PARA", typeof(decimal));
        table.Columns.Add("IZINBASLANGIC", typeof(DateTime));
        table.Columns.Add("IZINBITIS", typeof(DateTime));
        foreach (DataRow row in table.Rows)
        {
            row["AD"] = "Ada";
            row["SOYAD"] = "Test";
            row["IBAN"] = "TR000000000000000000000000";
            row["PARA"] = 1500.50m;
        }
        table.AcceptChanges();

        bindingFlag.SetValue(form, true);
        grid.BindingContext = new BindingContext();
        grid.DataSource = table;
        grid.ClearSelection();
        bindingFlag.SetValue(form, false);
        Assert.That(((TextBox)Field("TC_txt")).Text, Is.Empty);
        Assert.That(((TextBox)Field("money_txt")).Text, Is.Empty);

        grid.Rows[1].Selected = true;
        Assert.That(((TextBox)Field("TC_txt")).Text, Is.EqualTo("22222222222"));
        Assert.That(((TextBox)Field("money_txt")).Text, Is.EqualTo("1500,50"));
    }

    [Test]
    public void ChangedDisplayIdentityCannotRedirectDeletion()
    {
        var table = Records();
        using var grid = Bind(table);
        table.Rows[0]["TC"] = 22222222222L;
        grid.Rows[0].Selected = true;
        Assert.That(RecordSelection.TryGetTc(grid, out long tc), Is.True);
        Assert.That(tc, Is.EqualTo(11111111111L));
    }

    [Test]
    public void ChangedAdminDisplayNameCannotRedirectDeletion()
    {
        var table = Records();
        using var grid = Bind(table);
        table.Rows[0]["username"] = "beta";
        grid.Rows[0].Selected = true;
        Assert.That(RecordSelection.TryGetKey(grid, "username", out string username), Is.True);
        Assert.That(username, Is.EqualTo("alpha"));
    }

    [Test]
    public void SortingUsesTheSelectedRecordInsteadOfItsOldRowIndex()
    {
        var table = Records();
        using var grid = Bind(table);
        table.DefaultView.Sort = "TC DESC";
        grid.ClearSelection();
        grid.Rows[0].Selected = true;
        Assert.That(RecordSelection.TryGetTc(grid, out long tc), Is.True);
        Assert.That(tc, Is.EqualTo(22222222222L));
    }

    [Test]
    public void AnUnsavedLocalRowCannotBecomeADeleteTarget()
    {
        var table = Records();
        table.Rows.Add(33333333333L, "local");
        using var grid = Bind(table);
        grid.Rows[2].Selected = true;
        Assert.That(RecordSelection.TryGetTc(grid, out _), Is.False);
    }
}
