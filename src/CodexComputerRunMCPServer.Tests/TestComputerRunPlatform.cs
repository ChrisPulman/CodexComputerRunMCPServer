using System.Drawing;

namespace CodexComputerRunMCPServer.Tests;

internal sealed class TestComputerRunPlatform : IComputerRunPlatform
{
    public bool IsWindows { get; set; } = true;

    public Rectangle Bounds { get; set; } = new(10, 20, 640, 480);

    public byte[] PngBytes { get; set; } = [0x89, 0x50, 0x4E, 0x47];

    public DesktopPoint CursorPosition { get; set; } = new(123, 456);

    public List<(int X, int Y)> CursorMoves { get; } = [];

    public List<(MouseButton Button, int Clicks, TimeSpan Interval)> Clicks { get; } = [];

    public List<int> Scrolls { get; } = [];

    public List<(byte[] Chord, TimeSpan Duration)> PressedKeys { get; } = [];

    public List<byte[]> Hotkeys { get; } = [];

    public List<string> ClipboardTexts { get; } = [];

    public List<Rectangle> Captures { get; } = [];

    public List<(Rectangle Bounds, string Path)> SavedScreenshots { get; } = [];

    public List<WindowInfo> Windows { get; } =
    [
        new(100, 200, "notepad", "Untitled - Notepad"),
        new(101, 201, "explorer", "Downloads"),
    ];

    public Dictionary<char, short> KeyScans { get; } = new()
    {
        ['a'] = 0x41,
        ['A'] = (1 << 8) | 0x41,
        ['l'] = 0x4C,
        ['v'] = 0x56,
        ['1'] = 0x31,
        ['?'] = (1 << 8) | 0xBF,
    };

    public Rectangle GetVirtualScreenBounds() => Bounds;

    public byte[] CapturePng(Rectangle bounds)
    {
        Captures.Add(bounds);
        return PngBytes;
    }

    public void SaveScreenshotPng(Rectangle bounds, string path)
    {
        SavedScreenshots.Add((bounds, path));
        File.WriteAllBytes(path, PngBytes);
    }

    public void MoveCursor(int x, int y) => CursorMoves.Add((x, y));

    public DesktopPoint GetCursorPosition() => CursorPosition;

    public void Click(MouseButton button, int clicks, TimeSpan interval) => Clicks.Add((button, clicks, interval));

    public void Scroll(int amount) => Scrolls.Add(amount);

    public void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration) => PressedKeys.Add((keyChord.ToArray(), duration));

    public void PressHotkey(IReadOnlyList<byte> virtualKeys) => Hotkeys.Add(virtualKeys.ToArray());

    public void SetClipboardText(string text) => ClipboardTexts.Add(text);

    public IReadOnlyList<WindowInfo> ListWindows(int limit) => Windows.Take(limit).ToArray();

    public short KeyScan(char character) => KeyScans.TryGetValue(character, out var scan) ? scan : (short)-1;
}
