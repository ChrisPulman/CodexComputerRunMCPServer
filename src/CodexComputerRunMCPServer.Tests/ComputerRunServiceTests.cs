using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace CodexComputerRunMCPServer.Tests;

public class ComputerRunServiceTests
{
    [Test]
    public async Task Screenshot_WithImage_ReturnsMetadataAndImageWithoutTempFile()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = service.Screenshot(path: null, includeImage: true);

        await Assert.That(platform.Captures.Count).IsEqualTo(1);
        await Assert.That(platform.SavedScreenshots.Count).IsEqualTo(0);
        await Assert.That(result.Content.Count).IsEqualTo(2);
        await Assert.That(result.Content.OfType<ImageContentBlock>().Any()).IsTrue();

        var metadata = ReadMetadata(result);
        await Assert.That(metadata.GetProperty("path").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(metadata.GetProperty("platform").GetString()).IsEqualTo("Test");
        await Assert.That(metadata.GetProperty("width").GetInt32()).IsEqualTo(640);
        await Assert.That(metadata.GetProperty("height").GetInt32()).IsEqualTo(480);
    }

    [Test]
    public async Task Screenshot_WithoutImageAndWithoutPath_AvoidsPngEncoding()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = service.Screenshot(path: null, includeImage: false);

        await Assert.That(platform.Captures.Count).IsEqualTo(0);
        await Assert.That(platform.SavedScreenshots.Count).IsEqualTo(0);
        await Assert.That(result.Content.Count).IsEqualTo(1);
        await Assert.That(ReadMetadata(result).GetProperty("message").GetString()).Contains("PNG image omitted");
    }

    [Test]
    public async Task Screenshot_WithPath_WritesResolvedFile()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);
        var output = Path.Combine(Path.GetTempPath(), "codex-computer-run-tests", Guid.NewGuid().ToString("N"), "screen.png");

        try
        {
            var result = service.Screenshot(output, includeImage: false);

            await Assert.That(File.Exists(output)).IsTrue();
            await Assert.That(platform.SavedScreenshots.Count).IsEqualTo(1);
            await Assert.That(ReadMetadata(result).GetProperty("path").GetString()).IsEqualTo(output);
        }
        finally
        {
            TryDelete(output);
        }
    }

    [Test]
    public async Task Screenshot_WithPathAndImage_CapturesOnceAndWritesImageBytes()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);
        var output = Path.Combine(Path.GetTempPath(), "codex-computer-run-tests", Guid.NewGuid().ToString("N"), "screen.png");

        try
        {
            var result = service.Screenshot(output, includeImage: true);

            await Assert.That(platform.Captures.Count).IsEqualTo(1);
            await Assert.That(platform.SavedScreenshots.Count).IsEqualTo(0);
            await Assert.That(File.ReadAllBytes(output).Length).IsEqualTo(platform.PngBytes.Length);
            await Assert.That(result.Content.OfType<ImageContentBlock>().Any()).IsTrue();
        }
        finally
        {
            TryDelete(output);
        }
    }

    [Test]
    public async Task MouseActions_MoveAndValidateCoordinates()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var moved = service.MoveMouse(5, 6, delay: null);
        var clicked = service.Click(7, 8, "right", clicks: 2, interval: 0.01, delay: null);
        var scrolled = service.Scroll(-4, 9, 10, delay: null);

        await Assert.That(moved).Contains("(5, 6)");
        await Assert.That(clicked).Contains("Clicked right 2 time(s)");
        await Assert.That(scrolled).Contains("-4");
        await Assert.That(platform.CursorMoves.Count).IsEqualTo(3);
        await Assert.That(platform.Clicks[0].Button).IsEqualTo(MouseButton.Right);
        await Assert.That(platform.Clicks[0].Clicks).IsEqualTo(2);
        await Assert.That(platform.Scrolls[0]).IsEqualTo(-4);
    }

    [Test]
    public async Task MouseActions_RejectPartialCoordinatesAndBadCounts()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        await Assert.That(() => service.Click(1, null, "left", 1, 0, null)).Throws<ArgumentException>();
        await Assert.That(() => service.Click(null, null, "left", 0, 0, null)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => service.Click(null, null, "side", 1, 0, null)).Throws<ArgumentException>();
        await Assert.That(() => service.Scroll(1, null, 2, null)).Throws<ArgumentException>();
    }

    [Test]
    public async Task KeyboardActions_ResolveSingleKeysHotkeysAndPaste()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var keyResult = service.PressKey("?", duration: 0.02, delay: null);
        var hotkeyResult = service.Hotkey("ctrl+l", delay: null);
        var pasteResult = service.TypeText("hello", delay: null);

        await Assert.That(keyResult).Contains("Pressed ?");
        await Assert.That(hotkeyResult).Contains("ctrl+l");
        await Assert.That(pasteResult).Contains("5 character");
        await Assert.That(string.Join(",", platform.PressedKeys[0].Chord)).IsEqualTo($"{KeyboardInput.ShiftKey},191");
        await Assert.That(platform.PressedKeys[0].Duration).IsEqualTo(TimeSpan.FromSeconds(0.02));
        await Assert.That(string.Join(",", platform.Hotkeys[0])).IsEqualTo($"{KeyboardInput.ControlKey},76");
        await Assert.That(platform.PastedTexts[0]).IsEqualTo("hello");
    }

    [Test]
    public async Task JsonTools_ReturnCursorPositionAndWindows()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var cursor = JsonDocument.Parse(service.CursorPosition()).RootElement;
        var windows = JsonDocument.Parse(service.ListWindows(1)).RootElement;

        await Assert.That(cursor.GetProperty("x").GetInt32()).IsEqualTo(123);
        await Assert.That(cursor.GetProperty("y").GetInt32()).IsEqualTo(456);
        await Assert.That(windows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(windows[0].GetProperty("title").GetString()).IsEqualTo("Untitled - Notepad");
    }

    [Test]
    public async Task ListWindows_RejectsInvalidLimit()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.ListWindows(0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task Service_PropagatesUnsupportedPlatformErrors()
    {
        var service = new ComputerRunService(new UnsupportedComputerRunPlatform("TestOS"));

        await Assert.That(() => service.CursorPosition())
            .Throws<PlatformNotSupportedException>()
            .WithMessageContaining("TestOS");
    }

    private static JsonElement ReadMetadata(CallToolResult result)
    {
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Test cleanup only.
        }
    }
}
