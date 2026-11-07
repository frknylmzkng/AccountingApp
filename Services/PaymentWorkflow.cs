using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace WinFormsApp2;

internal enum PaymentStatus { Unconfirmed, Recorded, Reversed, Rejected }
internal enum PaymentPurpose { Settlement, Advance }
internal sealed record PaymentRequest(Guid RequestId, PersonnelGroup Group, long Tc, decimal Amount, long ClosureId)
{
    // Missing fields identify the original on-disk format. Such requests can be
    // inspected, but cannot create a new payment without a selected row version.
    public int FormatVersion { get; init; }
    public byte[]? PersonVersion { get; init; }
    public Guid? PersonId { get; init; }
    public string? AuditReason { get; init; }
    public PaymentPurpose Purpose { get; init; }
    public Guid? ProjectId { get; init; }
    internal void Validate()
    {
        if (AuditReason != null) _ = AuditContext.ValidateReason(AuditReason);
        if (!Enum.IsDefined(Purpose) || ProjectId == Guid.Empty || (Purpose == PaymentPurpose.Advance && !ProjectId.HasValue))
            throw new InvalidOperationException("Ödeme türü veya avans projesi geçersiz.");
        if (RequestId == Guid.Empty || !Enum.IsDefined(Group) || Tc < 10000000000L || Tc > 99999999999L ||
            Amount <= 0 || Amount > 9999999999999999.99m || decimal.Round(Amount, 2) != Amount || ClosureId < 0)
            throw new InvalidOperationException("Bekleyen ödeme bilgisi geçersiz. Yeni ödeme başlatılmadı.");
        if ((FormatVersion == 0 && (PersonVersion != null || PersonId != null)) ||
            (FormatVersion is 2 or 3 && PersonVersion is not { Length: 8 }) ||
            (FormatVersion == 3 && (!PersonId.HasValue || PersonId == Guid.Empty)) ||
            (PersonId.HasValue && PersonId == Guid.Empty) || FormatVersion is not (0 or 2 or 3))
            throw new InvalidOperationException("Bekleyen ödemenin sürüm bilgisi geçersiz. Yeni ödeme başlatılmadı.");
    }
    internal PaymentRequest Snapshot() { Validate(); return this with { PersonVersion = PersonVersion?.ToArray() }; }
}
internal sealed record PaymentOutcome(PaymentStatus Status, string? Reason = null);
internal sealed record PaymentState(PaymentRequest Request, PaymentOutcome Outcome);
internal sealed record PaymentJournalEntry(PaymentRequest Request, PaymentOutcome? Rejection = null);

internal interface IPaymentGateway
{
    Task<PaymentRequest> PreparePaymentAsync(PersonnelGroup group, long tc, decimal amount, byte[] expectedVersion, CancellationToken token);
    Task<PaymentOutcome> InspectPaymentAsync(PaymentRequest request, CancellationToken token);
    Task<PaymentOutcome> SubmitPaymentAsync(PaymentRequest request, CancellationToken token);
}
internal interface IAdvancePaymentGateway
{
    Task<PaymentRequest> PrepareAdvanceAsync(PersonnelGroup group, long tc, decimal amount, byte[] expectedVersion, Guid projectId, CancellationToken token);
}

// One unresolved intent per Windows user/database, shared across screens and application instances.
// A missing/corrupt/unreadable journal must never be silently replaced with a new intent.
internal sealed class PaymentJournal
{
    private readonly string scope;
    internal string PathName { get; }
    internal PaymentJournal(string connectionString, string? directory = null)
    {
        if (connectionString.StartsWith("api-scope:", StringComparison.Ordinal)) scope = connectionString;
        else
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            string file = string.IsNullOrWhiteSpace(b.AttachDBFilename) ? "" : Path.GetFullPath(b.AttachDBFilename).ToUpperInvariant();
            scope = b.DataSource.ToUpperInvariant() + "|" + b.InitialCatalog.ToUpperInvariant() + "|" + file;
        }
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
        : this(new PaymentJournal(DesktopConnection.IsRemote(connectionString) ? "api-scope:" + RemoteApi.For(connectionString, user).Scope : connectionString), new PersonnelService(connectionString, user), user) { }

    internal async Task<PaymentState?> InspectAsync(CancellationToken token = default)
    {
        user.DemandViewSalary();
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

    internal async Task<PaymentState> BeginAsync(PersonnelGroup group, long tc, decimal amount, byte[] expectedVersion, CancellationToken token = default,
        PaymentPurpose purpose = PaymentPurpose.Settlement, Guid? projectId = null)
    {
        user.DemandPay();
        var selected = PaymentTarget.RequireSnapshot(new(group, tc, expectedVersion));
        using var lease = journal.Acquire();
        if (lease.Read() != null) throw new InvalidOperationException("Önce önceki ödemenin sonucunu kontrol edip onaylayın.");
        if (!Enum.IsDefined(purpose) || (purpose == PaymentPurpose.Advance && (!projectId.HasValue || projectId == Guid.Empty)))
            throw new ArgumentException("Avans için proje seçin.");
        var prepared = purpose == PaymentPurpose.Advance
            ? await (gateway as IAdvancePaymentGateway ?? throw new InvalidOperationException("Avans servisi kullanılamıyor."))
                .PrepareAdvanceAsync(selected.Group, selected.Tc, amount, selected.PersonVersion.ToArray(), projectId!.Value, token).ConfigureAwait(false)
            : await gateway.PreparePaymentAsync(selected.Group, selected.Tc, amount, selected.PersonVersion.ToArray(), token).ConfigureAwait(false);
        var request = prepared.Snapshot() with { AuditReason = AuditContext.Reason };
        if (request.Purpose != purpose || (purpose == PaymentPurpose.Advance && request.ProjectId != projectId))
            throw new InvalidOperationException("Ödeme türü/proje eşleşmedi; ödeme gönderilmedi.");
        if (request.FormatVersion != 3 || request.Group != selected.Group || request.Tc != selected.Tc || request.Amount != amount ||
            !request.PersonVersion!.SequenceEqual(selected.PersonVersion)) throw new InvalidOperationException(PaymentTarget.SelectionMessage);
        var entry = new PaymentJournalEntry(request);
        lease.Write(entry); // Must reach disk before any balance-changing command is sent.
        return await SubmitAsync(lease, entry, token).ConfigureAwait(false);
    }
    internal async Task<PaymentState> RetryAsync(CancellationToken token = default)
    {
        user.DemandPay();
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
        user.DemandPay();
        using var lease = journal.Acquire();
        var entry = lease.Read();
        if (entry == null) return;
        var state = await InspectAsync(entry, token).ConfigureAwait(false);
        if (state.Outcome.Status == PaymentStatus.Unconfirmed)
            throw new InvalidOperationException("Ödeme sonucu henüz doğrulanamadı. Önce sonucu kontrol edin veya aynı ödemeyi yeniden deneyin.");
        lease.Clear();
    }
}
