using System.Drawing;
using System.Text;

namespace CodexComputerRunMCPServer;

/// <summary>
/// macOS implementation backed by standard macOS utilities plus cliclick for pointer actions.
/// </summary>
internal sealed class MacComputerRunPlatform(IExternalCommandRunner? commandRunner = null)
    : ExternalCommandPlatform(commandRunner), IComputerRunPlatform
{
    /// <inheritdoc />
    public string PlatformName => "macOS";

    /// <inheritdoc />
    public Rectangle GetVirtualScreenBounds()
    {
        var output = RunAppleScript("tell application \"Finder\" to get bounds of window of desktop")
            .StandardOutputText
            .Trim();
        var parts = output.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            throw new FormatException($"Could not parse macOS desktop bounds from '{output}'.");
        }

        var left = ParseInt(parts[0], "left");
        var top = ParseInt(parts[1], "top");
        var right = ParseInt(parts[2], "right");
        var bottom = ParseInt(parts[3], "bottom");
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <inheritdoc />
    public byte[] CapturePng(Rectangle bounds) => CaptureViaTempFile(path => SaveScreenshotPng(bounds, path));

    /// <inheritdoc />
    public void SaveScreenshotPng(Rectangle bounds, string path)
    {
        if (!CommandRunner.CommandExists("screencapture"))
        {
            throw MissingDependency(PlatformName, "screencapture");
        }

        _ = RunRequired("screencapture", ["-x", path]);
    }

    /// <inheritdoc />
    public void MoveCursor(int x, int y) => RunCliclick([$"m:{x},{y}"]);

    /// <inheritdoc />
    public DesktopPoint GetCursorPosition()
    {
        var output = RunCliclick(["p"]).StandardOutputText.Trim();
        var parts = output.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new FormatException($"Could not parse cliclick pointer position from '{output}'.");
        }

        return new DesktopPoint(ParseInt(parts[0], "x"), ParseInt(parts[1], "y"));
    }

    /// <inheritdoc />
    public void Click(MouseButton button, int clicks, TimeSpan interval)
    {
        if (button == MouseButton.Middle)
        {
            throw new PlatformNotSupportedException("macOS middle-click automation is not supported by the built-in adapter.");
        }

        var position = GetCursorPosition();
        var commandPrefix = button == MouseButton.Right ? "rc" : "c";
        var repeat = Math.Max(1, clicks);
        for (var i = 0; i < repeat; i++)
        {
            RunCliclick([$"{commandPrefix}:{position.X},{position.Y}"]);
            if (i + 1 < repeat && interval > TimeSpan.Zero)
            {
                Thread.Sleep(interval);
            }
        }
    }

    /// <inheritdoc />
    public void Scroll(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        RunCliclick([$"w:0,{-amount}"]);
    }

    /// <inheritdoc />
    public void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration)
    {
        PressHotkey(keyChord);
        if (duration > TimeSpan.Zero)
        {
            Thread.Sleep(duration);
        }
    }

    /// <inheritdoc />
    public void PressHotkey(IReadOnlyList<byte> virtualKeys)
    {
        if (virtualKeys.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(virtualKeys));
        }

        RunAppleScript(CreateKeyCodeScript(virtualKeys));
    }

    /// <inheritdoc />
    public void PasteText(string text)
    {
        if (!CommandRunner.CommandExists("pbcopy"))
        {
            throw MissingDependency(PlatformName, "pbcopy");
        }

        _ = RunRequired("pbcopy", [], text);
        PressHotkey([0x5B, KeyboardInput.VKey]);
    }

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> ListWindows(int limit)
    {
        var script = """
            set output to ""
            tell application "System Events"
                repeat with proc in (processes whose visible is true)
                    set pidValue to unix id of proc
                    set processName to name of proc
                    repeat with win in windows of proc
                        set titleValue to name of win
                        if titleValue is not "" then
                            set output to output & pidValue & tab & processName & tab & titleValue & linefeed
                        end if
                    end repeat
                end repeat
            end tell
            return output
            """;
        var output = RunAppleScript(script).StandardOutputText;
        var windows = new List<WindowInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (windows.Count >= limit)
            {
                break;
            }

            var parts = line.Split('\t', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                continue;
            }

            windows.Add(new WindowInfo(0, ParseInt(parts[0], "pid"), parts[1], parts[2]));
        }

        return windows;
    }

    /// <inheritdoc />
    public short KeyScan(char character) => KeyboardInputDefaults.KeyScan(character);

    private ExternalCommandResult RunAppleScript(string script)
    {
        if (!CommandRunner.CommandExists("osascript"))
        {
            throw MissingDependency(PlatformName, "osascript");
        }

        return RunRequired("osascript", ["-e", script]);
    }

    private ExternalCommandResult RunCliclick(IReadOnlyList<string> arguments)
    {
        if (!CommandRunner.CommandExists("cliclick"))
        {
            throw MissingDependency(PlatformName, "cliclick");
        }

        return RunRequired("cliclick", arguments);
    }

    private static string CreateKeyCodeScript(IReadOnlyList<byte> virtualKeys)
    {
        var mainIndex = virtualKeys.Count - 1;
        var mainKey = virtualKeys[mainIndex];
        var modifiers = virtualKeys.Take(mainIndex).Where(ExternalKeyNames.IsModifier).ToArray();
        var script = new StringBuilder("tell application \"System Events\" to key code ");
        script.Append(ExternalKeyNames.ToMacKeyCode(mainKey));

        if (modifiers.Length > 0)
        {
            script.Append(" using {");
            script.Append(string.Join(", ", modifiers.Select(ExternalKeyNames.ToMacModifierName)));
            script.Append('}');
        }

        return script.ToString();
    }
}

