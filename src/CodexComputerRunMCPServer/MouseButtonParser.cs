namespace CodexComputerRunMCPServer;

/// <summary>
/// Represents supported mouse buttons that can be parsed from input text.
/// </summary>
internal enum MouseButton
{
    /// <summary>
    /// The left mouse button.
    /// </summary>
    Left,

    /// <summary>
    /// The right mouse button.
    /// </summary>
    Right,

    /// <summary>
    /// The middle mouse button (typically the scroll wheel button).
    /// </summary>
    Middle,
}

/// <summary>
/// Provides parsing helpers for converting text input into <see cref="MouseButton"/> values.
/// </summary>
internal static class MouseButtonParser
{
    /// <summary>
    /// Parses a mouse button name into a <see cref="MouseButton"/> value.
    /// </summary>
    /// <param name="button">
    /// The button name to parse. Supported values are <c>left</c>, <c>right</c>, and <c>middle</c>.
    /// Leading and trailing whitespace is ignored, and matching is case-insensitive.
    /// </param>
    /// <returns>The parsed <see cref="MouseButton"/> value.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="button"/> is null, empty, whitespace, or not one of the supported values.
    /// </exception>
    public static MouseButton Parse(string button)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(button);

        return button.Trim().ToLowerInvariant() switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => throw new ArgumentException("button must be left, right, or middle.", nameof(button)),
        };
    }
}
