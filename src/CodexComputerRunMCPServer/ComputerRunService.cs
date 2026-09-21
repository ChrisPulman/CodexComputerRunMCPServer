using System.Diagnostics;
using System.Drawing;
using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Defines high-level desktop automation operations exposed by the computer run service.
/// </summary>
internal interface IComputerRunService
{
    /// <summary>
    /// Captures a screenshot of the current virtual desktop and returns tool content metadata,
    /// optionally including raw image content.
    /// </summary>
    /// <param name="path">Optional destination path for the PNG file. Environment variables are expanded.</param>
    /// <param name="includeImage">
    /// <see langword="true"/> to include the PNG bytes in the tool response; otherwise metadata only.
    /// </param>
    /// <returns>A tool result containing serialized screenshot metadata and optional image content.</returns>
    CallToolResult Screenshot(string? path, bool includeImage, Rectangle? region = null);

    /// <summary>
    /// Moves the cursor to the specified desktop coordinates.
    /// </summary>
    /// <param name="x">The absolute X coordinate in virtual desktop space.</param>
    /// <param name="y">The absolute Y coordinate in virtual desktop space.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message.</returns>
    string MoveMouse(int x, int y, double? delay);

    /// <summary>
    /// Performs one or more mouse clicks using the specified button, optionally moving first.
    /// </summary>
    /// <param name="x">Optional X coordinate. Must be provided together with <paramref name="y"/>.</param>
    /// <param name="y">Optional Y coordinate. Must be provided together with <paramref name="x"/>.</param>
    /// <param name="button">Mouse button name (for example, left, right, middle).</param>
    /// <param name="clicks">Number of clicks to perform. Must be at least 1.</param>
    /// <param name="interval">Interval in seconds between clicks. Negative values are clamped to 0.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message including final cursor position.</returns>
    string Click(int? x, int? y, string button, int clicks, double interval, double? delay);

    /// <summary>
    /// Scrolls the mouse wheel by the specified amount, optionally moving first.
    /// </summary>
    /// <param name="amount">Wheel notch delta to scroll.</param>
    /// <param name="x">Optional X coordinate. Must be provided together with <paramref name="y"/>.</param>
    /// <param name="y">Optional Y coordinate. Must be provided together with <paramref name="x"/>.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message.</returns>
    string Scroll(int amount, int? x, int? y, double? delay);

    /// <summary>
    /// Presses and holds a resolved key chord for the requested duration.
    /// </summary>
    /// <param name="key">The key or key chord to resolve.</param>
    /// <param name="duration">Hold duration in seconds. Negative values are clamped to 0.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message.</returns>
    string PressKey(string key, double duration, double? delay);

    /// <summary>
    /// Presses a hotkey combination.
    /// </summary>
    /// <param name="keys">Hotkey expression containing one or more key names.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message.</returns>
    string Hotkey(string keys, double? delay);

    /// <summary>
    /// Enters text into the focused application using the platform's preferred text-entry path.
    /// </summary>
    /// <param name="text">Text to enter. <see langword="null"/> is treated as an empty string.</param>
    /// <param name="delay">Optional delay in seconds to wait after the operation.</param>
    /// <returns>A human-readable operation result message with entered character count.</returns>
    string TypeText(string text, double? delay);

    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    /// <returns>A JSON payload containing <c>x</c> and <c>y</c> coordinates.</returns>
    string CursorPosition();

    /// <summary>
    /// Lists top desktop windows up to the specified limit.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return. Must be at least 1.</param>
    /// <returns>A JSON payload containing window metadata entries.</returns>
    string ListWindows(int limit);

    /// <summary>
    /// Finds visible windows by optional process, title, foreground, and minimized-state filters.
    /// </summary>
    string FindWindows(string? processName, string? titleContains, bool foregroundOnly, bool includeMinimized, int limit);

    /// <summary>
    /// Captures the screen-space bounds of a previously enumerated window handle.
    /// </summary>
    CallToolResult ScreenshotWindow(long handle, string? path, bool includeImage);

    /// <summary>
    /// Verifies that a native window still matches the requested targeting predicates.
    /// </summary>
    string VerifyWindow(long handle, string? processName, string? titleContains, bool requireForeground, bool allowMinimized);

    /// <summary>
    /// Waits for a matching window for a bounded period without performing desktop input.
    /// </summary>
    string WaitForWindow(string? processName, string? titleContains, bool foregroundOnly, bool includeMinimized, int timeoutMilliseconds, int pollMilliseconds);

    /// <summary>
    /// Brings a previously enumerated top-level window to the foreground.
    /// </summary>
    /// <param name="handle">Native window handle returned by <see cref="ListWindows"/>.</param>
    /// <param name="restore">Whether a minimized window should be restored before focusing it.</param>
    /// <returns>A human-readable operation result message.</returns>
    string ActivateWindow(long handle, bool restore);
}

/// <summary>
/// Default implementation of <see cref="IComputerRunService"/>.
/// </summary>
/// <param name="platform">Platform abstraction responsible for OS-level input and capture operations.</param>
internal sealed class ComputerRunService(IComputerRunPlatform platform) : IComputerRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Creates the default service instance using the built-in platform implementation for the current OS.
    /// </summary>
    /// <returns>A ready-to-use <see cref="IComputerRunService"/> instance.</returns>
    public static IComputerRunService CreateDefault() => new ComputerRunService(ComputerRunPlatformFactory.CreateDefault());

    /// <inheritdoc />
    public CallToolResult Screenshot(string? path, bool includeImage, Rectangle? region = null)
    {
        var bounds = region ?? platform.GetVirtualScreenBounds();
        ValidateScreenshotRegion(bounds);
        var screenshotPath = ResolveOptionalPath(path);
        byte[]? imageBytes = null;

        if (includeImage)
        {
            imageBytes = platform.CapturePng(bounds);
            if (screenshotPath is not null)
            {
                File.WriteAllBytes(screenshotPath, imageBytes);
            }
        }
        else if (screenshotPath is not null)
        {
            platform.SaveScreenshotPng(bounds, screenshotPath);
        }

        var metadata = new ScreenshotMetadata(
            CreateScreenshotMessage(screenshotPath, includeImage),
            screenshotPath,
            "image/png",
            platform.PlatformName,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height);

        List<ContentBlock> content =
        [
            new TextContentBlock { Text = JsonSerializer.Serialize(metadata, JsonOptions) },
        ];

        if (imageBytes is not null)
        {
            content.Add(ImageContentBlock.FromBytes(imageBytes, "image/png"));
        }

        return new CallToolResult { Content = content };
    }

    /// <inheritdoc />
    public string MoveMouse(int x, int y, double? delay)
    {
        platform.MoveCursor(x, y);
        Delay.Sleep(delay);
        return $"Moved cursor to ({x}, {y}).";
    }

    /// <inheritdoc />
    public string Click(int? x, int? y, string button, int clicks, double interval, double? delay)
    {
        MoveCursorIfCoordinatesProvided(x, y);

        if (clicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(clicks), "clicks must be at least 1.");
        }

        var parsedButton = MouseButtonParser.Parse(button);
        var intervalDelay = Delay.FromSeconds(Math.Max(0, interval), nameof(interval));
        platform.Click(parsedButton, clicks, intervalDelay);

        Delay.Sleep(delay);
        var point = platform.GetCursorPosition();
        return $"Clicked {button} {clicks} time(s) at ({point.X}, {point.Y}).";
    }

    /// <inheritdoc />
    public string Scroll(int amount, int? x, int? y, double? delay)
    {
        MoveCursorIfCoordinatesProvided(x, y);

        platform.Scroll(amount);
        Delay.Sleep(delay);
        return $"Scrolled {amount} wheel notch(es).";
    }

    /// <inheritdoc />
    public string PressKey(string key, double duration, double? delay)
    {
        var keyChord = KeyboardInput.ResolveKeyChord(key, platform.KeyScan);
        var holdDuration = Delay.FromSeconds(Math.Max(0, duration), nameof(duration));
        platform.PressKey(keyChord, holdDuration);

        Delay.Sleep(delay);
        return $"Pressed {key}.";
    }

    /// <inheritdoc />
    public string Hotkey(string keys, double? delay)
    {
        var virtualKeys = KeyboardInput.ResolveHotkey(keys, platform.KeyScan);
        platform.PressHotkey(virtualKeys);

        Delay.Sleep(delay);
        return $"Pressed hotkey {string.Join('+', KeyboardInput.SplitKeys(keys))}.";
    }

    /// <inheritdoc />
    public string TypeText(string text, double? delay)
    {
        var enteredText = text ?? string.Empty;
        platform.TypeText(enteredText);

        Delay.Sleep(delay);
        return $"Entered {enteredText.Length} character(s) into the focused app.";
    }

    /// <inheritdoc />
    public string CursorPosition()
    {
        var point = platform.GetCursorPosition();
        return JsonSerializer.Serialize(new { x = point.X, y = point.Y }, JsonOptions);
    }

    /// <inheritdoc />
    public string ListWindows(int limit)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be at least 1.");
        }

        var windows = ListWindowsWithSingleRetry(limit);
        return JsonSerializer.Serialize(windows, JsonOptions);
    }

    /// <inheritdoc />
    public string FindWindows(
        string? processName,
        string? titleContains,
        bool foregroundOnly,
        bool includeMinimized,
        int limit)
    {
        ValidateWindowLimit(limit);

        var normalizedProcess = NormalizeFilter(processName);
        var normalizedTitle = NormalizeFilter(titleContains);
        var windows = FindMatchingWindowInfos(normalizedProcess, normalizedTitle, foregroundOnly, includeMinimized, limit);

        return JsonSerializer.Serialize(windows, JsonOptions);
    }

    /// <inheritdoc />
    public CallToolResult ScreenshotWindow(long handle, string? path, bool includeImage)
    {
        if (handle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "handle must be a positive native window handle.");
        }

        var window = ListWindowsWithSingleRetry(WindowEnumerationLimit)
            .FirstOrDefault(candidate => candidate.Handle == handle);

        if (window is null)
        {
            throw new ArgumentException(
                $"Window handle {handle} was not found among visible top-level windows. Call list_windows or find_windows again.",
                nameof(handle));
        }

        if (window.Bounds is null)
        {
            throw new PlatformNotSupportedException(
                $"The current platform did not provide screen bounds for window handle {handle}.");
        }

        var bounds = new Rectangle(window.Bounds.Left, window.Bounds.Top, window.Bounds.Width, window.Bounds.Height);
        return Screenshot(path, includeImage, bounds);
    }

    /// <inheritdoc />
    public string VerifyWindow(
        long handle,
        string? processName,
        string? titleContains,
        bool requireForeground,
        bool allowMinimized)
    {
        if (handle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "handle must be a positive native window handle.");
        }

        var expectedProcess = NormalizeFilter(processName);
        var expectedTitle = NormalizeFilter(titleContains);
        var window = ListWindowsWithSingleRetry(WindowEnumerationLimit)
            .FirstOrDefault(candidate => candidate.Handle == handle);

        if (window is null)
        {
            return JsonSerializer.Serialize(new { ok = false, handle, reason = "not_found", window = (WindowInfo?)null }, JsonOptions);
        }

        var reason = expectedProcess is not null
            && !string.Equals(window.ProcessName, expectedProcess, StringComparison.OrdinalIgnoreCase)
            ? "process_mismatch"
            : expectedTitle is not null
                && !window.Title.Contains(expectedTitle, StringComparison.OrdinalIgnoreCase)
                ? "title_mismatch"
                : requireForeground && window.IsForeground != true
                    ? "not_foreground"
                    : !allowMinimized && window.IsMinimized == true
                        ? "minimized"
                        : "matched";

        return JsonSerializer.Serialize(new { ok = reason == "matched", handle, reason, window }, JsonOptions);
    }

    /// <inheritdoc />
    public string WaitForWindow(
        string? processName,
        string? titleContains,
        bool foregroundOnly,
        bool includeMinimized,
        int timeoutMilliseconds,
        int pollMilliseconds)
    {
        if (timeoutMilliseconds is < 0 or > MaxWindowWaitMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), $"timeoutMilliseconds must be between 0 and {MaxWindowWaitMilliseconds}.");
        }

        if (pollMilliseconds is < MinWindowPollMilliseconds or > MaxWindowPollMilliseconds)
        {
            throw new ArgumentOutOfRangeException(nameof(pollMilliseconds), $"pollMilliseconds must be between {MinWindowPollMilliseconds} and {MaxWindowPollMilliseconds}.");
        }

        var expectedProcess = NormalizeFilter(processName);
        var expectedTitle = NormalizeFilter(titleContains);
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var match = FindMatchingWindowInfos(expectedProcess, expectedTitle, foregroundOnly, includeMinimized, 1)
                .FirstOrDefault();
            if (match is not null)
            {
                return JsonSerializer.Serialize(new
                {
                    found = true,
                    waitedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                    window = match,
                }, JsonOptions);
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                return JsonSerializer.Serialize(new
                {
                    found = false,
                    waitedMs = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 3),
                    window = (WindowInfo?)null,
                }, JsonOptions);
            }

            var remaining = TimeSpan.FromMilliseconds(timeoutMilliseconds) - stopwatch.Elapsed;
            Thread.Sleep(remaining < TimeSpan.FromMilliseconds(pollMilliseconds)
                ? remaining
                : TimeSpan.FromMilliseconds(pollMilliseconds));
        }
    }

    /// <inheritdoc />
    public string ActivateWindow(long handle, bool restore)
    {
        if (handle <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(handle), "handle must be a positive native window handle.");
        }

        platform.ActivateWindow(handle, restore);
        return $"Activated window {handle}.";
    }

    /// <summary>
    /// Resolves an optional file path by expanding environment variables and creating parent directories.
    /// </summary>
    /// <param name="path">Optional user-provided path.</param>
    /// <returns>
    /// An absolute file path when provided; otherwise <see langword="null"/> if input is empty or whitespace.
    /// </returns>
    private static string? ResolveOptionalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(path);
        var fullPath = Path.GetFullPath(expandedPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return fullPath;
    }

    /// <summary>
    /// Validates a requested screenshot region before passing it to a platform adapter.
    /// </summary>
    /// <param name="region">The region to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the region has no area.</exception>
    private static void ValidateScreenshotRegion(Rectangle region)
    {
        if (region.Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Screenshot width must be greater than zero.");
        }

        if (region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "Screenshot height must be greater than zero.");
        }
    }

    private static void ValidateWindowLimit(int limit)
    {
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be at least 1.");
        }
    }

    private static string? NormalizeFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IReadOnlyList<WindowInfo> FindMatchingWindowInfos(
        string? processName,
        string? titleContains,
        bool foregroundOnly,
        bool includeMinimized,
        int limit)
        => ListWindowsWithSingleRetry(WindowEnumerationLimit)
            .Where(window => processName is null
                || string.Equals(window.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            .Where(window => titleContains is null
                || window.Title.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            .Where(window => !foregroundOnly || window.IsForeground == true)
            .Where(window => includeMinimized || window.IsMinimized != true)
            .Take(limit)
            .ToArray();

    /// <summary>
    /// Retries only the idempotent window enumeration once. Input-changing operations never use this path.
    /// </summary>
    private IReadOnlyList<WindowInfo> ListWindowsWithSingleRetry(int limit)
    {
        try
        {
            return platform.ListWindows(limit);
        }
        catch
        {
            return platform.ListWindows(limit);
        }
    }

    private const int WindowEnumerationLimit = 256;
    private const int MaxWindowWaitMilliseconds = 30_000;
    private const int MinWindowPollMilliseconds = 25;
    private const int MaxWindowPollMilliseconds = 1_000;

    /// <summary>
    /// Creates a user-facing status message for screenshot operations.
    /// </summary>
    /// <param name="screenshotPath">Resolved screenshot path, if one is used.</param>
    /// <param name="includeImage">Whether the screenshot bytes are included in-memory in the response.</param>
    /// <returns>A status message describing where screenshot output was produced.</returns>
    private static string CreateScreenshotMessage(string? screenshotPath, bool includeImage)
    {
        if (screenshotPath is not null)
        {
            return $"Screenshot saved to {screenshotPath}";
        }

        return includeImage ? "Screenshot captured in memory." : "Virtual desktop metadata captured; PNG image omitted.";
    }

    /// <summary>
    /// Moves the cursor only when both coordinates are provided.
    /// </summary>
    /// <param name="x">Optional X coordinate.</param>
    /// <param name="y">Optional Y coordinate.</param>
    /// <exception cref="ArgumentException">Thrown when only one coordinate is supplied.</exception>
    private void MoveCursorIfCoordinatesProvided(int? x, int? y)
    {
        if (x.HasValue != y.HasValue)
        {
            throw new ArgumentException("Both x and y must be supplied together.");
        }

        if (x is not null && y is not null)
        {
            platform.MoveCursor(x.Value, y.Value);
        }
    }

}

/// <summary>
/// Defines low-level, platform-specific desktop automation primitives used by <see cref="ComputerRunService"/>.
/// </summary>
internal interface IComputerRunPlatform
{
    /// <summary>
    /// Gets a display name for the current platform implementation.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets the full bounds of the virtual desktop spanning all monitors.
    /// </summary>
    /// <returns>The virtual screen rectangle.</returns>
    Rectangle GetVirtualScreenBounds();

    /// <summary>
    /// Captures a PNG image for the specified bounds and returns raw bytes.
    /// </summary>
    /// <param name="bounds">The area to capture.</param>
    /// <returns>PNG byte array.</returns>
    byte[] CapturePng(Rectangle bounds);

    /// <summary>
    /// Captures a PNG image for the specified bounds and writes it to disk.
    /// </summary>
    /// <param name="bounds">The area to capture.</param>
    /// <param name="path">Destination file path.</param>
    void SaveScreenshotPng(Rectangle bounds, string path);

    /// <summary>
    /// Moves the cursor to absolute desktop coordinates.
    /// </summary>
    /// <param name="x">Absolute X coordinate.</param>
    /// <param name="y">Absolute Y coordinate.</param>
    void MoveCursor(int x, int y);

    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    /// <returns>The current desktop point.</returns>
    DesktopPoint GetCursorPosition();

    /// <summary>
    /// Performs mouse clicks with the specified button and interval.
    /// </summary>
    /// <param name="button">Mouse button to click.</param>
    /// <param name="clicks">Number of clicks.</param>
    /// <param name="interval">Delay between clicks.</param>
    void Click(MouseButton button, int clicks, TimeSpan interval);

    /// <summary>
    /// Scrolls the mouse wheel by the given amount.
    /// </summary>
    /// <param name="amount">Wheel notch delta.</param>
    void Scroll(int amount);

    /// <summary>
    /// Presses and holds a resolved key chord for a duration.
    /// </summary>
    /// <param name="keyChord">Virtual key sequence representing the chord.</param>
    /// <param name="duration">Hold duration.</param>
    void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration);

    /// <summary>
    /// Presses a hotkey represented by virtual key codes.
    /// </summary>
    /// <param name="virtualKeys">Virtual key sequence to press.</param>
    void PressHotkey(IReadOnlyList<byte> virtualKeys);

    /// <summary>
    /// Enters text into the focused application using the platform's preferred text-entry path.
    /// </summary>
    /// <param name="text">Text to enter.</param>
    void TypeText(string text);

    /// <summary>
    /// Enumerates top-level windows up to the requested limit.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return.</param>
    /// <returns>Window metadata collection.</returns>
    IReadOnlyList<WindowInfo> ListWindows(int limit);

    /// <summary>
    /// Brings a native window handle to the foreground.
    /// </summary>
    /// <param name="handle">Native window handle returned by the platform.</param>
    /// <param name="restore">Whether a minimized window should be restored first.</param>
    void ActivateWindow(long handle, bool restore);

    /// <summary>
    /// Resolves a character to a platform-specific key scan code.
    /// </summary>
    /// <param name="character">Character to resolve.</param>
    /// <returns>Scan code for the specified character.</returns>
    short KeyScan(char character);
}

/// <summary>
/// Represents a point in desktop coordinate space.
/// </summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
internal readonly record struct DesktopPoint(int X, int Y);

/// <summary>
/// Describes screenshot metadata returned by the screenshot operation.
/// </summary>
/// <param name="Message">Human-readable operation message.</param>
/// <param name="Path">Absolute file path when the image was saved to disk; otherwise <see langword="null"/>.</param>
/// <param name="MimeType">Media type of the captured image.</param>
/// <param name="Left">Left edge of the captured virtual desktop bounds.</param>
/// <param name="Top">Top edge of the captured virtual desktop bounds.</param>
/// <param name="Width">Width of the captured virtual desktop bounds.</param>
/// <param name="Height">Height of the captured virtual desktop bounds.</param>
internal sealed record ScreenshotMetadata(
    string Message,
    string? Path,
    string MimeType,
    string Platform,
    int Left,
    int Top,
    int Width,
    int Height);

/// <summary>
/// Represents a single desktop window entry.
/// </summary>
/// <param name="Handle">Native window handle.</param>
/// <param name="ProcessId">Owning process identifier.</param>
/// <param name="ProcessName">Owning process name, when available.</param>
/// <param name="Title">Window title text.</param>
/// <param name="IsForeground">Whether the window is the current foreground window, when the platform reports it.</param>
/// <param name="IsMinimized">Whether the window is minimized, when the platform reports it.</param>
/// <param name="Bounds">Window bounds in virtual desktop screen coordinates, when available.</param>
internal sealed record WindowInfo(
    long Handle,
    int ProcessId,
    string? ProcessName,
    string Title,
    bool? IsForeground = null,
    bool? IsMinimized = null,
    WindowBounds? Bounds = null);

/// <summary>
/// Describes a top-level window's screen-space bounds.
/// </summary>
/// <param name="Left">Left edge in virtual desktop coordinates.</param>
/// <param name="Top">Top edge in virtual desktop coordinates.</param>
/// <param name="Width">Window width in pixels.</param>
/// <param name="Height">Window height in pixels.</param>
internal sealed record WindowBounds(int Left, int Top, int Width, int Height);
