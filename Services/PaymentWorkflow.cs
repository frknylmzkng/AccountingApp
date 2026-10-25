using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal enum PaymentStatus { Unconfirmed, Recorded, Reversed, Rejected }
internal sealed record PaymentRequest(Guid RequestId, PersonnelGroup Group, long Tc, decimal Amount, long ClosureId)
{
    internal void Validate()
    {
        if (RequestId == Guid.Empty || !Enum.IsDefined(Group) || Tc < 10000000000L || Tc > 99999999999L ||
            Amount <= 0 || Amount > 9999999999999999.99m || decimal.Round(Amount, 2) != Amount || ClosureId < 0)
            throw new InvalidOperationException("Bekleyen ödeme bilgisi geçersiz. Yeni ödeme başlatılmadı.");
    }
}
internal sealed record PaymentOutcome(PaymentStatus Status, string? Reason = null);
internal sealed record PaymentState(PaymentRequest Request, PaymentOutcome Outcome);
internal sealed record PaymentJournalEntry(PaymentRequest Request, PaymentOutcome? Rejection = null);

internal interface IPaymentGateway
{
    Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, CancellationToken token);
    Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token);
    Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token);
}

// One unresolved intent per Windows user/database, shared across screens and application instances.
// A missing/corrupt/unreadable journal must never be silently replaced with a new intent.
internal sealed class PaymentJournal
{
    private readonly string scope;
    internal string PathName { get; }
    internal PaymentJournal(string connectionString, string? directory = null)
    {
        var b = new SqlConnectionStringBuilder(connectionString);
        string file = string.IsNullOrWhiteSpace(b.AttachDBFilename) ? "" : Path.GetFullPath(b.AttachDBFilename).ToUpperInvariant();
        scope = b.DataSource.ToUpperInvariant() + "|" + b.InitialCatalog.ToUpperInvariant() + "|" + file;
        string name = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)));
        PathName = Path.Combine(directory ?? Path.Combine(UserPreferences.DirectoryPath, "payments"), name + ".pending");
    }
    internal Lease Acquire()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        try { return new(this, new FileStream(PathName + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)); }
        catch (IOException ex) { throw new InvalidOperationException("Ödeme durumu şu anda açılamıyor. Başka penceredeki işlemin tamamlanmasını bekleyip yeniden deneyin.", ex); }
    }
    internal sealed class Lease(PaymentJournal owner, FileStream gate) : IDisposable
    {
        internal PaymentJournalEntry? Read()
        {
            PaymentJournalEntry? entry;
            try { entry = DraftStore.Load<PaymentJournalEntry>(owner.PathName, owner.scope); }
            catch (FileNotFoundException) { return null; }
            if (entry?.Request == null || entry.Rejection is { Status: not PaymentStatus.Rejected })
                throw new InvalidOperationException("Bekleyen ödeme bilgisi okunamadı. Yeni ödeme başlatılmadı.");
            entry.Request.Validate();
            return entry;
        }
        internal void Write(PaymentJournalEntry entry) => DraftStore.Save(owner.PathName, owner.scope, entry, durable: true);
        internal void Clear() => File.Delete(owner.PathName);
        public void Dispose() => gate.Dispose();
    }
}

internal sealed class PaymentWorkflow(PaymentJournal journal, IPaymentGateway gateway, UserSession user)
{
    internal PaymentWorkflow(string connectionString, UserSession user)
        : this(new PaymentJournal(connectionString), new PersonnelService(connectionString, user), user) { }

    internal async Task<PaymentState?> InspectAsync(CancellationToken token = default)
    {
        using var lease = journal.Acquire();
        var entry = lease.Read();
        return entry == null ? null : await InspectAsync(entry, token).ConfigureAwait(false);
    }
    private async Task<PaymentState> InspectAsync(PaymentJournalEntry entry, CancellationToken token)
    {
        // Always consult the gateway, including cached rejections: an old session
        // must not clear a pending intent without current account authorization.
        var actual = await gateway.InspectPaymentAsync(entry.Request, token).ConfigureAwait(false);
        return new(entry.Request, actual.Status == PaymentStatus.Unconfirmed && entry.Rejection != null ? entry.Rejection : actual);
    }

    internal async Task<PaymentState> BeginAsync(PersonnelGroup group, long tc, decimal amount, CancellationToken token = default)
    {
        user.DemandEdit();
        using var lease = journal.Acquire();
        if (lease.Read() != null) throw new InvalidOperationException("Önce önceki ödemenin sonucunu kontrol edip onaylayın.");
        var request = await gateway.PreparePaymentAsync(group, tc, amount, token).ConfigureAwait(false);
        request.Validate();
        var entry = new PaymentJournalEntry(request);
        lease.Write(entry); // Must reach disk before any balance-changing command is sent.
        return await SubmitAsync(lease, entry, token).ConfigureAwait(false);
    }
    internal async Task<PaymentState> RetryAsync(CancellationToken token = default)
    {
        user.DemandEdit();
        using var lease = journal.Acquire();
        var entry = lease.Read() ?? throw new InvalidOperationException("Bekleyen ödeme bulunamadı. Listeyi yenileyin.");
        var state = await InspectAsync(entry, token).ConfigureAwait(false);
        return state.Outcome.Status == PaymentStatus.Unconfirmed
            ? await SubmitAsync(lease, entry, token).ConfigureAwait(false) : state;
    }
    private async Task<PaymentState> SubmitAsync(PaymentJournal.Lease lease, PaymentJournalEntry entry, CancellationToken token)
    {
        var outcome = await gateway.SubmitPaymentAsync(entry.Request, token).ConfigureAwait(false);
        if (outcome.Status == PaymentStatus.Rejected) lease.Write(entry with { Rejection = outcome });
        // Keep even successful requests until the user explicitly acknowledges the result.
        return new(entry.Request, outcome);
    }
    internal async Task AcknowledgeAsync(CancellationToken token = default)
    {
        user.DemandEdit();
        using var lease = journal.Acquire();
        var entry = lease.Read();
        if (entry == null) return;
        var state = await InspectAsync(entry, token).ConfigureAwait(false);
        if (state.Outcome.Status == PaymentStatus.Unconfirmed)
            throw new InvalidOperationException("Ödeme sonucu henüz doğrulanamadı. Önce sonucu kontrol edin veya aynı ödemeyi yeniden deneyin.");
        lease.Clear();
    }
}
