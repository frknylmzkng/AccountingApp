using Microsoft.Data.SqlClient;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

public partial class GuideDatabaseTests
{
    [TestCase(0), TestCase(1), Category("Personnel")]
    public async Task ArchiveAndRestoreRetainIdentityBalanceLeavesAndPaymentHistory(int groupValue)
    {
        var group = (PersonnelGroup)groupValue;
        await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        if (group == PersonnelGroup.General) await Service.SaveLeavesAsync([(await LeaveSelectionAsync(), new(new(2026, 1, 1), new(2026, 1, 3)))]);
        var payment = await Service.PreparePaymentAsync(group, Person().Tc, 30m, await VersionAsync(group), default);
        Assert.That(payment.FormatVersion, Is.EqualTo(3)); Assert.That(payment.PersonId, Is.Not.Null);
        await Service.SubmitPaymentAsync(payment, default);
        Guid id = payment.PersonId!.Value;
        await Service.ArchiveAsync(group, Person().Tc, await VersionAsync(group), employmentEnd: new DateTime(2099, 12, 31));
        Assert.That((await Service.ListAsync(group)).Rows, Is.Empty);
        var row = (await Service.ListAsync(group, includeArchived: true)).Rows[0];
        Assert.That(row["PersonId"], Is.EqualTo(id)); Assert.That(row["PARA"], Is.EqualTo(70m)); Assert.That(row["ArchivedUtc"], Is.TypeOf<DateTime>());
        var history = await Service.HistoryAsync(group, id);
        Assert.That(history.Payments.Rows.Count, Is.EqualTo(3)); // allocation, payment, zero-delta closure
        Assert.That(history.Leaves.Rows.Count, Is.EqualTo(group == PersonnelGroup.General ? 1 : 0));
        Assert.That(history.Events.Rows.Count, Is.EqualTo(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1)));
        await Service.RestoreArchivedAsync(group, id, (byte[])row[PersonnelConcurrency.ColumnName]);
        var active = (await Service.ListAsync(group)).Rows[0]; Assert.That(active["PersonId"], Is.EqualTo(id)); Assert.That(active["PARA"], Is.EqualTo(70m));
        Assert.That((await Service.HistoryAsync(group, id)).Events.Rows.Count, Is.EqualTo(2));
        Assert.That((await Service.SubmitPaymentAsync(payment, default)).Status, Is.EqualTo(PaymentStatus.Recorded));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.EqualTo(1));
    }

    [Test, Category("Personnel")]
    public async Task ArchiveFailureRollsBackCardEventAndClosureTogether()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1)); var version = await VersionAsync();
        Execute("CREATE TRIGGER dbo.GuideBlockAudit ON dbo.AuditLog AFTER INSERT AS IF EXISTS(SELECT 1 FROM inserted WHERE Action=N'Person.Archive') THROW 51099,N'Archive audit failure',1;");
        await Assert.ThrowsAsync<SqlException>(() => Service.ArchiveAsync(PersonnelGroup.General, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31)));
        Assert.That(await VersionAsync(), Is.EqualTo(version)); Assert.That(Scalar("SELECT ArchivedUtc FROM dbo.Calisan"), Is.EqualTo(DBNull.Value));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PersonnelArchiveEvents"), Is.Zero);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Closure'"), Is.Zero);
    }

    [Test, Category("Personnel")]
    public async Task OldSelectionAndOldUnsentPaymentCannotAffectReactivatedCard()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1)); byte[] selected = await VersionAsync();
        var request = await Service.PreparePaymentAsync(PersonnelGroup.General, Person().Tc, 30m, selected, default);
        await Service.ArchiveAsync(PersonnelGroup.General, Person().Tc, selected, employmentEnd: new DateTime(2099, 12, 31));
        var archived = (await Service.ListAsync(PersonnelGroup.General, includeArchived: true)).Rows[0];
        await Service.RestoreArchivedAsync(PersonnelGroup.General, (Guid)archived["PersonId"], (byte[])archived[PersonnelConcurrency.ColumnName]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.ArchiveAsync(PersonnelGroup.General, Person().Tc, selected, employmentEnd: new DateTime(2099, 12, 31)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.RestoreArchivedAsync(PersonnelGroup.General, (Guid)archived["PersonId"], (byte[])archived[PersonnelConcurrency.ColumnName]));
        Assert.That((await Service.SubmitPaymentAsync(request, default)).Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.Zero);
    }

    [Test, Category("Personnel")]
    public async Task PermanentIdentityCannotBeChangedAndViewerCannotArchive()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Guid id = (Guid)Scalar("SELECT PersonId FROM dbo.Calisan");
        Assert.Throws<SqlException>(() => Execute("UPDATE dbo.Calisan SET PersonId=NEWID()"));
        var viewer = new PersonnelService(connection, CreateActor("archive-viewer", UserRole.Viewer));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => viewer.ArchiveAsync(PersonnelGroup.General, Person().Tc, new byte[8], employmentEnd: new DateTime(2099, 12, 31)));
        Assert.That((await viewer.HistoryAsync(PersonnelGroup.General, id)).Payments.Rows.Count, Is.EqualTo(1));
        Assert.That(Scalar("SELECT PersonId FROM dbo.Calisan"), Is.EqualTo(id));
    }
}
