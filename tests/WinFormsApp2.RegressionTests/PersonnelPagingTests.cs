using System.Data;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [Test, Category("Paging")]
    public async Task PersonnelPagesKeepAllLeavesWithOnePersonAndUseDeterministicDistinctPeople()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 100m), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.SaveAsync(PersonnelGroup.Joker, Person(22222222222L, 200m), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.SaveAsync(PersonnelGroup.General, Person(33333333333L, 300m), true, employmentStart: new DateTime(2000, 1, 1));
        Execute(@"INSERT dbo.izinler(PersonId,EmploymentId,TC,IZINBASLANGIC,IZINBITIS)
            SELECT PersonId,CurrentEmploymentId,TC,'20261001','20261031' FROM dbo.Calisan WHERE TC=11111111111;
            INSERT dbo.izinler(PersonId,EmploymentId,TC,IZINBASLANGIC,IZINBITIS)
            SELECT PersonId,CurrentEmploymentId,TC,'20261010','20261030' FROM dbo.Calisan WHERE TC=11111111111;");
        var first = await Service.PageAsync(null, 0, 1);
        Assert.That(first.Total, Is.EqualTo(3)); Assert.That(first.Rows.Rows.Count, Is.EqualTo(2));
        Assert.That(first.Rows.AsEnumerable().Select(r => r.Field<long>("TC")).Distinct(), Is.EqualTo(new[] { 11111111111L }));
        var second = await Service.PageAsync(null, 1, 1);
        Assert.That(second.Rows.Rows[0].Field<long>("TC"), Is.EqualTo(22222222222L));
        var last = await Service.PageAsync(null, 999, 1);
        Assert.That(last.Index, Is.EqualTo(2)); Assert.That(last.Rows.Rows[0].Field<long>("TC"), Is.EqualTo(33333333333L));
        var selector = await Service.PageAsync(null, 0, 1, includeLeaves: false);
        Assert.That(selector.Rows.Rows.Count, Is.EqualTo(1));
        Assert.That(selector.Rows.Rows[0].RowState, Is.EqualTo(DataRowState.Unchanged));
    }

    [Test, Category("Paging")]
    public async Task SqlSummaryCountsAllActivePeopleAndDistinctLeaveHoldersRegardlessOfAlertPage()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 100m), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.SaveAsync(PersonnelGroup.Joker, Person(22222222222L, 200m), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.SaveAsync(PersonnelGroup.General, Person(33333333333L, 300m), true, employmentStart: new DateTime(2000, 1, 1));
        Execute(@"INSERT dbo.izinler(PersonId,EmploymentId,TC,IZINBASLANGIC,IZINBITIS)
            SELECT PersonId,CurrentEmploymentId,TC,'20261001','20261031' FROM dbo.Calisan WHERE TC=11111111111;
            INSERT dbo.izinler(PersonId,EmploymentId,TC,IZINBASLANGIC,IZINBITIS)
            SELECT PersonId,CurrentEmploymentId,TC,'20261010','20261030' FROM dbo.Calisan WHERE TC=11111111111;");
        var summary = await Service.SummaryAsync(new DateTime(2026, 10, 20), 999, 1);
        Assert.That(summary.GeneralCount, Is.EqualTo(2)); Assert.That(summary.JokerCount, Is.EqualTo(1));
        Assert.That(summary.OnLeaveCount, Is.EqualTo(1)); Assert.That(summary.Balance, Is.EqualTo(600m));
        Assert.That(summary.Average, Is.EqualTo(200m)); Assert.That(summary.Alerts.Total, Is.EqualTo(2));
        Assert.That(summary.Alerts.Rows.Rows.Count, Is.EqualTo(1));
        await Service.ArchiveAsync(PersonnelGroup.General, 33333333333L, await VersionAsync(tc: 33333333333L), employmentEnd: new DateTime(2099, 12, 31));
        summary = await Service.SummaryAsync(new DateTime(2026, 10, 20));
        Assert.That(summary.Balance, Is.EqualTo(300m)); Assert.That(summary.GeneralCount, Is.EqualTo(1));
        var archived = await Service.PageAsync(null, includeArchived: true, archivedOnly: true);
        Assert.That(archived.Total, Is.EqualTo(1));
    }

    [Test, Category("Paging")]
    public async Task LedgerRunningBalanceIncludesMovementsBeforeTheDisplayedPage()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        await Service.SaveAsync(PersonnelGroup.General, Person(amount: 120m), false, expectedVersion: await VersionAsync());
        await Service.PayCurrentAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid());
        var latest = await Service.LedgerPageAsync(0, 1);
        Assert.That(latest.Rows.Rows[0].Field<decimal>("RunningBalance"), Is.EqualTo(90m));
        var earlier = await Service.LedgerPageAsync(1, 1);
        Assert.That(earlier.Rows.Rows[0].Field<decimal>("RunningBalance"), Is.EqualTo(120m));
        Assert.That(latest.Total, Is.EqualTo(3));
    }

    [Test, Category("Paging")]
    public async Task TypedTcSearchAndLiteralLikeEscapingDoNotBroadenQueries()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Assert.That((await Service.PageAsync(null, tc: "11111111111")).Total, Is.EqualTo(1));
        Assert.That((await Service.PageAsync(null, tc: "1 OR 1=1")).Total, Is.Zero);
        Assert.That((await Service.PageAsync(null, first: "Ada_")).Total, Is.Zero);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.PageAsync(null, -1, 50));
        await Assert.ThrowsAsync<ArgumentException>(() => Service.PageAsync(null, 0, 201));
    }
}
