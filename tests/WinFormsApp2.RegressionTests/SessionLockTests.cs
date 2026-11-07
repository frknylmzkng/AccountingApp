using NUnit.Framework;

namespace WinFormsApp2.RegressionTests;

[TestFixture, NonParallelizable, Apartment(ApartmentState.STA)]
public sealed class SessionLockUiStateTests
{
    [TearDown] public void Release() => SessionUiState.Release();
    [Test]
    public async Task UiWaitsForUnlockButDoesNotKeepACompletedOperationActive()
    {
        SessionUiState.Lock();
        using (SessionUiState.Operation()) Assert.That(SessionUiState.ActiveOperations, Is.EqualTo(1));
        Assert.That(SessionUiState.ActiveOperations, Is.Zero);
        var waiting = SessionUiState.WaitAsync(CancellationToken.None);
        Assert.That(waiting.IsCompleted, Is.False);
        SessionUiState.Release(); await waiting;
        Assert.That(SessionUiState.IsLocked, Is.False);
    }
    [Test]
    public async Task DisposedScreenCanCancelItsWaitWithoutUnlockingOtherScreens()
    {
        SessionUiState.Lock(); using var cancel = new CancellationTokenSource();
        var waiting = SessionUiState.WaitAsync(cancel.Token); cancel.Cancel();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waiting);
        Assert.That(SessionUiState.IsLocked, Is.True);
    }
    [Test]
    public void IdleBoundaryUsesElapsedActivityRatherThanDateOrRenderingTicks()
    {
        Assert.That(IdleSessionGuard.IsExpired(599999, 0), Is.False);
        Assert.That(IdleSessionGuard.IsExpired(600000, 0), Is.True);
        Assert.That(IdleSessionGuard.IsExpired(900000, 500000), Is.False);
    }
    [Test]
    public void SavingOneGroupDoesNotClearAnotherGroupsUnsavedInput()
    {
        using var owner = new SessionForm();
        using var first = new TextBox { Text = "Original" }; using var second = new TextBox { UseSystemPasswordChar = true };
        using var changes = new UnsavedChanges(owner); changes.Track("Card", first); changes.Track("Password", second);
        first.Text = "Edited"; second.Text = "Synthetic-secret"; changes.DraftLoaded();
        Assert.That(changes.HasChanges(), Is.True);
        changes.Accept("Card"); Assert.That(changes.HasChanges(), Is.True);
        second.Clear(); Assert.That(changes.HasChanges(), Is.False);
    }
}

public partial class GuideDatabaseTests
{
    [Test, Category("SessionLock")]
    public async Task UnlockReusesOriginalSessionAndUsesPersistentFailedAttemptLimit()
    {
        var accounts = new AccountService(connection);
        Assert.That(await accounts.UnlockSessionAsync(admin, "wrong-synthetic-password"), Is.False);
        Assert.That(await accounts.UnlockSessionAsync(admin, "Test-only-password-123"), Is.True);
        Assert.That(await AccountAuthorization.IsCurrentAsync(connection, admin, default), Is.True);
        Assert.That(Scalar("SELECT COUNT(*) FROM dbo.AuditLog WHERE Action=N'Session.Unlock'"), Is.EqualTo(1));
        var other = CreateActor("unlock-target", UserRole.Viewer);
        await new AccountService(connection).SetActiveAsync(admin, await TargetAsync(other.Username), false, default);
        await Assert.ThrowsAsync<SessionRevokedException>(() => accounts.UnlockSessionAsync(other, "Test-only-password-123"));
    }
}
