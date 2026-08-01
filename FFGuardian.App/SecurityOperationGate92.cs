namespace FFGuardian;

internal static class SecurityOperationGate92
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static bool IsBusy => Gate.CurrentCount == 0;

    public static async Task<IDisposable?> TryEnterAsync()
    {
        bool entered = await Gate.WaitAsync(0).ConfigureAwait(true);
        return entered ? new Releaser() : null;
    }

    private sealed class Releaser : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Gate.Release();
        }
    }
}
