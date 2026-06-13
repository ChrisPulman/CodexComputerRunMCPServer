using System.Drawing;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Deterministic platform used when the current operating system has no implementation.
/// </summary>
/// <param name="osDescription">Human-readable OS description.</param>
internal sealed class UnsupportedComputerRunPlatform(string osDescription) : IComputerRunPlatform
{
    /// <inheritdoc />
    public string PlatformName => "Unsupported";

    /// <inheritdoc />
    public Rectangle GetVirtualScreenBounds() => throw CreateException();

    /// <inheritdoc />
    public byte[] CapturePng(Rectangle bounds) => throw CreateException();

    /// <inheritdoc />
    public void SaveScreenshotPng(Rectangle bounds, string path) => throw CreateException();

    /// <inheritdoc />
    public void MoveCursor(int x, int y) => throw CreateException();

    /// <inheritdoc />
    public DesktopPoint GetCursorPosition() => throw CreateException();

    /// <inheritdoc />
    public void Click(MouseButton button, int clicks, TimeSpan interval) => throw CreateException();

    /// <inheritdoc />
    public void Scroll(int amount) => throw CreateException();

    /// <inheritdoc />
    public void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration) => throw CreateException();

    /// <inheritdoc />
    public void PressHotkey(IReadOnlyList<byte> virtualKeys) => throw CreateException();

    /// <inheritdoc />
    public void PasteText(string text) => throw CreateException();

    /// <inheritdoc />
    public IReadOnlyList<WindowInfo> ListWindows(int limit) => throw CreateException();

    /// <inheritdoc />
    public short KeyScan(char character) => KeyboardInputDefaults.KeyScan(character);

    private PlatformNotSupportedException CreateException()
        => new(
            $"CodexComputerRunMCPServer does not support this operating system ({osDescription}). " +
            "Supported desktop targets are Windows, Linux, and macOS.");
}

