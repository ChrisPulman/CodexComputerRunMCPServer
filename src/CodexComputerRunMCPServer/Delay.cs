namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides helper methods for converting optional numeric delay values into <see cref="TimeSpan"/>
/// instances and applying synchronous thread delays.
/// </summary>
internal static class Delay
{
    /// <summary>
    /// Converts a delay value in seconds to a <see cref="TimeSpan"/> while enforcing finite input.
    /// </summary>
    /// <param name="seconds">The delay duration in seconds.</param>
    /// <param name="parameterName">The parameter name used when throwing argument exceptions.</param>
    /// <returns>
    /// <see cref="TimeSpan.Zero"/> when <paramref name="seconds"/> is less than or equal to zero;
    /// otherwise, a positive <see cref="TimeSpan"/> for the specified value.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="seconds"/> is <see cref="double.NaN"/>,
    /// <see cref="double.PositiveInfinity"/>, or <see cref="double.NegativeInfinity"/>.
    /// </exception>
    public static TimeSpan FromSeconds(double seconds, string parameterName)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds))
        {
            throw new ArgumentOutOfRangeException(parameterName, "Delay values must be finite.");
        }

        return seconds <= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Suspends the current thread for the specified number of seconds when a positive delay is provided.
    /// </summary>
    /// <param name="seconds">
    /// The optional delay duration in seconds. <see langword="null"/>, zero, and negative values result in no delay.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="seconds"/> has a non-finite value.
    /// </exception>
    public static void Sleep(double? seconds)
    {
        if (seconds is null)
        {
            return;
        }

        var delay = FromSeconds(seconds.Value, nameof(seconds));
        if (delay > TimeSpan.Zero)
        {
            Thread.Sleep(delay);
        }
    }
}
