using System.Drawing;
using System.Globalization;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Shared helpers for command-backed Linux and macOS desktop adapters.
/// </summary>
internal abstract class ExternalCommandPlatform(IExternalCommandRunner? commandRunner)
{
    private protected IExternalCommandRunner CommandRunner { get; } = commandRunner ?? new ExternalCommandRunner();

    private protected static PlatformNotSupportedException MissingDependency(
        string platformName,
        params string[] commands)
        => new(
            $"{platformName} desktop automation requires one of: {string.Join(", ", commands)}. " +
            "Install the missing command in the signed-in desktop session and restart the MCP server.");

    private protected ExternalCommandResult RunRequired(
        string fileName,
        IReadOnlyList<string> arguments,
        string? standardInput = null,
        TimeSpan? timeout = null)
    {
        var result = CommandRunner.Run(fileName, arguments, standardInput, timeout);
        if (result.ExitCode != 0)
        {
            var stderr = result.StandardError.Trim();
            var stdout = result.StandardOutputText.Trim();
            var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = $"exit code {result.ExitCode}";
            }

            throw new InvalidOperationException($"{fileName} failed: {detail}");
        }

        return result;
    }

    private protected byte[] CaptureViaTempFile(Action<string> capture)
    {
        var path = Path.Combine(Path.GetTempPath(), $"codex-computer-run-{Guid.NewGuid():N}.png");
        try
        {
            capture(path);
            return File.ReadAllBytes(path);
        }
        finally
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
    }

    private protected static int Milliseconds(TimeSpan interval)
        => Math.Max(0, (int)Math.Round(interval.TotalMilliseconds, MidpointRounding.AwayFromZero));

    private protected static int ParseInt(string value, string name)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new FormatException($"Could not parse {name} value '{value}'.");
    }
}

/// <summary>
/// Maps Windows-style virtual-key values to command-line key names.
/// </summary>
internal static class ExternalKeyNames
{
    public static string ToXdotoolName(byte virtualKey)
        => virtualKey switch
        {
            KeyboardInput.ShiftKey => "Shift_L",
            KeyboardInput.ControlKey => "Control_L",
            KeyboardInput.AltKey => "Alt_L",
            0x08 => "BackSpace",
            0x09 => "Tab",
            0x0D => "Return",
            0x1B => "Escape",
            0x20 => "space",
            0x21 => "Page_Up",
            0x22 => "Page_Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            0x5B => "Super_L",
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x41 and <= 0x5A => char.ToLowerInvariant((char)virtualKey).ToString(),
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            >= 0x60 and <= 0x69 => $"KP_{virtualKey - 0x60}",
            0xBA => "semicolon",
            0xBB => "equal",
            0xBC => "comma",
            0xBD => "minus",
            0xBE => "period",
            0xBF => "slash",
            0xC0 => "grave",
            0xDB => "bracketleft",
            0xDC => "backslash",
            0xDD => "bracketright",
            0xDE => "apostrophe",
            _ => throw new ArgumentException($"Unsupported virtual key: 0x{virtualKey:X2}.", nameof(virtualKey)),
        };

    public static int ToMacKeyCode(byte virtualKey)
        => virtualKey switch
        {
            KeyboardInput.ShiftKey => 56,
            KeyboardInput.ControlKey => 59,
            KeyboardInput.AltKey => 58,
            0x08 => 51,
            0x09 => 48,
            0x0D => 36,
            0x1B => 53,
            0x20 => 49,
            0x21 => 116,
            0x22 => 121,
            0x23 => 119,
            0x24 => 115,
            0x25 => 123,
            0x26 => 126,
            0x27 => 124,
            0x28 => 125,
            0x2D => 114,
            0x2E => 117,
            0x30 => 29,
            0x31 => 18,
            0x32 => 19,
            0x33 => 20,
            0x34 => 21,
            0x35 => 23,
            0x36 => 22,
            0x37 => 26,
            0x38 => 28,
            0x39 => 25,
            0x41 => 0,
            0x42 => 11,
            0x43 => 8,
            0x44 => 2,
            0x45 => 14,
            0x46 => 3,
            0x47 => 5,
            0x48 => 4,
            0x49 => 34,
            0x4A => 38,
            0x4B => 40,
            0x4C => 37,
            0x4D => 46,
            0x4E => 45,
            0x4F => 31,
            0x50 => 35,
            0x51 => 12,
            0x52 => 15,
            0x53 => 1,
            0x54 => 17,
            0x55 => 32,
            0x56 => 9,
            0x57 => 13,
            0x58 => 7,
            0x59 => 16,
            0x5A => 6,
            0x5B => 55,
            0x70 => 122,
            0x71 => 120,
            0x72 => 99,
            0x73 => 118,
            0x74 => 96,
            0x75 => 97,
            0x76 => 98,
            0x77 => 100,
            0x78 => 101,
            0x79 => 109,
            0x7A => 103,
            0x7B => 111,
            0xBA => 41,
            0xBB => 24,
            0xBC => 43,
            0xBD => 27,
            0xBE => 47,
            0xBF => 44,
            0xC0 => 50,
            0xDB => 33,
            0xDC => 42,
            0xDD => 30,
            0xDE => 39,
            _ => throw new ArgumentException($"Unsupported virtual key: 0x{virtualKey:X2}.", nameof(virtualKey)),
        };

    public static string ToMacModifierName(byte virtualKey)
        => virtualKey switch
        {
            KeyboardInput.ShiftKey => "shift down",
            KeyboardInput.ControlKey => "control down",
            KeyboardInput.AltKey => "option down",
            0x5B => "command down",
            _ => throw new ArgumentException($"Unsupported macOS modifier key: 0x{virtualKey:X2}.", nameof(virtualKey)),
        };

    public static bool IsModifier(byte virtualKey)
        => virtualKey is KeyboardInput.ShiftKey or KeyboardInput.ControlKey or KeyboardInput.AltKey or 0x5B;
}
