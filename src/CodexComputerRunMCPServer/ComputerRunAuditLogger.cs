using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Controls optional JSONL action auditing without exposing text payloads or screenshot bytes.
/// </summary>
internal sealed record ComputerRunAuditOptions(bool Enabled, string Path)
{
    public static ComputerRunAuditOptions FromEnvironment()
    {
        var enabled = ReadBoolean(Environment.GetEnvironmentVariable("CODEX_COMPUTER_RUN_AUDIT"));
        var path = Environment.GetEnvironmentVariable("CODEX_COMPUTER_RUN_AUDIT_PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = string.IsNullOrWhiteSpace(localAppData) ? System.IO.Path.GetTempPath() : localAppData;
            path = System.IO.Path.Combine(root, "CodexComputerRunMCPServer", "actions.jsonl");
        }

        return new ComputerRunAuditOptions(enabled, System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path)));
    }

    private static bool ReadBoolean(string? value)
        => value?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "on";
}

/// <summary>
/// Writes one structured record per tool invocation when explicitly enabled.
/// </summary>
internal static class ComputerRunAuditLogger
{
    private static readonly object FileGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static TResult Execute<TResult>(
        string tool,
        object? arguments,
        Func<TResult> action,
        ComputerRunAuditOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentNullException.ThrowIfNull(action);

        var resolvedOptions = options ?? ComputerRunAuditOptions.FromEnvironment();
        if (!resolvedOptions.Enabled)
        {
            return action();
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = action();
            Write(resolvedOptions, tool, arguments, stopwatch.Elapsed, error: null);
            return result;
        }
        catch (Exception exception)
        {
            Write(resolvedOptions, tool, arguments, stopwatch.Elapsed, exception);
            throw;
        }
    }

    private static void Write(
        ComputerRunAuditOptions options,
        string tool,
        object? arguments,
        TimeSpan duration,
        Exception? error)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(options.Path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var entry = new
            {
                timestampUtc = DateTimeOffset.UtcNow,
                actionId = Guid.NewGuid(),
                tool,
                arguments,
                durationMs = Math.Round(duration.TotalMilliseconds, 3),
                processId = Environment.ProcessId,
                success = error is null,
                error = error is null
                    ? null
                    : new { type = error.GetType().Name, message = error.Message },
            };
            var line = JsonSerializer.Serialize(entry, JsonOptions) + Environment.NewLine;

            lock (FileGate)
            {
                File.AppendAllText(options.Path, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Auditing must never change the result of the desktop operation.
        }
    }
}
