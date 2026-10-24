using System.Data;
using System.Reflection;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable]
public class LeaveWorkflowTests
{
    private static T Field<T>(Genel form, string name) => (T)typeof(Genel).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;

    private static DataGridView BindPeople(Genel form)
    {
        var table = new DataTable();
        table.Columns.Add("TC", typeof(long));
        foreach (string column in new[] { "AD", "SOYAD", "IBAN" }) table.Columns.Add(column);
        table.Columns.Add("PARA", typeof(decimal));
        table.Columns.Add("IZINBASLANGIC", typeof(DateTime));
        table.Columns.Add("IZINBITIS", typeof(DateTime));
        table.Rows.Add(11111111111L, "Ada", "Test", "TR000000000000000000000000", 1500m,
            new DateTime(2026, 1, 10), new DateTime(2026, 1, 12));
        table.Rows.Add(22222222222L, "Ece", "Test", "TR000000000000000000000000", 2500m,
            DBNull.Value, DBNull.Value);
        table.AcceptChanges();
        var grid = Field<DataGridView>(form, "calisanView");
        var binding = typeof(Genel).GetField("bindingRecords", BindingFlags.Instance | BindingFlags.NonPublic)!;
        binding.SetValue(form, true);
        grid.BindingContext = new BindingContext();
        grid.DataSource = table;
        grid.ClearSelection();
        binding.SetValue(form, false);
        return grid;
    }

    [Test]
    public void NewEmployeeStartsWithLeaveControlsDisabled()
    {
        using var form = new Genel();
        Assert.That(Field<DateTimePicker>(form, "dtpBaslangic").Enabled, Is.False);
        Assert.That(Field<DateTimePicker>(form, "dtpBitis").Enabled, Is.False);
        Assert.That(Field<Button>(form, "izinKaydet_btn").Enabled, Is.False);
        Assert.That(Field<Label>(form, "izinDurumLabel").Text, Does.Contain("listeden personel seçin"));
    }

    [Test]
    public void SwitchingFromEmployeeWithLeaveToEmployeeWithoutLeaveResetsDatesAndTarget()
    {
        using var form = new Genel();
        var grid = BindPeople(form);
        grid.Rows[0].Selected = true;
        Assert.That(Field<DateTimePicker>(form, "dtpBaslangic").Value.Date,
            Is.EqualTo(new DateTime(2026, 1, 10)));
        grid.ClearSelection();
        grid.Rows[1].Selected = true;
        Assert.That(Field<DateTimePicker>(form, "dtpBaslangic").Value.Date, Is.EqualTo(DateTime.Today));
        Assert.That(Field<DateTimePicker>(form, "dtpBitis").Value.Date, Is.EqualTo(DateTime.Today));
        Assert.That(Field<Button>(form, "izinKaydet_btn").Enabled, Is.True);
        Assert.That(Field<Label>(form, "izinDurumLabel").Text, Does.Contain("22222222222"));
    }

    [Test]
    public void ClearRemovesSelectionAndInputsAndDisablesLeaveSaving()
    {
        using var form = new Genel();
        var grid = BindPeople(form);
        grid.Rows[0].Selected = true;
        Field<DateTimePicker>(form, "dtpBitis").Value = new DateTime(2026, 2, 1);
        typeof(Genel).GetMethod("temizle_btn_Click", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(form, new object[] { form, EventArgs.Empty });
        Assert.That(grid.SelectedRows.Count, Is.Zero);
        foreach (string field in new[] { "TC_txt", "ad_txt", "soyad_txt", "IBAN_txt", "money_txt" })
            Assert.That(Field<TextBox>(form, field).Text, Is.Empty);
        Assert.That(Field<Button>(form, "izinKaydet_btn").Enabled, Is.False);
        Assert.That(Field<DateTimePicker>(form, "dtpBaslangic").Enabled, Is.False);
        Assert.That(Field<DateTimePicker>(form, "dtpBitis").Value.Date, Is.EqualTo(DateTime.Today));
    }

    [Test]
    public void DeselectingEmployeeDisablesLeaveSaving()
    {
        using var form = new Genel();
        var grid = BindPeople(form);
        grid.Rows[0].Selected = true;
        grid.ClearSelection();
        Assert.That(Field<Button>(form, "izinKaydet_btn").Enabled, Is.False);
        Assert.That(Field<DateTimePicker>(form, "dtpBaslangic").Enabled, Is.False);
    }
}
