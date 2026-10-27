using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture]
public class LeaveInputTests
{
    [Test]
    public void EndBeforeStartIsRejectedWithAnExplanation()
    {
        Assert.That(LeaveInput.TryCreate(new DateTime(2026, 10, 25), new DateTime(2026, 10, 24),
            out var leave, out string error), Is.False);
        Assert.That(leave, Is.Null);
        Assert.That(error, Does.Contain("önce olamaz"));
    }

    [TestCase(0)]
    [TestCase(5)]
    public void ValidPeriodsUseCalendarDays(int days)
    {
        DateTime start = new DateTime(2026, 10, 24, 23, 59, 0);
        DateTime end = start.Date.AddDays(days).AddMinutes(1);
        Assert.That(LeaveInput.TryCreate(start, end, out var leave, out string error), Is.True);
        Assert.That(leave, Is.EqualTo(new LeavePeriod(start.Date, end.Date)));
        Assert.That(error, Is.Empty);
    }

    [Test]
    public void InvalidDatesAreRejectedAtStorageBoundaryWithoutConnectingToDatabase()
    {
        var store = new PersonnelStore("deliberately invalid connection string");
        var invalid = new LeavePeriod(new DateTime(2026, 10, 25), new DateTime(2026, 10, 24));
        Assert.Throws<ArgumentException>(() => store.SaveLeave(new LeaveTarget(11111111111L, new byte[8], null), invalid));
    }

    [Test]
    public void JokerCannotSilentlyIgnoreAnExplicitLeave()
    {
        var store = new PersonnelStore("deliberately invalid connection string");
        var person = new PersonnelDetails(11111111111L, "Ada", "Test", "TR000000000000000000000000", 100m);
        var leave = new LeavePeriod(DateTime.Today, DateTime.Today);
        Assert.Throws<ArgumentException>(() => store.Add(PersonnelGroup.Joker, person, leave));
    }
}
