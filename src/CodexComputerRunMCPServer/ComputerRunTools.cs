using System.ComponentModel;
using System.Drawing;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Exposes MCP tool endpoints for desktop automation actions, including screen capture,
/// mouse input, keyboard input, and window/query utilities.
/// </summary>
/// <remarks>
/// Methods in this type are discovered through MCP attributes and delegate execution to
/// <see cref="ComputerRunToolRuntime.Service"/>.
/// </remarks>
[McpServerToolType]
public static class ComputerRunTools
{
    /// <summary>
    /// Captures the current virtual desktop as a PNG image.
    /// </summary>
    /// <param name="path">
    /// Optional destination path for the PNG file. If omitted, no file is created.
    /// </param>
    /// <param name="include_image">
    /// <see langword="true"/> to include PNG image data in the MCP tool result; otherwise <see langword="false"/>.
    /// </param>
    /// <returns>
    /// A <see cref="CallToolResult"/> containing the screenshot result payload.
    /// </returns>
    [McpServerTool]
    [Description("Capture the current desktop or a requested screen region as a PNG. Pass path to save it on disk; omit path for an in-memory MCP image result.")]
    public static CallToolResult screenshot(
        [Description("Optional output PNG path. If omitted, no temporary file is created.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true,
        [Description("Optional left edge of a screen-space capture region. Provide all four region values together.")] int? left = null,
        [Description("Optional top edge of a screen-space capture region. Provide all four region values together.")] int? top = null,
        [Description("Optional width of a screen-space capture region. Must be greater than zero.")] int? width = null,
        [Description("Optional height of a screen-space capture region. Must be greater than zero.")] int? height = null)
        => Invoke(service => service.Screenshot(path, include_image, CreateScreenshotRegion(left, top, width, height)));

    /// <summary>
    /// Moves the mouse cursor to absolute desktop coordinates.
    /// </summary>
    /// <param name="x">Absolute X coordinate.</param>
    /// <param name="y">Absolute Y coordinate.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Move the mouse cursor to absolute desktop coordinates.")]
    public static string move_mouse(
        [Description("Absolute X coordinate.")] int x,
        [Description("Absolute Y coordinate.")] int y,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.MoveMouse(x, y, delay));

    /// <summary>
    /// Performs a mouse click at the current cursor position or at provided coordinates.
    /// </summary>
    /// <param name="x">Optional absolute X coordinate.</param>
    /// <param name="y">Optional absolute Y coordinate.</param>
    /// <param name="button">Mouse button to click: <c>left</c>, <c>right</c>, or <c>middle</c>.</param>
    /// <param name="clicks">Number of click repetitions.</param>
    /// <param name="interval">Delay between repeated clicks, in seconds.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Click at the current cursor position or at absolute desktop coordinates.")]
    public static string click(
        [Description("Optional absolute X coordinate.")] int? x = null,
        [Description("Optional absolute Y coordinate.")] int? y = null,
        [Description("Mouse button: left, right, or middle.")] string button = "left",
        [Description("Number of clicks.")] int clicks = 1,
        [Description("Delay between repeated clicks, in seconds.")] double interval = 0.08,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.Click(x, y, button, clicks, interval, delay));

    /// <summary>
    /// Scrolls the mouse wheel, optionally after moving to specified coordinates.
    /// </summary>
    /// <param name="amount">Wheel notches; positive scrolls up and negative scrolls down.</param>
    /// <param name="x">Optional absolute X coordinate to move to before scrolling.</param>
    /// <param name="y">Optional absolute Y coordinate to move to before scrolling.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Scroll the mouse wheel. Positive amount scrolls up; negative amount scrolls down.")]
    public static string scroll(
        [Description("Wheel notches. Positive scrolls up; negative scrolls down.")] int amount = -3,
        [Description("Optional absolute X coordinate to move to before scrolling.")] int? x = null,
        [Description("Optional absolute Y coordinate to move to before scrolling.")] int? y = null,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.Scroll(amount, x, y, delay));

    /// <summary>
    /// Presses and releases a single keyboard key.
    /// </summary>
    /// <param name="key">Key name or single character to press.</param>
    /// <param name="duration">Time to hold the key, in seconds.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Press a single keyboard key, for example enter, tab, escape, f5, a, A, ?, or 1.")]
    public static string press_key(
        [Description("Key name or single character.")] string key,
        [Description("How long to hold the key, in seconds.")] double duration = 0.03,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.PressKey(key, duration, delay));

    /// <summary>
    /// Presses a keyboard shortcut chord such as <c>ctrl+l</c> or <c>ctrl+shift+escape</c>.
    /// </summary>
    /// <param name="keys">Shortcut text using <c>+</c>, comma, or space separators.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Press a keyboard shortcut, for example ctrl+l or ctrl+shift+escape.")]
    public static string hotkey(
        [Description("Shortcut text. Use +, comma, or space separators, e.g. ctrl+shift+escape.")] string keys,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.Hotkey(keys, delay));

    /// <summary>
    /// Enters Unicode text into the currently focused application using the platform's preferred text-entry path.
    /// </summary>
    /// <param name="text">Text content to paste.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Enter Unicode text into the focused application. Windows uses direct Unicode input without changing the clipboard.")]
    public static string type_text(
        [Description("Text to paste into the focused application.")] string text,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => InvokeControl(service => service.TypeText(text, delay));

    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    /// <returns>A JSON payload describing the current cursor coordinates.</returns>
    [McpServerTool]
    [Description("Return the current desktop cursor position as JSON.")]
    public static string cursor_position()
        => Invoke(service => service.CursorPosition());

    /// <summary>
    /// Lists visible top-level desktop windows.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return.</param>
    /// <returns>A JSON array payload with visible window metadata.</returns>
    [McpServerTool]
    [Description("List visible top-level desktop windows as JSON, including process identity, foreground/minimized state, and screen-space bounds when available.")]
    public static string list_windows([Description("Maximum number of windows to return.")] int limit = 50)
        => Invoke(service => service.ListWindows(limit));

    /// <summary>
    /// Finds visible windows using stable metadata instead of screen coordinates.
    /// </summary>
    [McpServerTool]
    [Description("Find visible top-level windows by optional process name, title substring, foreground state, or minimized state.")]
    public static string find_windows(
        [Description("Optional process name, matched case-insensitively, for example Notepad or msedge.")] string? process_name = null,
        [Description("Optional case-insensitive substring of the window title.")] string? title_contains = null,
        [Description("Return only windows reported as the current foreground window.")] bool foreground_only = false,
        [Description("Include minimized windows in the results.")] bool include_minimized = true,
        [Description("Maximum number of matching windows to return.")] int limit = 50)
        => Invoke(service => service.FindWindows(process_name, title_contains, foreground_only, include_minimized, limit));

    /// <summary>
    /// Captures a window's screen-space bounds by native handle.
    /// </summary>
    [McpServerTool]
    [Description("Capture a visible top-level window by native handle returned by list_windows or find_windows. The capture is screen-space and does not reveal occluded content.")]
    public static CallToolResult screenshot_window(
        [Description("Native window handle returned by list_windows or find_windows.")] long handle,
        [Description("Optional output PNG path. If omitted, no file is created.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true)
        => Invoke(service => service.ScreenshotWindow(handle, path, include_image));

    /// <summary>
    /// Brings a window returned by <see cref="list_windows"/> to the foreground.
    /// </summary>
    /// <param name="handle">Native window handle returned by list_windows.</param>
    /// <param name="restore">Restore the window first when it is minimized.</param>
    /// <returns>A human-readable activation result.</returns>
    [McpServerTool]
    [Description("Activate a previously enumerated window by native handle. Use a handle from list_windows; optionally restore it when minimized.")]
    public static string activate_window(
        [Description("Native window handle returned by list_windows.")] long handle,
        [Description("Restore the window before focusing it when minimized.")] bool restore = true)
        => InvokeControl(service => service.ActivateWindow(handle, restore));

    private static TResult Invoke<TResult>(Func<IComputerRunService, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        return action(ComputerRunToolRuntime.Service);
    }

    private static TResult InvokeControl<TResult>(Func<IComputerRunService, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = ComputerRunToolRuntime.BeginToolInvocation();
        using var control = ComputerRunToolRuntime.BeginDesktopControlInvocation();
        return action(ComputerRunToolRuntime.Service);
    }

    private static Rectangle? CreateScreenshotRegion(int? left, int? top, int? width, int? height)
    {
        var values = new[] { left, top, width, height };
        if (values.Any(value => value.HasValue) && values.Any(value => !value.HasValue))
        {
            throw new ArgumentException("left, top, width, and height must be supplied together.");
        }

        return left.HasValue
            ? new Rectangle(left.Value, top!.Value, width!.Value, height!.Value)
            : null;
    }
}
