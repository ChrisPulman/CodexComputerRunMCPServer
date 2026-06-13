using System.Drawing;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Linux implementation backed by common desktop automation commands.
/// </summary>
internal sealed class LinuxComputerRunPlatform(IExternalCommandRunner? commandRunner = null)
    : ExternalCommandPlatform(commandRunner), IComputerRunPlatform
{
    /// <inheritdoc />
    public string PlatformName => "Linux";

    /// <inheritdoc />
    public Rectangle GetVirtualScreenBounds()
    {
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
    public byte[] CapturePng(Rectangle bounds) => CaptureViaTempFile(path => SaveScreenshotPng(bounds, path));

    /// <inheritdoc />
    public void SaveScreenshotPng(Rectangle bounds, string path)
    {
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
        => RunXdotool(["mousemove", "--sync", x.ToString(), y.ToString()]);

    /// <inheritdoc />
    public DesktopPoint GetCursorPosition()
    {
        var output = RunXdotool(["getmouselocation", "--shell"]).StandardOutputText;
        var values = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.OrdinalIgnoreCase);

        return new DesktopPoint(ParseInt(values["X"], "X"), ParseInt(values["Y"], "Y"));
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

        RunXdotool([
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

        var button = amount > 0 ? "4" : "5";
        RunXdotool(["click", "--repeat", Math.Abs(amount).ToString(), button]);
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
            RunXdotool(["keydown", name]);
        }

        Thread.Sleep(duration);

        foreach (var name in names.Reverse())
        {
            RunXdotool(["keyup", name]);
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
        RunXdotool(["key", "--clearmodifiers", chord]);
    }

    /// <inheritdoc />
    public void PasteText(string text)
    {
        if (TrySetClipboardText(text))
        {
            PressHotkey([KeyboardInput.ControlKey, KeyboardInput.VKey]);
            return;
        }

        if (CommandRunner.CommandExists("xdotool"))
        {
            RunXdotool(["type", "--clearmodifiers", "--delay", "0", "--", text]);
            return;
        }

        throw MissingDependency(PlatformName, "wl-copy", "xclip", "xsel", "xdotool");
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> ListWindows(int limit)
    {
        if (CommandRunner.CommandExists("wmctrl"))
        {
            return ListWindowsWithWmctrl(limit);
        }

        if (CommandRunner.CommandExists("xdotool"))
        {
            return ListWindowsWithXdotool(limit);
        }

        throw MissingDependency(PlatformName, "wmctrl", "xdotool");
    }

    /// <inheritdoc />
    public short KeyScan(char character) => KeyboardInputDefaults.KeyScan(character);

    private ExternalCommandResult RunXdotool(IReadOnlyList<string> arguments)
    {
        if (!CommandRunner.CommandExists("xdotool"))
        {
            throw MissingDependency(PlatformName, "xdotool");
        }

        return RunRequired("xdotool", arguments);
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
        var ids = RunXdotool(["search", "--onlyvisible", "--name", "."]).StandardOutputText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(limit)
            .ToArray();
        var windows = new List<WindowInfo>(ids.Length);

        foreach (var id in ids)
        {
            var title = RunXdotool(["getwindowname", id]).StandardOutputText.Trim();
            var pidText = RunXdotool(["getwindowpid", id]).StandardOutputText.Trim();
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

