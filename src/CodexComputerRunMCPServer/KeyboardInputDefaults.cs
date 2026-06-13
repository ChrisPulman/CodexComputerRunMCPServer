namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides keyboard-layout independent fallback mappings for non-Windows platform adapters.
/// </summary>
internal static class KeyboardInputDefaults
{
    private const short ShiftFlag = 1 << 8;

    private static readonly IReadOnlyDictionary<char, short> SymbolScans = new Dictionary<char, short>
    {
        [' '] = 0x20,
        ['0'] = 0x30,
        ['1'] = 0x31,
        ['2'] = 0x32,
        ['3'] = 0x33,
        ['4'] = 0x34,
        ['5'] = 0x35,
        ['6'] = 0x36,
        ['7'] = 0x37,
        ['8'] = 0x38,
        ['9'] = 0x39,
        ['!'] = ShiftFlag | 0x31,
        ['@'] = ShiftFlag | 0x32,
        ['#'] = ShiftFlag | 0x33,
        ['$'] = ShiftFlag | 0x34,
        ['%'] = ShiftFlag | 0x35,
        ['^'] = ShiftFlag | 0x36,
        ['&'] = ShiftFlag | 0x37,
        ['*'] = ShiftFlag | 0x38,
        ['('] = ShiftFlag | 0x39,
        [')'] = ShiftFlag | 0x30,
        ['-'] = 0xBD,
        ['_'] = ShiftFlag | 0xBD,
        ['='] = 0xBB,
        ['+'] = ShiftFlag | 0xBB,
        ['['] = 0xDB,
        ['{'] = ShiftFlag | 0xDB,
        [']'] = 0xDD,
        ['}'] = ShiftFlag | 0xDD,
        ['\\'] = 0xDC,
        ['|'] = ShiftFlag | 0xDC,
        [';'] = 0xBA,
        [':'] = ShiftFlag | 0xBA,
        ['\''] = 0xDE,
        ['"'] = ShiftFlag | 0xDE,
        [','] = 0xBC,
        ['<'] = ShiftFlag | 0xBC,
        ['.'] = 0xBE,
        ['>'] = ShiftFlag | 0xBE,
        ['/'] = 0xBF,
        ['?'] = ShiftFlag | 0xBF,
        ['`'] = 0xC0,
        ['~'] = ShiftFlag | 0xC0,
    };

    /// <summary>
    /// Maps a character to a packed Windows-style virtual key and shift-state value.
    /// </summary>
    /// <param name="character">Character to map.</param>
    /// <returns>A packed scan value, or <c>-1</c> when no fallback mapping exists.</returns>
    public static short KeyScan(char character)
    {
        if (character is >= 'a' and <= 'z')
        {
            return (short)char.ToUpperInvariant(character);
        }

        if (character is >= 'A' and <= 'Z')
        {
            return (short)(ShiftFlag | character);
        }

        return SymbolScans.TryGetValue(character, out var scan) ? scan : (short)-1;
    }
}

