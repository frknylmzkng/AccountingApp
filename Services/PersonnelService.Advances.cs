namespace WinFormsApp2;

internal sealed partial class PersonnelService
{
    public async Task<PaymentRequest> PrepareAdvanceAsync(PersonnelGroup group, long tc, decimal amount, byte[] expectedVersion, Guid projectId, CancellationToken token)
    {
        if (DesktopConnection.IsRemote(connectionString)) return RemoteApi.Payment(await RemoteApi.For(connectionString, user).SendAsync<AccountingApp.Contracts.PaymentIntent>("payments/prepare", new AccountingApp.Contracts.PaymentPrepare((AccountingApp.Contracts.PersonnelGroup)group, tc, amount, expectedVersion, AccountingApp.Contracts.PaymentPurpose.Advance, projectId, RemoteApi.Reason), token).ConfigureAwait(false));
        user.DemandPay();
        throw new InvalidOperationException("Proje avansı için projenin Ödeme hazırlama / onay ekranını kullanın. Hazırlayan ile onaylayan farklı olmalıdır.");
    }
}
