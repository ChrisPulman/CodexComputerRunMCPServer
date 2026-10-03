using System.Buffers.Binary;
using System.Drawing;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Linux implementation backed by common desktop automation commands.
/// </summary>
internal sealed class LinuxComputerRunPlatform : ExternalCommandPlatform, IComputerRunPlatform
{
    private readonly bool _isWaylandSession;
    private Rectangle? _lastWaylandVirtualScreenBounds;

    public LinuxComputerRunPlatform(IExternalCommandRunner? commandRunner = null, bool? waylandSession = null)
        : base(commandRunner)
    {
        _isWaylandSession = waylandSession ??
            string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }

    /// <inheritdoc />
    public string PlatformName => "Linux";

    /// <inheritdoc />
    public Rectangle GetVirtualScreenBounds()
    {
        if (_isWaylandSession)
        {
            var png = CaptureViaTempFile(CaptureFullScreenshot);
            _lastWaylandVirtualScreenBounds = GetPngBounds(png);
            return _lastWaylandVirtualScreenBounds.Value;
        }

        if (CommandRunner.CommandExists("xdotool"))
        {
            var output = RunRequired("xdotool", ["getdisplaygeometry"]).StandardOutputText;
            var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                return new Rectangle(0, 0, ParseInt(parts[0], "width"), ParseInt(parts[1], "height"));
            }
        }

        if (CommandRunner.CommandExists("xrandr"))
        {
            var output = RunRequired("xrandr", ["--current"]).StandardOutputText;
            foreach (var line in output.Split('\n', StringSplitOptions.TrimEntries))
            {
                var currentIndex = line.IndexOf(" current ", StringComparison.OrdinalIgnoreCase);
                if (currentIndex < 0)
                {
                    continue;
                }

                var current = line[(currentIndex + " current ".Length)..];
                var commaIndex = current.IndexOf(',', StringComparison.Ordinal);
                if (commaIndex > 0)
                {
                    var dimensions = current[..commaIndex].Split('x', StringSplitOptions.TrimEntries);
                    if (dimensions.Length == 2)
                    {
                        return new Rectangle(
                            0,
                            0,
                            ParseInt(dimensions[0], "width"),
                            ParseInt(dimensions[1], "height"));
                    }
                }
            }
        }

        throw MissingDependency(PlatformName, "xdotool", "xrandr");
    }

    /// <inheritdoc />
    public byte[] CapturePng(Rectangle bounds, bool highlightCursor = true) => CaptureViaTempFile(path => SaveScreenshotPng(bounds, path, highlightCursor));

    /// <inheritdoc />
    public void SaveScreenshotPng(Rectangle bounds, string path, bool highlightCursor = true)
    {
        if (_isWaylandSession)
        {
            var isFullDesktop = _lastWaylandVirtualScreenBounds == bounds;
            if (isFullDesktop && CurrentDesktopContains("KDE") && CommandRunner.CommandExists("spectacle"))
            {
                RunSpectacle(path);
                return;
            }

            if (isFullDesktop && CurrentDesktopContains("GNOME") && CommandRunner.CommandExists("gnome-screenshot"))
            {
                _ = RunRequired("gnome-screenshot", ["-f", path]);
                return;
            }

            if (CommandRunner.CommandExists("grim"))
            {
                _ = RunRequired("grim", ["-g", $"{bounds.X},{bounds.Y} {bounds.Width}x{bounds.Height}", path]);
                return;
            }

            if (CommandRunner.CommandExists("gnome-screenshot"))
            {
                EnsureFullWaylandBounds(bounds, "gnome-screenshot");
                _ = RunRequired("gnome-screenshot", ["-f", path]);
                return;
            }

            if (CommandRunner.CommandExists("spectacle"))
            {
                EnsureFullWaylandBounds(bounds, "Spectacle");
                RunSpectacle(path);
                return;
            }

            throw MissingDependency("Linux Wayland", "grim (rectangular regions)", "gnome-screenshot", "spectacle");
        }

        if (CommandRunner.CommandExists("gnome-screenshot"))
        {
            _ = RunRequired("gnome-screenshot", ["-f", path]);
            return;
        }

        if (CommandRunner.CommandExists("grim"))
        {
            _ = RunRequired("grim", [path]);
            return;
        }

        if (CommandRunner.CommandExists("import"))
        {
            _ = RunRequired("import", ["-window", "root", path]);
            return;
        }

        throw MissingDependency(PlatformName, "gnome-screenshot", "grim", "import");
    }

    /// <inheritdoc />
    public void MoveCursor(int x, int y)
        => RunPointerCommand(_isWaylandSession
            ? ["mousemove", x.ToString(), y.ToString()]
            : ["mousemove", "--sync", x.ToString(), y.ToString()]);

    /// <inheritdoc />
    public DesktopPoint GetCursorPosition()
    {
        if (_isWaylandSession && CommandRunner.CommandExists("hyprctl"))
        {
            try
            {
                var cursorOutput = RunRequired("hyprctl", ["cursorpos"]).StandardOutputText.Trim();
                var coordinates = cursorOutput.Split(',', StringSplitOptions.TrimEntries);
                if (coordinates.Length == 2)
                {
                    return new DesktopPoint(ParseInt(coordinates[0], "X"), ParseInt(coordinates[1], "Y"));
                }

                throw new FormatException($"Could not parse Hyprland cursor position '{cursorOutput}'.");
            }
            catch (InvalidOperationException)
            {
                // hyprctl may be installed outside a Hyprland session; try wdotool next.
            }
        }

        string output;
        try
        {
            output = RunPointerCommand(["getmouselocation", "--shell"]).StandardOutputText;
        }
        catch (InvalidOperationException exception) when (_isWaylandSession)
        {
            throw new PlatformNotSupportedException(
                "This Wayland compositor does not expose the current global cursor position. " +
                "Pointer movement and clicking remain available, but cursor_position cannot be reported.",
                exception);
        }

        var values = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        return new DesktopPoint(ParseInt(values["X"], "X"), ParseInt(values["Y"], "Y"));
    }

    /// <inheritdoc />
    public void ActivateWindow(long handle, bool restore)
    {
        var windowId = $"0x{handle:X}";
        if (_isWaylandSession && CommandRunner.CommandExists("wdotool"))
        {
            _ = RunRequired("wdotool", ["windowactivate", handle.ToString()]);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("wmctrl"))
        {
            _ = RunRequired("wmctrl", ["-ia", windowId]);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("xdotool"))
        {
            _ = RunPointerCommand(["windowactivate", "--sync", handle.ToString()]);
            return;
        }

        throw MissingWindowDependency();
    }

    /// <inheritdoc />
    public void RequestCloseWindow(long handle)
    {
        var windowId = $"0x{handle:X}";
        if (_isWaylandSession && CommandRunner.CommandExists("wdotool"))
        {
            _ = RunRequired("wdotool", ["windowclose", handle.ToString()]);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("wmctrl"))
        {
            _ = RunRequired("wmctrl", ["-ic", windowId]);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("xdotool"))
        {
            _ = RunPointerCommand(["windowclose", handle.ToString()]);
            return;
        }

        throw MissingWindowDependency();
    }

    /// <inheritdoc />
    public void Click(MouseButton button, int clicks, TimeSpan interval)
    {
        var buttonNumber = button switch
        {
            MouseButton.Left => "1",
            MouseButton.Middle => "2",
            MouseButton.Right => "3",
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown mouse button."),
        };

        if (_isWaylandSession)
        {
            for (var index = 0; index < Math.Max(1, clicks); index++)
            {
                RunPointerCommand(["click", buttonNumber]);
                if (index + 1 < clicks && interval > TimeSpan.Zero)
                {
                    Thread.Sleep(interval);
                }
            }

            return;
        }

        RunPointerCommand([
            "click",
            "--repeat",
            Math.Max(1, clicks).ToString(),
            "--delay",
            Milliseconds(interval).ToString(),
            buttonNumber,
        ]);
    }

    /// <inheritdoc />
    public void Scroll(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        if (_isWaylandSession)
        {
            RunPointerCommand(["scroll", "0", (-amount).ToString()]);
            return;
        }

        var button = amount > 0 ? "4" : "5";
        RunPointerCommand(["click", "--repeat", Math.Abs(amount).ToString(), button]);
    }

    /// <inheritdoc />
    public void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            PressHotkey(keyChord);
            return;
        }

        var names = keyChord.Select(ExternalKeyNames.ToXdotoolName).ToArray();
        foreach (var name in names)
        {
            RunPointerCommand(["keydown", name]);
        }

        Thread.Sleep(duration);

        foreach (var name in names.Reverse())
        {
            RunPointerCommand(["keyup", name]);
        }
    }

    /// <inheritdoc />
    public void PressHotkey(IReadOnlyList<byte> virtualKeys)
    {
        if (virtualKeys.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(virtualKeys));
        }

        var chord = string.Join('+', virtualKeys.Select(ExternalKeyNames.ToXdotoolName));
        RunPointerCommand(["key", "--clearmodifiers", chord]);
    }

    /// <inheritdoc />
    public void TypeText(string text)
    {
        if (TrySetClipboardText(text))
        {
            PressHotkey([KeyboardInput.ControlKey, KeyboardInput.VKey]);
            return;
        }

        if (_isWaylandSession && CommandRunner.CommandExists("wdotool"))
        {
            RunRequired("wdotool", ["type", "--clearmodifiers", "--", text]);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("xdotool"))
        {
            RunPointerCommand(["type", "--clearmodifiers", "--delay", "0", "--", text]);
            return;
        }

        throw MissingDependency(_isWaylandSession ? "Linux Wayland" : PlatformName,
            "wl-copy", "xclip", "xsel", _isWaylandSession ? "wdotool" : "xdotool");
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> ListWindows(int limit)
    {
        if (_isWaylandSession && CommandRunner.CommandExists("wdotool"))
        {
            return ListWindowsWithWdotool(limit);
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("wmctrl"))
        {
            return ListWindowsWithWmctrl(limit);
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("xdotool"))
        {
            return ListWindowsWithXdotool(limit);
        }

        throw MissingWindowDependency();
    }

    /// <inheritdoc />
    public short KeyScan(char character) => KeyboardInputDefaults.KeyScan(character);

    private ExternalCommandResult RunPointerCommand(IReadOnlyList<string> arguments)
    {
        var command = _isWaylandSession ? "wdotool" : "xdotool";
        if (!CommandRunner.CommandExists(command))
        {
            throw MissingDependency(_isWaylandSession ? "Linux Wayland" : PlatformName, command);
        }

        return RunRequired(command, arguments);
    }

    private PlatformNotSupportedException MissingWindowDependency()
        => _isWaylandSession
            ? MissingDependency("Linux Wayland", "wdotool")
            : MissingDependency(PlatformName, "wmctrl", "xdotool");

    private void CaptureFullScreenshot(string path)
    {
        if (_isWaylandSession && CurrentDesktopContains("KDE") && CommandRunner.CommandExists("spectacle"))
        {
            RunSpectacle(path);
            return;
        }

        if (_isWaylandSession && CurrentDesktopContains("GNOME") && CommandRunner.CommandExists("gnome-screenshot"))
        {
            _ = RunRequired("gnome-screenshot", ["-f", path]);
            return;
        }

        if (CommandRunner.CommandExists("grim"))
        {
            _ = RunRequired("grim", [path]);
            return;
        }

        if (CommandRunner.CommandExists("gnome-screenshot"))
        {
            _ = RunRequired("gnome-screenshot", ["-f", path]);
            return;
        }

        if (CommandRunner.CommandExists("spectacle"))
        {
            RunSpectacle(path);
            return;
        }

        if (!_isWaylandSession && CommandRunner.CommandExists("import"))
        {
            _ = RunRequired("import", ["-window", "root", path]);
            return;
        }

        throw MissingDependency(_isWaylandSession ? "Linux Wayland" : PlatformName,
            "grim", "gnome-screenshot", "spectacle", "import (X11 only)");
    }

    private void EnsureFullWaylandBounds(Rectangle bounds, string captureTool)
    {
        var fullBounds = _lastWaylandVirtualScreenBounds ?? GetVirtualScreenBounds();
        if (bounds != fullBounds)
        {
            throw new PlatformNotSupportedException(
                $"{captureTool} can capture the full Wayland desktop but cannot capture an exact region. " +
                "Install grim for rectangular screenshot requests.");
        }
    }

    private void RunSpectacle(string path)
        => _ = RunRequired("spectacle", ["--background", "--nonotify", "--output", path]);

    private static bool CurrentDesktopContains(string name)
    {
        var desktop = string.Join(':',
            Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"),
            Environment.GetEnvironmentVariable("XDG_SESSION_DESKTOP"));
        return desktop.Contains(name, StringComparison.OrdinalIgnoreCase);
    }

    private static Rectangle GetPngBounds(byte[] png)
    {
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (png.Length < 24 || !png.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            throw new InvalidDataException("The desktop capture command did not produce a valid PNG image.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException($"The desktop capture returned invalid PNG dimensions {width}x{height}.");
        }

        return new Rectangle(0, 0, width, height);
    }

    private IReadOnlyList<WindowInfo> ListWindowsWithWdotool(int limit)
    {
        var ids = RunRequired("wdotool", ["search", "--name", ".", "--regex"]).StandardOutputText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(limit)
            .ToArray();
        var windows = new List<WindowInfo>(ids.Length);

        foreach (var id in ids)
        {
            var title = RunRequired("wdotool", ["getwindowname", id]).StandardOutputText.Trim();
            var pidText = RunRequired("wdotool", ["getwindowpid", id]).StandardOutputText.Trim();
            var handle = long.TryParse(id, out var parsedHandle) ? parsedHandle : 0;
            var pid = int.TryParse(pidText, out var parsedPid) ? parsedPid : 0;

            if (!string.IsNullOrWhiteSpace(title))
            {
                windows.Add(new WindowInfo(handle, pid, TryReadProcessName(pid), title));
            }
        }

        return windows;
    }

    private bool TrySetClipboardText(string text)
    {
        if (CommandRunner.CommandExists("wl-copy"))
        {
            _ = RunRequired("wl-copy", [], text);
            return true;
        }

        if (CommandRunner.CommandExists("xclip"))
        {
            _ = RunRequired("xclip", ["-selection", "clipboard"], text);
            return true;
        }

        if (CommandRunner.CommandExists("xsel"))
        {
            _ = RunRequired("xsel", ["--clipboard", "--input"], text);
            return true;
        }

        return false;
    }

    private IReadOnlyList<WindowInfo> ListWindowsWithWmctrl(int limit)
    {
        var output = RunRequired("wmctrl", ["-lp"]).StandardOutputText;
        var windows = new List<WindowInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (windows.Count >= limit)
            {
                break;
            }

            var parts = line.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 5)
            {
                continue;
            }

            var handle = Convert.ToInt64(parts[0][2..], 16);
            var pid = ParseInt(parts[2], "pid");
            var title = parts[4];
            if (!string.IsNullOrWhiteSpace(title))
            {
                windows.Add(new WindowInfo(handle, pid, TryReadProcessName(pid), title));
            }
        }

        return windows;
    }

    private IReadOnlyList<WindowInfo> ListWindowsWithXdotool(int limit)
    {
        var ids = RunPointerCommand(["search", "--onlyvisible", "--name", "."]).StandardOutputText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(limit)
            .ToArray();
        var windows = new List<WindowInfo>(ids.Length);

        foreach (var id in ids)
        {
            var title = RunPointerCommand(["getwindowname", id]).StandardOutputText.Trim();
            var pidText = RunPointerCommand(["getwindowpid", id]).StandardOutputText.Trim();
            var handle = long.TryParse(id, out var parsedHandle) ? parsedHandle : 0;
            var pid = int.TryParse(pidText, out var parsedPid) ? parsedPid : 0;

            if (!string.IsNullOrWhiteSpace(title))
            {
                windows.Add(new WindowInfo(handle, pid, TryReadProcessName(pid), title));
            }
        }

        return windows;
    }

    private static string? TryReadProcessName(int pid)
    {
        if (pid <= 0)
        {
            return null;
        }

        try
        {
            var path = $"/proc/{pid}/comm";
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }
        catch
        {
            return null;
        }
    }
}

