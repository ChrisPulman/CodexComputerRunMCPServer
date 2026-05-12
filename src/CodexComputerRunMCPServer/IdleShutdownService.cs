using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Stops the MCP host after a period with no active or recent tool invocations.
/// </summary>
/// <param name="options">Lifecycle options.</param>
/// <param name="applicationLifetime">Host lifetime controller.</param>
/// <param name="logger">Logger used for lifecycle diagnostics.</param>
internal sealed class IdleShutdownService(
    ComputerRunLifecycleOptions options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<IdleShutdownService> logger) : BackgroundService
{
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.IdleShutdownEnabled)
        {
            logger.LogDebug("Computer run idle shutdown is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(options.IdleCheckInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                var snapshot = ComputerRunToolRuntime.ActivityTracker.GetSnapshot();
                var nowUtc = _timeProvider.GetUtcNow();

                if (!ShouldStopForIdle(options, snapshot, nowUtc))
                {
                    continue;
                }

                logger.LogInformation(
                    "Stopping computer run MCP server after {IdleDuration:g} without MCP tool activity.",
                    snapshot.GetIdleDuration(nowUtc));

                applicationLifetime.StopApplication();
                return;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }

    /// <summary>
    /// Determines whether current activity satisfies the configured idle shutdown threshold.
    /// </summary>
    /// <param name="options">Lifecycle options.</param>
    /// <param name="snapshot">Current activity snapshot.</param>
    /// <param name="nowUtc">The current UTC time.</param>
    /// <returns><see langword="true"/> when the server should stop for idleness.</returns>
    internal static bool ShouldStopForIdle(
        ComputerRunLifecycleOptions options,
        ComputerRunActivitySnapshot snapshot,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshot);

        return options.IdleShutdownEnabled
            && snapshot.ActiveInvocations == 0
            && snapshot.GetIdleDuration(nowUtc) >= options.IdleTimeout;
    }
}
