namespace CodexComputerRunMCPServer;

/// <summary>
/// Tracks recent MCP tool activity so the server can shut itself down after it becomes idle.
/// </summary>
internal sealed class ComputerRunActivityTracker
{
    private readonly TimeProvider _timeProvider;
    private long _lastActivityUtcTicks;
    private int _activeInvocations;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComputerRunActivityTracker"/> class.
    /// </summary>
    /// <param name="timeProvider">Optional time provider used by tests.</param>
    public ComputerRunActivityTracker(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        Touch();
    }

    /// <summary>
    /// Records the start of an MCP tool invocation.
    /// </summary>
    /// <returns>An invocation scope that records completion when disposed.</returns>
    public IDisposable BeginInvocation()
    {
        _ = Interlocked.Increment(ref _activeInvocations);
        Touch();
        return new InvocationScope(this);
    }

    /// <summary>
    /// Gets an immutable snapshot of current tool activity.
    /// </summary>
    /// <returns>The current activity state.</returns>
    public ComputerRunActivitySnapshot GetSnapshot()
    {
        var ticks = Interlocked.Read(ref _lastActivityUtcTicks);
        var activeInvocations = Volatile.Read(ref _activeInvocations);
        return new ComputerRunActivitySnapshot(new DateTimeOffset(ticks, TimeSpan.Zero), activeInvocations);
    }

    private void EndInvocation()
    {
        Touch();
        _ = Interlocked.Decrement(ref _activeInvocations);
    }

    private void Touch()
    {
        var timestamp = _timeProvider.GetUtcNow().UtcTicks;
        Interlocked.Exchange(ref _lastActivityUtcTicks, timestamp);
    }

    private sealed class InvocationScope(ComputerRunActivityTracker owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.EndInvocation();
            }
        }
    }
}

/// <summary>
/// Represents an immutable snapshot of MCP tool activity.
/// </summary>
/// <param name="LastActivityUtc">The latest observed tool start or completion time.</param>
/// <param name="ActiveInvocations">The number of tool calls currently running.</param>
internal sealed record ComputerRunActivitySnapshot(DateTimeOffset LastActivityUtc, int ActiveInvocations)
{
    /// <summary>
    /// Calculates how long the server has been idle at the specified time.
    /// </summary>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns>The elapsed time since the last tool activity.</returns>
    public TimeSpan GetIdleDuration(DateTimeOffset nowUtc) => nowUtc - LastActivityUtc;
}
