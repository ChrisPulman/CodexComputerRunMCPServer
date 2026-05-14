using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Configures process lifecycle safeguards for the computer run MCP server.
/// </summary>
/// <param name="SingleInstanceEnabled">Whether startup should reject concurrent server instances.</param>
/// <param name="IdleShutdownEnabled">Whether the host should stop after a period without tool activity.</param>
/// <param name="IdleTimeout">How long the server may remain unused before it shuts down.</param>
/// <param name="IdleCheckInterval">How often idle state is checked.</param>
internal sealed record ComputerRunLifecycleOptions(
    bool SingleInstanceEnabled,
    bool IdleShutdownEnabled,
    TimeSpan IdleTimeout,
    TimeSpan IdleCheckInterval)
{
    /// <summary>
    /// Default time without MCP tool use before the server stops itself.
    /// </summary>
    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Default cadence for checking idle state.
    /// </summary>
    public static readonly TimeSpan DefaultIdleCheckInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets the default lifecycle options.
    /// </summary>
    public static ComputerRunLifecycleOptions Default { get; } = new(
        SingleInstanceEnabled: true,
        IdleShutdownEnabled: false,
        IdleTimeout: DefaultIdleTimeout,
        IdleCheckInterval: DefaultIdleCheckInterval);

    /// <summary>
    /// Creates lifecycle options from application configuration and supported environment variables.
    /// </summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The resolved lifecycle options.</returns>
    public static ComputerRunLifecycleOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var singleInstanceEnabled = ReadBoolean(
            configuration,
            "SingleInstanceEnabled",
            "CODEX_COMPUTER_RUN_SINGLE_INSTANCE",
            Default.SingleInstanceEnabled);

        var idleShutdownEnabled = ReadBoolean(
            configuration,
            "IdleShutdownEnabled",
            "CODEX_COMPUTER_RUN_IDLE_SHUTDOWN",
            Default.IdleShutdownEnabled);

        var idleTimeout = ReadSeconds(
            configuration,
            "IdleTimeoutSeconds",
            "CODEX_COMPUTER_RUN_IDLE_TIMEOUT_SECONDS",
            Default.IdleTimeout);

        var idleCheckInterval = ReadSeconds(
            configuration,
            "IdleCheckIntervalSeconds",
            "CODEX_COMPUTER_RUN_IDLE_CHECK_INTERVAL_SECONDS",
            Default.IdleCheckInterval);

        if (idleTimeout <= TimeSpan.Zero)
        {
            idleShutdownEnabled = false;
        }

        if (idleCheckInterval <= TimeSpan.Zero)
        {
            idleCheckInterval = Default.IdleCheckInterval;
        }

        return new ComputerRunLifecycleOptions(
            singleInstanceEnabled,
            idleShutdownEnabled,
            idleTimeout,
            idleCheckInterval);
    }

    /// <summary>
    /// Creates lifecycle options from environment variables only.
    /// </summary>
    /// <returns>The resolved lifecycle options.</returns>
    public static ComputerRunLifecycleOptions FromEnvironment()
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        return FromConfiguration(configuration);
    }

    private static string? ReadValue(IConfiguration configuration, string key, string environmentVariable)
        => configuration[$"ComputerRun:{key}"]
        ?? configuration[key]
        ?? configuration[environmentVariable]
        ?? Environment.GetEnvironmentVariable(environmentVariable);

    private static bool ReadBoolean(
        IConfiguration configuration,
        string key,
        string environmentVariable,
        bool defaultValue)
    {
        var value = ReadValue(configuration, key, environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => defaultValue,
        };
    }

    private static TimeSpan ReadSeconds(
        IConfiguration configuration,
        string key,
        string environmentVariable,
        TimeSpan defaultValue)
    {
        var value = ReadValue(configuration, key, environmentVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : defaultValue;
    }
}
