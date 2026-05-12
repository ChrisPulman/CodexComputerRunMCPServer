namespace CodexComputerRunMCPServer;

/// <summary>
/// Owns a process-wide lock file that prevents concurrent computer run MCP server instances.
/// </summary>
internal sealed class SingleInstanceGuard : IDisposable
{
    private readonly FileStream? _lockFile;

    private SingleInstanceGuard(string lockFilePath, FileStream? lockFile, bool isEnabled)
    {
        LockFilePath = lockFilePath;
        _lockFile = lockFile;
        IsEnabled = isEnabled;
    }

    /// <summary>
    /// Gets the exit code used when startup is rejected because another instance is running.
    /// </summary>
    public const int ConcurrentInstanceExitCode = 2;

    /// <summary>
    /// Gets the process-wide lock file path used by default.
    /// </summary>
    public static string DefaultLockFilePath { get; } = CreateDefaultLockFilePath();

    /// <summary>
    /// Gets a value indicating whether single-instance enforcement is enabled.
    /// </summary>
    public bool IsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether this process owns the single-instance lock.
    /// </summary>
    public bool HasOwnership => !IsEnabled || _lockFile is not null;

    /// <summary>
    /// Gets the lock file path used by this guard.
    /// </summary>
    public string LockFilePath { get; }

    /// <summary>
    /// Attempts to acquire the single-instance guard.
    /// </summary>
    /// <param name="enabled">Whether single-instance enforcement should be active.</param>
    /// <param name="lockFilePath">Optional lock file path used by tests.</param>
    /// <returns>A guard describing whether this process owns the lock file.</returns>
    public static SingleInstanceGuard TryAcquire(bool enabled, string? lockFilePath = null)
    {
        var resolvedLockFilePath = Path.GetFullPath(
            string.IsNullOrWhiteSpace(lockFilePath) ? DefaultLockFilePath : lockFilePath);

        if (!enabled)
        {
            return new SingleInstanceGuard(resolvedLockFilePath, lockFile: null, isEnabled: false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(resolvedLockFilePath)!);

        try
        {
            var lockFile = new FileStream(
                resolvedLockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            WriteOwnerMetadata(lockFile);
            return new SingleInstanceGuard(resolvedLockFilePath, lockFile, isEnabled: true);
        }
        catch (IOException)
        {
            return new SingleInstanceGuard(resolvedLockFilePath, lockFile: null, isEnabled: true);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lockFile?.Dispose();

        if (_lockFile is not null)
        {
            try
            {
                File.Delete(LockFilePath);
            }
            catch (IOException)
            {
                // The process is already shutting down; a stale unlocked file is harmless.
            }
            catch (UnauthorizedAccessException)
            {
                // The process is already shutting down; a stale unlocked file is harmless.
            }
        }
    }

    private static void WriteOwnerMetadata(FileStream lockFile)
    {
        var metadata = $"pid={Environment.ProcessId}{Environment.NewLine}startedUtc={DateTimeOffset.UtcNow:O}";
        using var writer = new StreamWriter(lockFile, leaveOpen: true);
        lockFile.SetLength(0);
        writer.Write(metadata);
        writer.Flush();
        lockFile.Flush(flushToDisk: true);
    }

    private static string CreateDefaultLockFilePath()
    {
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = string.IsNullOrWhiteSpace(localApplicationData)
            ? Path.GetTempPath()
            : localApplicationData;

        return Path.Combine(root, "CodexComputerRunMCPServer", "server.lock");
    }
}
