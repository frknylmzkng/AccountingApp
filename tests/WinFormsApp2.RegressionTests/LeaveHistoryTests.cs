using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [Test, Category("Leaves")]
    public async Task LeaveDateChangesRetainPreviousDatesActorAndPermanentIdentity()
    {
        await SeedLeavePerson();
        Guid id = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan");
        await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), Period(2))]);
        await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), Period(3))]);
        var history = (await Service.HistoryAsync(PersonnelGroup.General, id)).LeaveChanges;
        Assert.That(history.Rows.Count, Is.EqualTo(3));
        Assert.That(Scalar("SELECT OldStart FROM dbo.LeaveHistory WHERE HistoryId=(SELECT MAX(HistoryId) FROM dbo.LeaveHistory)"), Is.EqualTo(Period(2).Start));
        Assert.That(Scalar("SELECT NewStart FROM dbo.LeaveHistory WHERE HistoryId=(SELECT MAX(HistoryId) FROM dbo.LeaveHistory)"), Is.EqualTo(Period(3).Start));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory WHERE PersonId=(SELECT PersonId FROM dbo.Calisan) AND Actor=N'test-admin' AND ChangedUtc IS NOT NULL"), Is.EqualTo(3));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.izinler"), Is.EqualTo(1));
        await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), Period(3))]);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(3)); // no fake change for identical dates
    }

    [Test, Category("Leaves")]
    public async Task AuditFailureOrStaleSelectionCannotLeavePhantomLeaveHistory()
    {
        await SeedLeavePerson(); var stale = await LeaveSelectionAsync();
        await Service.SaveLeavesAsync([(stale, Period(2))]); int rows = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SaveLeavesAsync([(stale, Period(3))]));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(rows));
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Leave.SavePlan') THROW 51099,N'Leave history rollback',1;");
        await Assert.ThrowsAsync<SqlException>(async () => await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), Period(3))]));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(rows));
        Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler"), Is.EqualTo(Period(2).Start));
    }

    [Test, Category("Leaves")]
    public async Task DirectSqlChangesNeverReusePreviousApplicationActorAndDeletesKeepHistory()
    {
        await SeedLeavePerson(); int leaveId = Convert.ToInt32(Scalar("SELECT IzinId FROM dbo.izinler"));
        Execute("UPDATE dbo.izinler SET IZINBASLANGIC='20260201',IZINBITIS='20260203'");
        Assert.That(Scalar("SELECT Actor FROM dbo.LeaveHistory WHERE HistoryId=(SELECT MAX(HistoryId) FROM dbo.LeaveHistory)"), Is.EqualTo(DBNull.Value));
        Assert.That(Scalar("SELECT SqlPrincipal FROM dbo.LeaveHistory WHERE HistoryId=(SELECT MAX(HistoryId) FROM dbo.LeaveHistory)"), Is.Not.EqualTo(DBNull.Value));
        Execute("DELETE FROM dbo.izinler");
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(3));
        Assert.That(Scalar("SELECT IzinId FROM dbo.LeaveHistory WHERE Operation=N'Delete'"), Is.EqualTo(leaveId));
        Assert.That(Scalar("SELECT OldStart FROM dbo.LeaveHistory WHERE Operation=N'Delete'"), Is.EqualTo(Period(2).Start));
        Assert.Throws<SqlException>(() => Execute("DELETE FROM dbo.LeaveHistory"));
        Assert.Throws<SqlException>(() => Execute("UPDATE dbo.LeaveHistory SET OldStart='20000101'"));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(3));
    }

    [Test, Category("Leaves")]
    public async Task BulkRollbackAndPersonnelArchivePreserveLeaveHistory()
    {
        await SeedLeavePerson(); await SeedLeavePerson(22222222222L);
        var first = await LeaveSelectionAsync(); var oldSecond = await LeaveSelectionAsync(22222222222L);
        await Service.SaveLeavesAsync([(oldSecond, Period(2))]); int rows = Convert.ToInt32(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SaveLeavesAsync([(first, Period(3)), (oldSecond, Period(3))]));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.LeaveHistory"), Is.EqualTo(rows));
        Assert.That(Scalar("SELECT IZINBASLANGIC FROM dbo.izinler WHERE TC=11111111111"), Is.EqualTo(Period(1).Start));
        Guid id = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan WHERE TC=11111111111");
        await Service.ArchiveAsync(PersonnelGroup.General, Person().Tc, await VersionAsync(), employmentEnd: new DateTime(2099, 12, 31));
        Assert.That((await Service.HistoryAsync(PersonnelGroup.General, id)).LeaveChanges.Rows.Count, Is.EqualTo(1));
        var viewer = new PersonnelService(connection, CreateActor("leave-history-viewer", UserRole.Viewer));
        Assert.That((await viewer.HistoryAsync(PersonnelGroup.General, id)).LeaveChanges.Rows.Count, Is.EqualTo(1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewer.SaveLeavesAsync([(first, Period(2))]));
    }
}
