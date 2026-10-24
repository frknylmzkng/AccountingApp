using System.Globalization;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture]
public class MoneyInputTests
{
    [TestCase("tr-TR")]
    [TestCase("en-US")]
    [TestCase("de-DE")]
    public void SameAmountReachesPersonnelDataRegardlessOfComputerCulture(string cultureName)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            foreach (string text in new[] { "1500,50", "1500.50", "1.500,50", "1,500.50", " 1500,50 " })
            {
                bool valid = PersonnelInput.TryCreate("11111111111", "Ada", "Test",
                    "TR000000000000000000000000", text, out var person, out string error);
                Assert.That(valid, Is.True, text + ": " + error);
                Assert.That(person!.Amount, Is.EqualTo(1500.50m), text);
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestCase("0", "0")]
    [TestCase("1500", "1500")]
    [TestCase("1500,5", "1500.5")]
    [TestCase("1500.5", "1500.5")]
    [TestCase("1.500.000,50", "1500000.50")]
    [TestCase("1,500,000.50", "1500000.50")]
    [TestCase("-100,50", "-100.50")]
    [TestCase("+100.50", "100.50")]
    [TestCase("00001500,50", "1500.50")]
    [TestCase("0,01", "0.01")]
    [TestCase("-0,00", "0")]
    [TestCase("79228162514264337593543950335.00", "79228162514264337593543950335")]
    public void ExplicitAmountFormatsPreserveValue(string input, string expected)
    {
        Assert.That(MoneyInput.TryParse(input, out decimal amount), Is.True);
        Assert.That(amount, Is.EqualTo(decimal.Parse(expected, CultureInfo.InvariantCulture)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("1.500")]
    [TestCase("1,500")]
    [TestCase("1500,500")]
    [TestCase("1500.500")]
    [TestCase("1.50,50")]
    [TestCase("1,50.50")]
    [TestCase("1.500.000")]
    [TestCase("1e3")]
    [TestCase("1500 TL")]
    [TestCase("₺1500")]
    [TestCase("1 500,50")]
    [TestCase("12,34,56")]
    [TestCase("NaN")]
    [TestCase("79228162514264337593543950336")]
    [TestCase("79228162514264337593543950335.01")]
    public void AmbiguousMalformedOrInexactAmountsAreRejected(string? input)
    {
        Assert.That(MoneyInput.TryParse(input, out _), Is.False, input);
    }

    [TestCase("1500.50")]
    [TestCase("0.01")]
    [TestCase("-100.50")]
    public void SelectingAndSavingAnAmountDoesNotChangeIt(string value)
    {
        decimal original = decimal.Parse(value, CultureInfo.InvariantCulture);
        Assert.That(MoneyInput.TryParse(MoneyInput.Format(original), out decimal parsed), Is.True);
        Assert.That(parsed, Is.EqualTo(original));
    }

    [Test]
    public void ExistingExtraPrecisionIsVisibleInsteadOfSilentlyRounded()
    {
        Assert.That(MoneyInput.Format(12.3456m), Is.EqualTo("12,3456"));
        Assert.That(MoneyInput.TryParse(MoneyInput.Format(12.3456m), out _), Is.False);
    }
}
