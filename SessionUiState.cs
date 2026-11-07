namespace WinFormsApp2;

// UI lock is separate from database sessions: committed work must finish and
// the original session must be revalidated before any waiting UI resumes.
internal static class SessionUiState
{
    private static TaskCompletionSource? unlocked;
    private static int operations;
    internal static bool IsLocked => unlocked != null;
    internal static Func<bool>? InteractionCheck;
    internal static bool CanInteract => !IsLocked && (InteractionCheck?.Invoke() ?? true);
    internal static int ActiveOperations => Volatile.Read(ref operations);
    internal static void Lock() => unlocked ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal static void Release()
    {
        var signal = unlocked; unlocked = null; signal?.TrySetResult();
    }
    internal static Task WaitAsync(CancellationToken token) => unlocked?.Task.WaitAsync(token) ?? Task.CompletedTask;
    internal static IDisposable Operation()
    { Interlocked.Increment(ref operations); return new OperationScope(); }
    private sealed class OperationScope : IDisposable
    { public void Dispose() => Interlocked.Decrement(ref operations); }
}
