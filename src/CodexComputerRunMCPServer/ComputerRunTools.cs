using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodexComputerRunMCPServer;

[McpServerToolType]
public static class ComputerRunTools
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly IReadOnlyDictionary<string, byte> VirtualKeys = CreateVirtualKeyMap();

    [McpServerTool]
    [Description("Capture the Windows virtual desktop as a PNG. By default the screenshot is temporary; pass path to keep it on disk.")]
    public static CallToolResult screenshot(
        [Description("Optional output PNG path. If omitted, a temporary file is deleted after capture.")] string? path = null,
        [Description("Include PNG image data in the MCP tool result.")] bool include_image = true)
    {
        EnsureWindows();

        var ephemeral = string.IsNullOrWhiteSpace(path);
        var screenshotPath = ephemeral
            ? Path.Combine(Path.GetTempPath(), $"codex-computer-run-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}.png")
            : Environment.ExpandEnvironmentVariables(path!);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshotPath))!);

        var bounds = GetVirtualScreenBounds();
        using var bitmap = new Bitmap(bounds.Width, bounds.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(screenshotPath, ImageFormat.Png);

        try
        {
            var imageBytes = File.ReadAllBytes(screenshotPath);
            var metadata = new ScreenshotMetadata(
                ephemeral ? "Screenshot captured temporarily." : $"Screenshot saved to {screenshotPath}",
                ephemeral ? null : screenshotPath,
                "image/png",
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height);

            List<ContentBlock> content =
            [
                new TextContentBlock { Text = JsonSerializer.Serialize(metadata, JsonOptions) },
            ];

            if (include_image)
            {
                content.Add(ImageContentBlock.FromBytes(imageBytes, "image/png"));
            }

            return new CallToolResult { Content = content };
        }
        finally
        {
            if (ephemeral)
            {
                TryDelete(screenshotPath);
            }
        }
    }

    [McpServerTool]
    [Description("Move the mouse cursor to absolute Windows virtual-screen coordinates.")]
    public static string move_mouse(
        [Description("Absolute X coordinate.")] int x,
        [Description("Absolute Y coordinate.")] int y,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        if (!NativeMethods.SetCursorPos(x, y))
        {
            ThrowLastWin32Error("SetCursorPos failed");
        }

        Delay.Sleep(delay);
        return $"Moved cursor to ({x}, {y}).";
    }

    [McpServerTool]
    [Description("Click at the current cursor position or at absolute Windows virtual-screen coordinates.")]
    public static string click(
        [Description("Optional absolute X coordinate.")] int? x = null,
        [Description("Optional absolute Y coordinate.")] int? y = null,
        [Description("Mouse button: left, right, or middle.")] string button = "left",
        [Description("Number of clicks.")] int clicks = 1,
        [Description("Delay between repeated clicks, in seconds.")] double interval = 0.08,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        if (x is not null && y is not null && !NativeMethods.SetCursorPos(x.Value, y.Value))
        {
            ThrowLastWin32Error("SetCursorPos failed");
        }

        if (clicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(clicks), "clicks must be at least 1.");
        }

        var (down, up) = button.Trim().ToLowerInvariant() switch
        {
            "left" => (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp),
            "right" => (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp),
            "middle" => (NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp),
            _ => throw new ArgumentException("button must be left, right, or middle.", nameof(button)),
        };

        for (var i = 0; i < clicks; i++)
        {
            NativeMethods.mouse_event(down, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(TimeSpan.FromMilliseconds(30));
            NativeMethods.mouse_event(up, 0, 0, 0, UIntPtr.Zero);
            if (i + 1 < clicks)
            {
                Thread.Sleep(TimeSpan.FromSeconds(Math.Max(0, interval)));
            }
        }

        Delay.Sleep(delay);
        var point = GetCursorPoint();
        return $"Clicked {button} {clicks} time(s) at ({point.X}, {point.Y}).";
    }

    [McpServerTool]
    [Description("Scroll the mouse wheel. Positive amount scrolls up; negative amount scrolls down.")]
    public static string scroll(
        [Description("Wheel notches. Positive scrolls up; negative scrolls down.")] int amount = -3,
        [Description("Optional absolute X coordinate to move to before scrolling.")] int? x = null,
        [Description("Optional absolute Y coordinate to move to before scrolling.")] int? y = null,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        if (x is not null && y is not null && !NativeMethods.SetCursorPos(x.Value, y.Value))
        {
            ThrowLastWin32Error("SetCursorPos failed");
        }

        NativeMethods.mouse_event(NativeMethods.MouseEventWheel, 0, 0, amount * 120, UIntPtr.Zero);
        Delay.Sleep(delay);
        return $"Scrolled {amount} wheel notch(es).";
    }

    [McpServerTool]
    [Description("Press a single keyboard key, for example enter, tab, escape, f5, a, or 1.")]
    public static string press_key(
        [Description("Key name or single character.")] string key,
        [Description("How long to hold the key, in seconds.")] double duration = 0.03,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        TapKey(key, duration);
        Delay.Sleep(delay);
        return $"Pressed {key}.";
    }

    [McpServerTool]
    [Description("Press a keyboard shortcut, for example ctrl+l or ctrl+shift+escape.")]
    public static string hotkey(
        [Description("Shortcut text. Use +, comma, or space separators, e.g. ctrl+shift+escape.")] string keys,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        var parts = SplitKeys(keys);
        PressHotkey(parts);
        Delay.Sleep(delay);
        return $"Pressed hotkey {string.Join('+', parts)}.";
    }

    [McpServerTool]
    [Description("Paste Unicode text into the focused Windows application using the clipboard and Ctrl+V.")]
    public static string type_text(
        [Description("Text to paste into the focused application.")] string text,
        [Description("Optional delay after the action, in seconds.")] double? delay = null)
    {
        EnsureWindows();
        SetClipboardText(text ?? string.Empty);
        PressHotkey(["ctrl", "v"]);
        Delay.Sleep(delay);
        return $"Pasted {(text ?? string.Empty).Length} character(s) into the focused app.";
    }

    [McpServerTool]
    [Description("Return the current Windows cursor position as JSON.")]
    public static string cursor_position()
    {
        EnsureWindows();
        var point = GetCursorPoint();
        return JsonSerializer.Serialize(new { x = point.X, y = point.Y }, JsonOptions);
    }

    [McpServerTool]
    [Description("List visible top-level Windows desktop windows as JSON.")]
    public static string list_windows([Description("Maximum number of windows to return.")] int limit = 50)
    {
        EnsureWindows();
        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be at least 1.");
        }

        var windows = new List<WindowInfo>();
        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (windows.Count >= limit)
            {
                return false;
            }

            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true;
            }

            var builder = new StringBuilder(length + 1);
            _ = NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            var processName = TryGetProcessName((int)processId);
            windows.Add(new WindowInfo(hWnd.ToInt64(), (int)processId, processName, title));
            return true;
        }, IntPtr.Zero);

        return JsonSerializer.Serialize(windows, JsonOptions);
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return Screen.PrimaryScreen?.Bounds ?? throw new InvalidOperationException("No Windows screens are available.");
        }

        var bounds = screens[0].Bounds;
        for (var i = 1; i < screens.Length; i++)
        {
            bounds = Rectangle.Union(bounds, screens[i].Bounds);
        }

        return bounds;
    }

    private static NativeMethods.Point GetCursorPoint()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            ThrowLastWin32Error("GetCursorPos failed");
        }

        return point;
    }

    private static void TapKey(string key, double duration)
    {
        var vk = ResolveVirtualKey(key);
        KeyDown(vk);
        Thread.Sleep(TimeSpan.FromSeconds(Math.Max(0, duration)));
        KeyUp(vk);
    }

    private static void PressHotkey(IReadOnlyList<string> keys)
    {
        if (keys.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(keys));
        }

        var virtualKeys = keys.Select(ResolveVirtualKey).ToArray();
        foreach (var vk in virtualKeys)
        {
            KeyDown(vk);
            Thread.Sleep(TimeSpan.FromMilliseconds(20));
        }

        for (var i = virtualKeys.Length - 1; i >= 0; i--)
        {
            KeyUp(virtualKeys[i]);
            Thread.Sleep(TimeSpan.FromMilliseconds(20));
        }
    }

    private static void KeyDown(byte vk) => NativeMethods.keybd_event(vk, 0, 0, UIntPtr.Zero);

    private static void KeyUp(byte vk) => NativeMethods.keybd_event(vk, 0, NativeMethods.KeyEventKeyUp, UIntPtr.Zero);

    private static byte ResolveVirtualKey(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();
        if (VirtualKeys.TryGetValue(normalized, out var vk))
        {
            return vk;
        }

        if (normalized.Length == 1)
        {
            var scan = NativeMethods.VkKeyScan(normalized[0]);
            if (scan != -1)
            {
                return (byte)(scan & 0xFF);
            }
        }

        throw new ArgumentException($"Unknown key: {key}", nameof(key));
    }

    private static string[] SplitKeys(string keys) => keys
        .Replace('+', ' ')
        .Replace(',', ' ')
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void SetClipboardText(string text)
    {
        var data = Encoding.Unicode.GetBytes(text + '\0');
        if (!NativeMethods.OpenClipboard(IntPtr.Zero))
        {
            ThrowLastWin32Error("OpenClipboard failed");
        }

        try
        {
            if (!NativeMethods.EmptyClipboard())
            {
                ThrowLastWin32Error("EmptyClipboard failed");
            }

            var handle = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable, (UIntPtr)data.Length);
            if (handle == IntPtr.Zero)
            {
                ThrowLastWin32Error("GlobalAlloc failed");
            }

            var locked = NativeMethods.GlobalLock(handle);
            if (locked == IntPtr.Zero)
            {
                ThrowLastWin32Error("GlobalLock failed");
            }

            Marshal.Copy(data, 0, locked, data.Length);
            _ = NativeMethods.GlobalUnlock(handle);

            if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, handle) == IntPtr.Zero)
            {
                ThrowLastWin32Error("SetClipboardData failed");
            }
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
        }
    }

    private static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, byte> CreateVirtualKeyMap()
    {
        var map = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["backspace"] = 0x08,
            ["tab"] = 0x09,
            ["enter"] = 0x0D,
            ["return"] = 0x0D,
            ["shift"] = 0x10,
            ["ctrl"] = 0x11,
            ["control"] = 0x11,
            ["alt"] = 0x12,
            ["pause"] = 0x13,
            ["capslock"] = 0x14,
            ["esc"] = 0x1B,
            ["escape"] = 0x1B,
            ["space"] = 0x20,
            ["pageup"] = 0x21,
            ["pagedown"] = 0x22,
            ["end"] = 0x23,
            ["home"] = 0x24,
            ["left"] = 0x25,
            ["up"] = 0x26,
            ["right"] = 0x27,
            ["down"] = 0x28,
            ["insert"] = 0x2D,
            ["delete"] = 0x2E,
            ["win"] = 0x5B,
            ["windows"] = 0x5B,
            ["cmd"] = 0x5B,
            ["meta"] = 0x5B,
            ["apps"] = 0x5D,
            ["numpad0"] = 0x60,
            ["numpad1"] = 0x61,
            ["numpad2"] = 0x62,
            ["numpad3"] = 0x63,
            ["numpad4"] = 0x64,
            ["numpad5"] = 0x65,
            ["numpad6"] = 0x66,
            ["numpad7"] = 0x67,
            ["numpad8"] = 0x68,
            ["numpad9"] = 0x69,
            ["multiply"] = 0x6A,
            ["add"] = 0x6B,
            ["subtract"] = 0x6D,
            ["decimal"] = 0x6E,
            ["divide"] = 0x6F,
            ["numlock"] = 0x90,
            ["scrolllock"] = 0x91,
        };

        for (var i = 1; i <= 24; i++)
        {
            map[$"f{i}"] = (byte)(0x6F + i);
        }

        for (var c = 'A'; c <= 'Z'; c++)
        {
            map[char.ToLowerInvariant(c).ToString()] = (byte)c;
        }

        for (var c = '0'; c <= '9'; c++)
        {
            map[c.ToString()] = (byte)c;
        }

        return map;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("CodexComputerRunMCPServer only operates on Windows.");
        }
    }

    private static void ThrowLastWin32Error(string message) => throw new Win32Exception(Marshal.GetLastWin32Error(), message);

    private sealed record ScreenshotMetadata(
        string Message,
        string? Path,
        string MimeType,
        int Left,
        int Top,
        int Width,
        int Height);

    private sealed record WindowInfo(long Handle, int ProcessId, string? ProcessName, string Title);
}
