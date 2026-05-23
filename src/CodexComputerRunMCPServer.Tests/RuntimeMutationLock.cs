namespace CodexComputerRunMCPServer.Tests;

internal static class RuntimeMutationLock
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public static async Task<IDisposable> AcquireAsync()
    {
        await Semaphore.WaitAsync().ConfigureAwait(false);
        return new Releaser();
    }

    private sealed class Releaser : IDisposable
    {
        public void Dispose() => Semaphore.Release();
    }
}
