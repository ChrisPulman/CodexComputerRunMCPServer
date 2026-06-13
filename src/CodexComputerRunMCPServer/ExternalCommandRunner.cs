using System.Diagnostics;
using System.Text;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Runs external desktop commands used by non-Windows platform adapters.
/// </summary>
internal interface IExternalCommandRunner
{
    /// <summary>
    /// Determines whether an executable is available on the current PATH.
    /// </summary>
    /// <param name="fileName">Executable name or absolute path.</param>
    /// <returns><see langword="true"/> when the command can be started.</returns>
    bool CommandExists(string fileName);

    /// <summary>
    /// Runs a command and captures stdout/stderr.
    /// </summary>
    /// <param name="fileName">Executable name.</param>
    /// <param name="arguments">Command arguments.</param>
    /// <param name="standardInput">Optional UTF-8 stdin text.</param>
    /// <param name="timeout">Optional command timeout.</param>
    /// <returns>The completed process result.</returns>
    ExternalCommandResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        TimeSpan? timeout = null);
}

/// <summary>
/// Captured external command result.
/// </summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="StandardOutput">Raw stdout bytes.</param>
/// <param name="StandardError">Decoded stderr text.</param>
internal sealed record ExternalCommandResult(int ExitCode, byte[] StandardOutput, string StandardError)
{
    /// <summary>
    /// Gets stdout decoded as UTF-8.
    /// </summary>
    public string StandardOutputText => Encoding.UTF8.GetString(StandardOutput);
}

/// <summary>
/// Default process-backed command runner.
/// </summary>
internal sealed class ExternalCommandRunner : IExternalCommandRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <inheritdoc />
    public bool CommandExists(string fileName)
    {
        if (Path.IsPathFullyQualified(fileName) || fileName.Contains(Path.DirectorySeparatorChar))
        {
            return File.Exists(fileName);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public ExternalCommandResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        TimeSpan? timeout = null)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        using var output = new MemoryStream();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start {fileName}.");
        }

        if (standardInput is not null)
        {
            process.StandardInput.Write(standardInput);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)(timeout ?? DefaultTimeout).TotalMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process exited between timeout detection and kill.
            }

            throw new TimeoutException($"{fileName} timed out.");
        }

        outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        return new ExternalCommandResult(process.ExitCode, output.ToArray(), error);
    }
}

