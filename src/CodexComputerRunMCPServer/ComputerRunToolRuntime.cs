namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides a thread-safe runtime container for the <see cref="IComputerRunService"/> instance
/// used by computer run tools. Supports replacing the service during testing.
/// </summary>
internal static class ComputerRunToolRuntime
{
    private static IComputerRunService _service = ComputerRunService.CreateDefault();
    private static ComputerRunActivityTracker _activityTracker = new();

    /// <summary>
    /// Gets the current <see cref="IComputerRunService"/> instance using a volatile read
    /// to ensure visibility across threads.
    /// </summary>
    public static IComputerRunService Service => Volatile.Read(ref _service);

    /// <summary>
    /// Gets the activity tracker used by MCP tool calls and lifecycle services.
    /// </summary>
    public static ComputerRunActivityTracker ActivityTracker => Volatile.Read(ref _activityTracker);

    /// <summary>
    /// Starts tracking a tool invocation until the returned scope is disposed.
    /// </summary>
    /// <returns>An invocation scope that must be disposed when the tool call completes.</returns>
    public static IDisposable BeginToolInvocation() => ActivityTracker.BeginInvocation();

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
    /// Replaces the current activity tracker for the duration of a test.
    /// </summary>
    /// <param name="activityTracker">The test activity tracker to use.</param>
    /// <returns>
    /// An <see cref="IDisposable"/> that, when disposed, restores the previous tracker.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="activityTracker"/> is <see langword="null"/>.
    /// </exception>
    internal static IDisposable ReplaceActivityTrackerForTests(ComputerRunActivityTracker activityTracker)
    {
        ArgumentNullException.ThrowIfNull(activityTracker);
        var previous = Interlocked.Exchange(ref _activityTracker, activityTracker);
        return new RestoreActivityTracker(activityTracker, previous);
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

    /// <summary>
    /// Restores a previously held <see cref="ComputerRunActivityTracker"/> instance when disposed.
    /// </summary>
    private sealed class RestoreActivityTracker(
        ComputerRunActivityTracker current,
        ComputerRunActivityTracker previous) : IDisposable
    {
        /// <summary>
        /// Restores the previous <see cref="ComputerRunActivityTracker"/> instance in a thread-safe manner.
        /// </summary>
        public void Dispose()
        {
            _ = Interlocked.CompareExchange(ref _activityTracker, previous, current);
        }
    }
}
