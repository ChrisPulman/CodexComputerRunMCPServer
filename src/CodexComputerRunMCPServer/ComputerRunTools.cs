using System.ComponentModel;
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
    /// Captures the Windows virtual desktop as a PNG image.
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
    [Description("Capture the Windows virtual desktop as a PNG. Pass path to save it on disk; omit path for an in-memory MCP image result.")]
    public static CallToolResult screenshot(
        [Description("Optional output PNG path. If omitted, no temporary file is created.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true)
        => ComputerRunToolRuntime.Service.Screenshot(path, include_image);

    /// <summary>
    /// Moves the mouse cursor to absolute virtual-screen coordinates.
    /// </summary>
    /// <param name="x">Absolute X coordinate.</param>
    /// <param name="y">Absolute Y coordinate.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Move the mouse cursor to absolute Windows virtual-screen coordinates.")]
    public static string move_mouse(
        [Description("Absolute X coordinate.")] int x,
        [Description("Absolute Y coordinate.")] int y,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => ComputerRunToolRuntime.Service.MoveMouse(x, y, delay);

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
    [Description("Click at the current cursor position or at absolute Windows virtual-screen coordinates.")]
    public static string click(
        [Description("Optional absolute X coordinate.")] int? x = null,
        [Description("Optional absolute Y coordinate.")] int? y = null,
        [Description("Mouse button: left, right, or middle.")] string button = "left",
        [Description("Number of clicks.")] int clicks = 1,
        [Description("Delay between repeated clicks, in seconds.")] double interval = 0.08,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => ComputerRunToolRuntime.Service.Click(x, y, button, clicks, interval, delay);

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
        => ComputerRunToolRuntime.Service.Scroll(amount, x, y, delay);

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
        => ComputerRunToolRuntime.Service.PressKey(key, duration, delay);

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
        => ComputerRunToolRuntime.Service.Hotkey(keys, delay);

    /// <summary>
    /// Pastes Unicode text into the currently focused Windows application via clipboard and Ctrl+V.
    /// </summary>
    /// <param name="text">Text content to paste.</param>
    /// <param name="delay">Optional post-action delay in seconds.</param>
    /// <returns>A JSON status string returned by the runtime service.</returns>
    [McpServerTool]
    [Description("Paste Unicode text into the focused Windows application using the clipboard and Ctrl+V.")]
    public static string type_text(
        [Description("Text to paste into the focused application.")] string text,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
        => ComputerRunToolRuntime.Service.TypeText(text, delay);

    /// <summary>
    /// Gets the current cursor position.
    /// </summary>
    /// <returns>A JSON payload describing the current cursor coordinates.</returns>
    [McpServerTool]
    [Description("Return the current Windows cursor position as JSON.")]
    public static string cursor_position()
        => ComputerRunToolRuntime.Service.CursorPosition();

    /// <summary>
    /// Lists visible top-level desktop windows.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return.</param>
    /// <returns>A JSON array payload with visible window metadata.</returns>
    [McpServerTool]
    [Description("List visible top-level Windows desktop windows as JSON.")]
    public static string list_windows([Description("Maximum number of windows to return.")] int limit = 50)
        => ComputerRunToolRuntime.Service.ListWindows(limit);
}
