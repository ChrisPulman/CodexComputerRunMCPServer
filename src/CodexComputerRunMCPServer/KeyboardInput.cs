namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides helpers to parse human-readable key input strings and resolve them to Windows virtual-key sequences.
/// </summary>
internal static class KeyboardInput
{
    /// <summary>
    /// Virtual-key code for the Shift key.
    /// </summary>
    public const byte ShiftKey = 0x10;

    /// <summary>
    /// Virtual-key code for the Control key.
    /// </summary>
    public const byte ControlKey = 0x11;

    /// <summary>
    /// Virtual-key code for the Alt key.
    /// </summary>
    public const byte AltKey = 0x12;

    /// <summary>
    /// Virtual-key code for the V key.
    /// </summary>
    public const byte VKey = 0x56;

    /// <summary>
    /// Lookup table of named key tokens (for example, <c>enter</c> or <c>f5</c>) to virtual-key codes.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, byte> NamedVirtualKeys = CreateVirtualKeyMap();

    /// <summary>
    /// Splits a hotkey expression into key tokens.
    /// </summary>
    /// <param name="keys">
    /// A hotkey expression containing key names separated by <c>+</c>, <c>,</c>, or whitespace
    /// (for example, <c>Ctrl+Shift+P</c>).
    /// </param>
    /// <returns>An array of non-empty key tokens with surrounding whitespace removed.</returns>
    /// <exception cref="ArgumentException"><paramref name="keys"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static string[] SplitKeys(string keys)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keys);

        return keys
            .Replace('+', ' ')
            .Replace(',', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Resolves a hotkey expression into a flattened sequence of virtual-key codes.
    /// </summary>
    /// <param name="keys">The hotkey expression to resolve.</param>
    /// <param name="keyScanner">
    /// A function that maps a single character to a packed keyboard scan result, where:
    /// low byte = virtual-key code, high byte = modifier flags (Shift=1, Ctrl=2, Alt=4),
    /// and <c>-1</c> indicates no mapping.
    /// </param>
    /// <returns>
    /// A byte array containing the ordered virtual-key sequence needed to represent the input expression.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="keys"/> is invalid, empty after parsing, or contains an unknown key token.
    /// </exception>
    public static byte[] ResolveHotkey(string keys, Func<char, short> keyScanner)
    {
        var parts = SplitKeys(keys);
        if (parts.Length == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(keys));
        }

        var resolvedKeys = new List<byte>(parts.Length + 2);
        foreach (var part in parts)
        {
            resolvedKeys.AddRange(ResolveKeyChord(part, keyScanner));
        }

        return resolvedKeys.ToArray();
    }

    /// <summary>
    /// Resolves a single key token into one or more virtual-key codes.
    /// </summary>
    /// <param name="key">
    /// A single key token, either:
    /// a one-character literal (for example, <c>A</c>) or
    /// a named key (for example, <c>enter</c>, <c>ctrl</c>, <c>f12</c>).
    /// </param>
    /// <param name="keyScanner">
    /// A function that maps a character to a packed scan result used to derive required modifiers and base key code.
    /// </param>
    /// <returns>
    /// A byte array containing the resolved key chord. Character inputs may return modifier keys followed by the base key.
    /// Named keys return a single virtual-key code.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is invalid or cannot be resolved.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="keyScanner"/> is <see langword="null"/>.</exception>
    public static byte[] ResolveKeyChord(string key, Func<char, short> keyScanner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(keyScanner);

        var trimmed = key.Trim();
        if (trimmed.Length == 1)
        {
            var scan = keyScanner(trimmed[0]);
            if (scan != -1)
            {
                var keyCode = (byte)(scan & 0xFF);
                var shiftState = (scan >> 8) & 0xFF;
                var chord = new List<byte>(4);
                if ((shiftState & 0x01) != 0)
                {
                    chord.Add(ShiftKey);
                }

                if ((shiftState & 0x02) != 0)
                {
                    chord.Add(ControlKey);
                }

                if ((shiftState & 0x04) != 0)
                {
                    chord.Add(AltKey);
                }

                chord.Add(keyCode);
                return chord.ToArray();
            }
        }

        var normalized = trimmed.ToLowerInvariant();
        if (NamedVirtualKeys.TryGetValue(normalized, out var vk))
        {
            return [vk];
        }

        throw new ArgumentException($"Unknown key: {key}", nameof(key));
    }

    /// <summary>
    /// Creates the default named-key lookup used by <see cref="ResolveKeyChord"/>.
    /// </summary>
    /// <returns>
    /// A case-insensitive dictionary of key aliases and function-key names (<c>f1</c> through <c>f24</c>)
    /// mapped to their virtual-key codes.
    /// </returns>
    private static IReadOnlyDictionary<string, byte> CreateVirtualKeyMap()
    {
        var map = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            ["backspace"] = 0x08,
            ["tab"] = 0x09,
            ["enter"] = 0x0D,
            ["return"] = 0x0D,
            ["shift"] = ShiftKey,
            ["ctrl"] = ControlKey,
            ["control"] = ControlKey,
            ["alt"] = AltKey,
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

        return map;
    }
}
