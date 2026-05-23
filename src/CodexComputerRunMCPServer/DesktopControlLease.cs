namespace CodexComputerRunMCPServer;

/// <summary>
/// Coordinates exclusive desktop-control access across concurrently running MCP server processes.
/// </summary>
internal sealed class DesktopControlLease : IDisposable
{
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;
    private readonly Guid _ownerId = Guid.NewGuid();
    private FileStream? _lockFile;
    private Timer? _releaseTimer;
    private DateTimeOffset _leaseExpiresUtc;
    private int _activeControlInvocations;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DesktopControlLease"/> class.
    /// </summary>
    /// <param name="enabled">Whether cross-process desktop-control coordination should be active.</param>
    /// <param name="leaseDuration">How long this process keeps control after its latest control action.</param>
    /// <param name="lockFilePath">Optional lock path used by tests.</param>
    /// <param name="timeProvider">Optional time provider used by tests.</param>
    public DesktopControlLease(
        bool enabled,
        TimeSpan leaseDuration,
        string? lockFilePath = null,
        TimeProvider? timeProvider = null)
    {
        IsEnabled = enabled;
        LeaseDuration = leaseDuration < TimeSpan.Zero ? TimeSpan.Zero : leaseDuration;
        LockFilePath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(lockFilePath) ? DefaultLockFilePath : lockFilePath);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Gets the process-wide desktop-control lease file path used by default.
    /// </summary>
    public static string DefaultLockFilePath { get; } = CreateDefaultLockFilePath();

    /// <summary>
    /// Gets a value indicating whether cross-process desktop-control locking is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets the idle lease duration after the latest desktop-control action.
    /// </summary>
    public TimeSpan LeaseDuration { get; }

    /// <summary>
    /// Gets the lease file path used by this instance.
    /// </summary>
    public string LockFilePath { get; }

    /// <summary>
    /// Creates a lease from configured lifecycle options.
    /// </summary>
    /// <param name="options">The resolved lifecycle options.</param>
    /// <returns>A configured <see cref="DesktopControlLease"/> instance.</returns>
    public static DesktopControlLease FromOptions(ComputerRunLifecycleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new DesktopControlLease(options.ControlLockEnabled, options.ControlLeaseDuration);
    }

    /// <summary>
    /// Begins a desktop-control invocation, acquiring or renewing the process lease.
    /// </summary>
    /// <returns>A scope that must be disposed when the control action completes.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when another server process or concurrent invocation owns desktop control.
    /// </exception>
    public IDisposable BeginControlInvocation()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!IsEnabled)
            {
                _activeControlInvocations++;
                return new ControlInvocationScope(this);
            }

            if (_activeControlInvocations > 0)
            {
                throw CreateBusyException("Another desktop-control operation is already running in this server process.");
            }

            if (_lockFile is null && !TryAcquireLockFile())
            {
                throw CreateBusyException(
                    "Another Codex Computer Run MCP session currently owns desktop control.");
            }

            _activeControlInvocations = 1;
            RenewLease();
            _releaseTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            return new ControlInvocationScope(this);
        }
    }

    /// <summary>
    /// Releases the lock immediately when the configured lease has expired and no action is running.
    /// </summary>
    /// <returns><see langword="true"/> when a held lease was released.</returns>
    internal bool ReleaseExpiredIdleLease()
    {
        lock (_sync)
        {
            return ReleaseExpiredIdleLeaseCore(_timeProvider.GetUtcNow());
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _releaseTimer?.Dispose();
            _releaseTimer = null;
            ReleaseLockFile();
        }
    }

    private void EndControlInvocation()
    {
        lock (_sync)
        {
            if (_activeControlInvocations > 0)
            {
                _activeControlInvocations--;
            }

            if (_activeControlInvocations == 0)
            {
                ScheduleIdleRelease();
            }
        }
    }

    private bool TryAcquireLockFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LockFilePath)!);

        try
        {
            _lockFile = new FileStream(
                LockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            return true;
        }
        catch (IOException)
        {
            _lockFile = null;
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            _lockFile = null;
            return false;
        }
    }

    private void RenewLease()
    {
        _leaseExpiresUtc = _timeProvider.GetUtcNow() + LeaseDuration;
        WriteOwnerMetadata();
    }

    private void ScheduleIdleRelease()
    {
        if (!IsEnabled || _lockFile is null)
        {
            return;
        }

        if (LeaseDuration <= TimeSpan.Zero)
        {
            ReleaseLockFile();
            return;
        }

        _releaseTimer ??= new Timer(_ => ReleaseExpiredIdleLease(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _releaseTimer.Change(LeaseDuration, Timeout.InfiniteTimeSpan);
    }

    private bool ReleaseExpiredIdleLeaseCore(DateTimeOffset nowUtc)
    {
        if (_activeControlInvocations > 0 || _lockFile is null)
        {
            return false;
        }

        if (nowUtc < _leaseExpiresUtc)
        {
            _releaseTimer?.Change(_leaseExpiresUtc - nowUtc, Timeout.InfiniteTimeSpan);
            return false;
        }

        ReleaseLockFile();
        return true;
    }

    private void ReleaseLockFile()
    {
        var lockFile = _lockFile;
        _lockFile = null;
        lockFile?.Dispose();

        if (lockFile is null)
        {
            return;
        }

        try
        {
            File.Delete(LockFilePath);
        }
        catch (IOException)
        {
            // A stale unlocked file is harmless; the exclusive file handle is the coordination primitive.
        }
        catch (UnauthorizedAccessException)
        {
            // A stale unlocked file is harmless; the exclusive file handle is the coordination primitive.
        }
    }

    private void WriteOwnerMetadata()
    {
        if (_lockFile is null)
        {
            return;
        }

        var metadata = string.Join(
            Environment.NewLine,
            $"ownerId={_ownerId:N}",
            $"pid={Environment.ProcessId}",
            $"leaseExpiresUtc={_leaseExpiresUtc:O}",
            $"leaseSeconds={LeaseDuration.TotalSeconds}");

        using var writer = new StreamWriter(_lockFile, leaveOpen: true);
        _lockFile.SetLength(0);
        writer.Write(metadata);
        writer.Flush();
        _lockFile.Flush(flushToDisk: true);
    }

    private InvalidOperationException CreateBusyException(string reason)
        => new(
            $"{reason} Try again after the active control action completes or after " +
            $"{LeaseDuration.TotalSeconds:0.###} second(s) without control input.");

    private static string CreateDefaultLockFilePath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.GetTempPath()
            : localApplicationData;

        return Path.Combine(root, "CodexComputerRunMCPServer", "control.lock");
    }

    private sealed class ControlInvocationScope(DesktopControlLease owner) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                owner.EndControlInvocation();
            }
        }
    }
}
