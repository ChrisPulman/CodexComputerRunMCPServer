namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides a thread-safe runtime container for the <see cref="IComputerRunService"/> instance
/// used by computer run tools. Supports replacing the service during testing.
/// </summary>
internal static class ComputerRunToolRuntime
{
    private static IComputerRunService _service = ComputerRunService.CreateDefault();

    /// <summary>
    /// Gets the current <see cref="IComputerRunService"/> instance using a volatile read
    /// to ensure visibility across threads.
    /// </summary>
    public static IComputerRunService Service => Volatile.Read(ref _service);

    /// <summary>
    /// Replaces the current <see cref="IComputerRunService"/> with the specified instance
    /// for the duration of a test, restoring the previous service when the returned
    /// <see cref="IDisposable"/> is disposed.
    /// </summary>
    /// <param name="service">The test <see cref="IComputerRunService"/> to use.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that, when disposed, restores the previous
    /// <see cref="IComputerRunService"/> instance.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> is <see langword="null"/>.</exception>
    internal static IDisposable ReplaceServiceForTests(IComputerRunService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        var previous = Interlocked.Exchange(ref _service, service);
        return new RestoreService(previous);
    }

    /// <summary>
    /// Restores a previously held <see cref="IComputerRunService"/> instance when disposed.
    /// </summary>
    private sealed class RestoreService(IComputerRunService previous) : IDisposable
    {
        /// <summary>
        /// Restores the previous <see cref="IComputerRunService"/> instance in a thread-safe manner.
        /// </summary>
        public void Dispose() => Interlocked.Exchange(ref _service, previous);
    }
}
