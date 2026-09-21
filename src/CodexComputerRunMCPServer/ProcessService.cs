using System.Diagnostics;
using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides bounded process observation and dry-run-first process/URL launching.
/// </summary>
internal static class ProcessService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaxWaitMilliseconds = 30_000;
    private const int MinPollMilliseconds = 25;
    private const int MaxPollMilliseconds = 1_000;

    public static string ListProcesses(string? processName, int limit)
    {
        if (limit is < 1 or > 1_000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be between 1 and 1000.");
        }

        var normalized = NormalizeProcessName(processName);
        var processes = normalized is null
            ? Process.GetProcesses()
            : Process.GetProcessesByName(normalized);
        try
        {
            var entries = processes
                .Take(limit)
                .Select(CreateProcessEntry)
                .ToArray();
            return JsonSerializer.Serialize(new { processName = normalized, entries }, JsonOptions);
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    public static string WaitForProcess(string? processName, int? processId, int timeoutMilliseconds, int pollMilliseconds)
    {
        if (string.IsNullOrWhiteSpace(processName) && processId is null)
        {
            throw new ArgumentException("Provide processName or processId.");
        }

        if (timeoutMilliseconds is < 0 or > MaxWaitMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), $"timeoutMilliseconds must be between 0 and {MaxWaitMilliseconds}.");
        }

        if (pollMilliseconds is < MinPollMilliseconds or > MaxPollMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), $"pollMilliseconds must be between {MinPollMilliseconds} and {MaxPollMilliseconds}.");
        }

        var normalized = NormalizeProcessName(processName);
        var stopwatch = Stopwatch.StartNew();
        while (true)
        {
            var match = FindProcess(normalized, processId);
            if (match is not null)
            {
                using (match)
                {
                    return JsonSerializer.Serialize(new
                    {
                        found = true,
                        waitedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                        process = CreateProcessEntry(match),
                    }, JsonOptions);
                }
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                return JsonSerializer.Serialize(new
                {
                    found = false,
                    waitedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                    process = (ProcessEntry?)null,
                }, JsonOptions);
            }

            var remaining = TimeSpan.FromMilliseconds(timeoutMilliseconds) - stopwatch.Elapsed;
            Thread.Sleep(remaining < TimeSpan.FromMilliseconds(pollMilliseconds)
                ? remaining
                : TimeSpan.FromMilliseconds(pollMilliseconds));
        }
    }

    public static string Launch(string executable, IReadOnlyList<string> arguments, string? workingDirectory, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            throw new ArgumentException("A non-empty executable is required.", nameof(executable));
        }

        var normalizedWorkingDirectory = ResolveWorkingDirectory(workingDirectory);
        if (!dryRun)
        {
            var startInfo = new ProcessStartInfo(executable.Trim())
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = normalizedWorkingDirectory ?? Environment.CurrentDirectory,
            };
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException($"Failed to start application: {executable}");
            return JsonSerializer.Serialize(new
            {
                operation = "launch_application",
                executable = executable.Trim(),
                arguments,
                workingDirectory = normalizedWorkingDirectory,
                dryRun,
                launched = true,
                processId = process.Id,
                processIdIsBestEffort = true,
                recommendation = "For GUI applications that reuse an existing process, use wait_for_window to identify the final window.",
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            operation = "launch_application",
            executable = executable.Trim(),
            arguments,
            workingDirectory = normalizedWorkingDirectory,
            dryRun,
            launched = false,
            processIdIsBestEffort = true,
            recommendation = "For GUI applications that reuse an existing process, use wait_for_window to identify the final window.",
        }, JsonOptions);
    }

    public static string OpenUrl(string url, bool dryRun)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new ArgumentException("Only absolute http and https URLs are supported.", nameof(url));
        }

        if (!dryRun)
        {
            using var process = Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            return JsonSerializer.Serialize(new
            {
                operation = "open_url",
                url = uri.AbsoluteUri,
                dryRun,
                opened = process is not null,
                processId = process?.Id,
                processIdIsBestEffort = true,
            }, JsonOptions);
        }

        return JsonSerializer.Serialize(new
        {
            operation = "open_url",
            url = uri.AbsoluteUri,
            dryRun,
            opened = false,
            processIdIsBestEffort = true,
        }, JsonOptions);
    }

    private static Process? FindProcess(string? processName, int? processId)
    {
        if (processId is not null)
        {
            try
            {
                var process = Process.GetProcessById(processId.Value);
                if (processName is not null
                    && !string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return DisposeAndReturnNull(process);
                }

                return KeepIfRunning(process);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        foreach (var process in Process.GetProcessesByName(processName!))
        {
            var running = KeepIfRunning(process);
            if (running is not null)
            {
                return running;
            }
        }

        return null;
    }

    private static Process? KeepIfRunning(Process process)
    {
        try
        {
            return process.HasExited ? DisposeAndReturnNull(process) : process;
        }
        catch (InvalidOperationException)
        {
            return DisposeAndReturnNull(process);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return DisposeAndReturnNull(process);
        }
    }

    private static Process? DisposeAndReturnNull(Process process)
    {
        process.Dispose();
        return null;
    }

    private static ProcessEntry CreateProcessEntry(Process process)
    {
        string? title = null;
        long? windowHandle = null;
        bool? hasExited = null;
        try
        {
            hasExited = process.HasExited;
            title = string.IsNullOrWhiteSpace(process.MainWindowTitle) ? null : process.MainWindowTitle;
            windowHandle = process.MainWindowHandle == IntPtr.Zero ? null : process.MainWindowHandle.ToInt64();
        }
        catch (InvalidOperationException)
        {
            // The process can exit between enumeration and metadata access.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Access to another process can be denied; identity remains useful.
        }

        return new ProcessEntry(process.Id, process.ProcessName, title, windowHandle, hasExited);
    }

    private static string? NormalizeProcessName(string? processName)
    {
        var normalized = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
        return normalized?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true
            ? normalized[..^4]
            : normalized;
    }

    private static string? ResolveWorkingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var resolved = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException($"Working directory not found: {resolved}");
        }

        return resolved;
    }
}

internal sealed record ProcessEntry(
    int ProcessId,
    string ProcessName,
    string? MainWindowTitle,
    long? MainWindowHandle,
    bool? HasExited);
