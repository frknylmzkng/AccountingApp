using System.Data;
using System.Text.Json;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, Apartment(ApartmentState.STA), NonParallelizable, Category("Payments")]
public class PaymentSelectionTests
{
    [Test]
    public void SortedSelectionUsesOriginalIdentityAndVersionBeforeConfirmation()
    {
        using var table = new DataTable(); table.Columns.Add("TC", typeof(long)); table.Columns.Add("GRUP"); table.Columns.Add("RecordVersion", typeof(byte[]));
        byte[] first = [1, 0, 0, 0, 0, 0, 0, 0], second = [2, 0, 0, 0, 0, 0, 0, 0];
        table.Rows.Add(11111111111L, "Genel", first); table.Rows.Add(22222222222L, "Joker", second); table.AcceptChanges();
        using var grid = new DataGridView { BindingContext = new BindingContext() }; RecordSelection.Configure(grid); grid.DataSource = table;
        table.DefaultView.Sort = "TC DESC"; grid.ClearSelection(); grid.Rows[0].Selected = true;
        var row = ((DataRowView)grid.Rows[0].DataBoundItem!).Row;
        row["TC"] = 33333333333L; row["GRUP"] = "Genel"; row["RecordVersion"] = first;
        var snapshot = PaymentTarget.FromSelection(grid);
        Assert.That(snapshot.Tc, Is.EqualTo(22222222222L)); Assert.That(snapshot.Group, Is.EqualTo(PersonnelGroup.Joker)); Assert.That(snapshot.PersonVersion, Is.EqualTo(second));
        // A confirmation dialog may run the UI message loop; rebinding afterwards
        // must not change the target that was already frozen.
        grid.DataSource = null; second[0] = 77;
        Assert.That(snapshot.PersonVersion[0], Is.EqualTo(2));
        RecordSelection.HideInternalColumns(grid);
    }

    [TestCase(-1), TestCase(0), TestCase(7), TestCase(9)]
    public async Task MissingOrMalformedSelectionVersionStopsBeforeDatabaseAccess(int length)
    {
        var service = new PersonnelService("invalid connection string", new("operator", UserRole.PersonnelManager) { PaymentGranted = true, SalaryViewGranted = true });
        byte[] version = length < 0 ? null! : new byte[length];
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.PreparePaymentAsync(PersonnelGroup.General, 11111111111L, 30m, version, default));
        Assert.That(error!.Message, Is.EqualTo(PaymentTarget.SelectionMessage));
    }

    [Test]
    public void SelectionWithoutSnapshotMetadataOrWithUnknownGroupIsRejected()
    {
        using var table = new DataTable(); table.Columns.Add("TC", typeof(long)); table.Columns.Add("GRUP");
        table.Rows.Add(11111111111L, "Genel"); table.AcceptChanges();
        using var grid = new DataGridView { BindingContext = new BindingContext() }; RecordSelection.Configure(grid); grid.DataSource = table;
        grid.ClearSelection(); grid.Rows[0].Selected = true;
        Assert.Throws<InvalidOperationException>(() => PaymentTarget.FromSelection(grid));
        table.Columns.Add("RecordVersion", typeof(byte[])); table.Rows[0]["RecordVersion"] = new byte[8]; table.Rows[0]["GRUP"] = "Unknown"; table.AcceptChanges();
        Assert.Throws<InvalidOperationException>(() => PaymentTarget.FromSelection(grid));
    }

    [Test]
    public void JournalFormatsPreserveLegacyAndNewVersionedRequestsWithoutGuessing()
    {
        var legacy = JsonSerializer.Deserialize<PaymentRequest>("{\"RequestId\":\"00000000-0000-0000-0000-000000000001\",\"Group\":0,\"Tc\":11111111111,\"Amount\":30,\"ClosureId\":0}")!;
        Assert.DoesNotThrow(legacy.Validate); Assert.That(legacy.FormatVersion, Is.Zero); Assert.That(legacy.PersonVersion, Is.Null);
        var oldVersioned = legacy with { FormatVersion = 2, PersonVersion = [1, 2, 3, 4, 5, 6, 7, 8] };
        Assert.DoesNotThrow(oldVersioned.Validate);
        var current = oldVersioned with { FormatVersion = 3, PersonId = Guid.NewGuid() };
        var roundTrip = JsonSerializer.Deserialize<PaymentRequest>(JsonSerializer.Serialize(current))!;
        Assert.DoesNotThrow(roundTrip.Validate); Assert.That(roundTrip.PersonVersion, Is.EqualTo(current.PersonVersion));
        Assert.Throws<InvalidOperationException>(() => (current with { PersonVersion = null }).Validate());
        Assert.Throws<InvalidOperationException>(() => (current with { PersonId = null }).Validate());
        Assert.Throws<InvalidOperationException>(() => (current with { PersonId = Guid.Empty }).Validate());
        Assert.Throws<InvalidOperationException>(() => (current with { FormatVersion = 99 }).Validate());
        Assert.Throws<InvalidOperationException>(() => (legacy with { PersonVersion = new byte[8] }).Validate());
        var copy = current.Snapshot(); current.PersonVersion![0] = 88; Assert.That(copy.PersonVersion![0], Is.EqualTo(1));
    }
}

public partial class GuideDatabaseTests
{
    [TestCase(0, "Recreated"), TestCase(1, "Recreated")]
    [TestCase(0, "Name"), TestCase(1, "Name")]
    [TestCase(0, "Iban"), TestCase(1, "Iban")]
    [TestCase(0, "Payment"), TestCase(1, "Payment")]
    [Category("Payments")]
    public async Task StalePaymentSelectionIsRejectedBeforePreparingOrWritingJournal(int groupId, string change)
    {
        var group = (PersonnelGroup)groupId; await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        byte[] selected = await VersionAsync(group);
        await ChangePaymentPersonAsync(group, change);
        decimal balance = (decimal)Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}");
        int audits = (int)Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), ledger = (int)Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger");
        var journal = new PaymentJournal(connection, Path.Combine(directory, "selection-" + Guid.NewGuid().ToString("N")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await new PaymentWorkflow(journal, Service, admin).BeginAsync(group, Person().Tc, 30m, selected));

        Assert.That(error!.Message, Is.EqualTo(PaymentTarget.ConflictMessage));
        Assert.That(File.Exists(journal.PathName), Is.False);
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(balance));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
    }

    [TestCase(0, "Recreated"), TestCase(1, "Recreated")]
    [TestCase(0, "Name"), TestCase(1, "Name")]
    [TestCase(0, "Iban"), TestCase(1, "Iban")]
    [TestCase(0, "Payment"), TestCase(1, "Payment")]
    [Category("Payments")]
    public async Task PreparedPaymentRejectsChangesBeforeSubmissionAndKeepsDefinitiveRejection(int groupId, string change)
    {
        var group = (PersonnelGroup)groupId; await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        var journal = new PaymentJournal(connection, Path.Combine(directory, "prepared-" + Guid.NewGuid().ToString("N")));
        var interrupted = new PaymentWorkflow(journal, new LostReplyGateway(Service, false), admin);
        await Assert.ThrowsAsync<IOException>(async () => await interrupted.BeginAsync(group, Person().Tc, 30m, await VersionAsync(group)));
        await ChangePaymentPersonAsync(group, change);
        decimal balance = (decimal)Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}");
        int ledger = (int)Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), audits = (int)Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'");
        var reopened = new PaymentWorkflow(journal, Service, admin);

        var rejected = await reopened.RetryAsync();

        Assert.That(rejected.Outcome.Status, Is.EqualTo(PaymentStatus.Rejected)); Assert.That(rejected.Outcome.Reason, Is.EqualTo(PaymentTarget.ConflictMessage));
        Assert.That((await reopened.InspectAsync())!.Outcome.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(balance));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
        await reopened.AcknowledgeAsync();
        Assert.That((await reopened.BeginAsync(group, Person().Tc, 10m, await VersionAsync(group))).Outcome.Status, Is.EqualTo(PaymentStatus.Recorded));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(balance - 10m));
    }

    [TestCase(0, "Recreated"), TestCase(1, "Recreated")]
    [TestCase(0, "Name"), TestCase(1, "Name")]
    [TestCase(0, "Deleted"), TestCase(1, "Deleted")]
    [TestCase(0, "Reversed"), TestCase(1, "Reversed")]
    [Category("Payments")]
    public async Task RecordedRequestIsRecognizedAfterPersonnelVersionChangesOrDisappears(int groupId, string change)
    {
        var group = (PersonnelGroup)groupId; await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        var request = await Service.PreparePaymentAsync(group, Person().Tc, 30m, await VersionAsync(group), default);
        Assert.That((await Service.SubmitPaymentAsync(request, default)).Status, Is.EqualTo(PaymentStatus.Recorded));
        if (change == "Deleted") await Service.DeleteAsync(group, Person().Tc, await VersionAsync(group), employmentEnd: new DateTime(2099, 12, 31));
        else if (change == "Reversed") await Service.ReversePaymentAsync(request.RequestId);
        else await ChangePaymentPersonAsync(group, change);
        int audits = (int)Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), ledger = (int)Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger");

        var outcome = await Service.SubmitPaymentAsync(request, default);

        Assert.That(outcome.Status, Is.EqualTo(change == "Reversed" ? PaymentStatus.Reversed : PaymentStatus.Recorded));
        Assert.That((await Service.InspectPaymentAsync(request, default)).Status, Is.EqualTo(outcome.Status));
        Assert.That(await Service.PayAsync(group, Person().Tc, 30m, request.RequestId), Is.EqualTo(request.RequestId));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger)); Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action<>N'Record.Change'"), Is.EqualTo(audits));
    }

    [TestCase(0, false), TestCase(1, false), TestCase(0, true), TestCase(1, true)]
    [Category("Payments")]
    public async Task LegacyPendingRequestOnlyRecoversExistingPaymentAndNeverCreatesNewDebit(int groupId, bool recorded)
    {
        var group = (PersonnelGroup)groupId; await Service.SaveAsync(group, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        Guid id = Guid.NewGuid(); if (recorded) await Service.PayCurrentAsync(group, Person().Tc, 30m, id);
        var legacy = new PaymentRequest(id, group, Person().Tc, 30m, 0);
        var journal = new PaymentJournal(connection, Path.Combine(directory, "legacy-" + Guid.NewGuid().ToString("N")));
        // Serialize the old shape, with no format/version fields, through DPAPI.
        string scope = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).DataSource.ToUpperInvariant() + "|" +
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog.ToUpperInvariant() + "|" + Path.GetFullPath(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).AttachDBFilename).ToUpperInvariant();
        DraftStore.Save(journal.PathName, scope, new { Request = new { legacy.RequestId, legacy.Group, legacy.Tc, legacy.Amount, legacy.ClosureId }, Rejection = (object?)null });
        var flow = new PaymentWorkflow(journal, Service, admin);
        int ledger = (int)Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger");
        var result = await flow.RetryAsync();
        Assert.That(result.Outcome.Status, Is.EqualTo(recorded ? PaymentStatus.Recorded : PaymentStatus.Rejected));
        if (!recorded) Assert.That(result.Outcome.Reason, Is.EqualTo(PaymentTarget.LegacyMessage));
        Assert.That(Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}"), Is.EqualTo(recorded ? 70m : 100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger"), Is.EqualTo(ledger));
        await flow.AcknowledgeAsync(); Assert.That(File.Exists(journal.PathName), Is.False);
    }

    [Test, Category("Payments")]
    public async Task VersionlessDirectPaymentCannotBypassSelectedRecordProtection()
    {
        await Service.SaveAsync(PersonnelGroup.General, Person(), true, employmentStart: new DateTime(2000, 1, 1));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Service.PayAsync(PersonnelGroup.General, Person().Tc, 30m, Guid.NewGuid()));
        Assert.That(Scalar("SELECT PARA FROM dbo.Calisan"), Is.EqualTo(100m));
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.PaymentLedger WHERE Kind=N'Payment'"), Is.Zero);
    }

    private async Task ChangePaymentPersonAsync(PersonnelGroup group, string change)
    {
        byte[] version = await VersionAsync(group);
        if (change == "Recreated") { await Service.DeleteAsync(group, Person().Tc, version, employmentEnd: new DateTime(2099, 12, 31)); await ReactivateAndEditAsync(group, Person(amount: 250m) with { FirstName = "Yeni" }); }
        else if (change == "Payment") await Service.PayAsync(group, Person().Tc, 10m, Guid.NewGuid(), expectedVersion: version);
        else
        {
            decimal balance = (decimal)Scalar($"SELECT PARA FROM {PersonnelRepository.Table(group)}");
            var person = Person(amount: balance);
            await Service.SaveAsync(group, change == "Name" ? person with { FirstName = "Yeni" } : person with { Iban = "TR330006100519786457841326" }, false, expectedVersion: version);
        }
    }
}
