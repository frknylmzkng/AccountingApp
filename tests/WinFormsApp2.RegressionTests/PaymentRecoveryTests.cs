using System.Security.Cryptography;
using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, NonParallelizable, Category("Payments")]
public class PaymentRecoveryTests
{
    private string directory = "";
    private const string Connection = "Server=(LocalDB)\\RecoveryTests;Database=Recovery;Integrated Security=true";
    private readonly UserSession user = new("operator", UserRole.PersonnelManager);
    private PaymentJournal Journal => new(Connection, directory);
    [SetUp] public void Setup() { directory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Recovery_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
    [TearDown] public void Cleanup() { Directory.Delete(directory, true); }

    private sealed class Gateway : IPaymentGateway
    {
        internal readonly Dictionary<Guid, PaymentRequest> Committed = [];
        internal readonly List<PaymentRequest> Sent = [];
        internal int Prepared;
        internal Func<PaymentRequest, CancellationToken, Task<PaymentOutcome>>? Submit;
        internal bool InspectionUnavailable;
        public Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, CancellationToken token)
        { Prepared++; return Task.FromResult(new PaymentRequest(Guid.NewGuid(), group, tc, amount, 0)); }
        public Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token)
        {
            if (InspectionUnavailable) throw new IOException("Simulated connection loss");
            return Task.FromResult(new PaymentOutcome(Committed.ContainsKey(request.RequestId) ? PaymentStatus.Recorded : PaymentStatus.Unconfirmed));
        }
        public Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token)
        {
            Sent.Add(request);
            if (Submit != null) return Submit(request, token);
            Committed.TryAdd(request.RequestId, request);
            return Task.FromResult(new PaymentOutcome(PaymentStatus.Recorded));
        }
    }
    private PaymentWorkflow Flow(Gateway gateway) => new(Journal, gateway, user);

    [Test]
    public async Task LostCommitResponseSurvivesReopeningAndNeverSendsAnotherPayment()
    {
        var gateway = new Gateway();
        gateway.Submit = (request, _) => { gateway.Committed.Add(request.RequestId, request); throw new IOException("Response lost after commit"); };
        await Assert.ThrowsAsync<IOException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));

        var reopened = Flow(gateway);
        Assert.That((await reopened.InspectAsync())!.Outcome.Status, Is.EqualTo(PaymentStatus.Recorded));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await reopened.BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        Assert.That((await reopened.RetryAsync()).Outcome.Status, Is.EqualTo(PaymentStatus.Recorded));
        Assert.That(gateway.Sent, Has.Count.EqualTo(1)); Assert.That(gateway.Prepared, Is.EqualTo(1));
        await reopened.AcknowledgeAsync();
        Assert.That(await Flow(gateway).InspectAsync(), Is.Null);
    }
    [Test]
    public async Task LostResponseBeforeCommitRetriesIdenticalPayloadAndCannotBeAcknowledgedEarly()
    {
        var gateway = new Gateway { Submit = (_, _) => throw new IOException("Request not committed") };
        await Assert.ThrowsAsync<IOException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.Joker, 11111111111L, 30m));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Flow(gateway).AcknowledgeAsync());
        gateway.Submit = null;

        await Flow(gateway).RetryAsync();

        Assert.That(gateway.Sent, Has.Count.EqualTo(2)); Assert.That(gateway.Sent[1], Is.EqualTo(gateway.Sent[0]));
        Assert.That(gateway.Committed, Has.Count.EqualTo(1)); Assert.That(gateway.Prepared, Is.EqualTo(1));
    }
    [Test]
    public async Task SuccessfulReplyIsRetainedUntilAcknowledgedAndAllowsIntentionalNextPayment()
    {
        var gateway = new Gateway();
        var first = await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        await Flow(gateway).AcknowledgeAsync();
        var next = await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        Assert.That(next.Request.RequestId, Is.Not.EqualTo(first.Request.RequestId));
        Assert.That(gateway.Committed, Has.Count.EqualTo(2));
    }
    [Test]
    public async Task DefinitelyRejectedRequestIsRetainedAndCanBeAcknowledgedWithoutResubmitting()
    {
        var gateway = new Gateway { Submit = (_, _) => Task.FromResult(new PaymentOutcome(PaymentStatus.Rejected, "Balance changed")) };
        await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        Assert.That((await Flow(gateway).RetryAsync()).Outcome.Status, Is.EqualTo(PaymentStatus.Rejected));
        Assert.That(gateway.Sent, Has.Count.EqualTo(1));
        await Flow(gateway).AcknowledgeAsync();
        Assert.That(File.Exists(Journal.PathName), Is.False);
    }
    [Test]
    public async Task UnavailableStatusDoesNotErasePendingOrAllowNewIntent()
    {
        var gateway = new Gateway { Submit = (_, _) => throw new IOException("Connection lost") };
        await Assert.ThrowsAsync<IOException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        byte[] before = File.ReadAllBytes(Journal.PathName); gateway.InspectionUnavailable = true;
        await Assert.ThrowsAsync<IOException>(async () => await Flow(gateway).AcknowledgeAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.Joker, 22222222222L, 20m));
        Assert.That(File.ReadAllBytes(Journal.PathName), Is.EqualTo(before)); Assert.That(gateway.Prepared, Is.EqualTo(1));
    }
    [Test]
    public async Task CorruptPendingFileFailsClosedAndIsNeverOverwritten()
    {
        var gateway = new Gateway(); await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        byte[] invalid = [1, 2, 3, 4]; File.WriteAllBytes(Journal.PathName, invalid);
        await Assert.ThrowsAsync<CryptographicException>(async () => await Flow(gateway).InspectAsync());
        await Assert.ThrowsAsync<CryptographicException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        Assert.That(gateway.Sent, Has.Count.EqualTo(1)); Assert.That(File.ReadAllBytes(Journal.PathName), Is.EqualTo(invalid));
    }
    [Test]
    public async Task ExclusiveJournalLeasePreventsTwoSimultaneousBegins()
    {
        var gateway = new Gateway();
        var waiting = new TaskCompletionSource<PaymentOutcome>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.Submit = (_, _) => waiting.Task;
        var first = Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
            Assert.That(gateway.Prepared, Is.EqualTo(1));
        }
        finally { waiting.SetResult(new(PaymentStatus.Recorded)); await first; }
    }
    [Test]
    public async Task JournalIsEncryptedAndIndependentOfConnectionTimeoutOrApplicationUser()
    {
        var gateway = new Gateway(); var state = await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m);
        string bytes = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(Journal.PathName));
        Assert.That(bytes, Does.Not.Contain("11111111111")); Assert.That(bytes, Does.Not.Contain(state.Request.RequestId.ToString()));
        var sameDatabase = new PaymentJournal(Connection + ";Connect Timeout=45", directory);
        Assert.That(sameDatabase.PathName, Is.EqualTo(Journal.PathName));
        var otherUser = new PaymentWorkflow(sameDatabase, gateway, new("another-operator", UserRole.Admin));
        Assert.That((await otherUser.InspectAsync())!.Request, Is.EqualTo(state.Request));
        Assert.That(new PaymentJournal(Connection.Replace("Database=Recovery", "Database=Other"), directory).PathName, Is.Not.EqualTo(Journal.PathName));
    }
    [Test]
    public async Task FailureToPersistPreventsAnyPaymentSubmission()
    {
        var gateway = new Gateway(); Directory.CreateDirectory(Journal.PathName);
        await Assert.CatchAsync<Exception>(async () => await Flow(gateway).BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        Assert.That(gateway.Sent, Is.Empty);
    }
    [Test]
    public async Task ViewerCannotCreateRetryOrAcknowledgePayment()
    {
        var gateway = new Gateway(); var viewer = new PaymentWorkflow(Journal, gateway, new("viewer", UserRole.Viewer));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await viewer.BeginAsync(PersonnelGroup.General, 11111111111L, 30m));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await viewer.RetryAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await viewer.AcknowledgeAsync());
        Assert.That(gateway.Prepared, Is.Zero);
    }
}
