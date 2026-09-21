using System.Drawing;
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
    public async Task Screenshot_WithRegion_UsesRequestedBounds()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);
        var region = new Rectangle(-20, 30, 320, 200);

        var result = service.Screenshot(path: null, includeImage: true, region);

        await Assert.That(platform.Captures.Single()).IsEqualTo(region);
        var metadata = ReadMetadata(result);
        await Assert.That(metadata.GetProperty("left").GetInt32()).IsEqualTo(-20);
        await Assert.That(metadata.GetProperty("top").GetInt32()).IsEqualTo(30);
        await Assert.That(metadata.GetProperty("width").GetInt32()).IsEqualTo(320);
        await Assert.That(metadata.GetProperty("height").GetInt32()).IsEqualTo(200);
    }

    [Test]
    public async Task Screenshot_RejectsEmptyRegion()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.Screenshot(null, includeImage: false, new Rectangle(0, 0, 0, 100)))
            .Throws<ArgumentOutOfRangeException>();
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
    public async Task KeyboardActions_ResolveSingleKeysHotkeysAndTextEntry()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var keyResult = service.PressKey("?", duration: 0.02, delay: null, targetHandle: null);
        var hotkeyResult = service.Hotkey("ctrl+l", delay: null, targetHandle: null);
        var typeResult = service.TypeText("hello", delay: null, targetHandle: null);

        await Assert.That(keyResult).Contains("Pressed ?");
        await Assert.That(hotkeyResult).Contains("ctrl+l");
        await Assert.That(typeResult).Contains("5 character");
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
        await Assert.That(windows[0].GetProperty("isForeground").GetBoolean()).IsTrue();
        await Assert.That(windows[0].GetProperty("isMinimized").GetBoolean()).IsFalse();
        await Assert.That(windows[0].GetProperty("bounds").GetProperty("width").GetInt32()).IsEqualTo(640);
        await Assert.That(windows[0].GetProperty("bounds").GetProperty("height").GetInt32()).IsEqualTo(480);
    }

    [Test]
    public async Task ListWindows_RejectsInvalidLimit()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.ListWindows(0)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task FindWindows_FiltersByProcessTitleAndState()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        var windows = JsonDocument.Parse(service.FindWindows("NOTEPAD", "untitled", foregroundOnly: true, includeMinimized: false, limit: 10)).RootElement;

        await Assert.That(windows.GetArrayLength()).IsEqualTo(1);
        await Assert.That(windows[0].GetProperty("handle").GetInt64()).IsEqualTo(100);
    }

    [Test]
    public async Task FindWindows_RejectsInvalidLimit()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.FindWindows(null, null, false, true, 0))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ScreenshotWindow_UsesWindowBounds()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = service.ScreenshotWindow(100, path: null, includeImage: true);

        await Assert.That(platform.Captures.Single()).IsEqualTo(new Rectangle(10, 20, 640, 480));
        await Assert.That(ReadMetadata(result).GetProperty("width").GetInt32()).IsEqualTo(640);
        await Assert.That(ReadMetadata(result).GetProperty("height").GetInt32()).IsEqualTo(480);
    }

    [Test]
    public async Task ScreenshotWindow_RejectsUnknownHandle()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.ScreenshotWindow(999, path: null, includeImage: false))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task WindowQueries_RetryEnumerationOnceAndVerifyState()
    {
        var platform = new TestComputerRunPlatform { ListWindowsFailuresRemaining = 1 };
        var service = new ComputerRunService(platform);

        var verification = JsonDocument.Parse(service.VerifyWindow(100, "notepad", "untitled", requireForeground: true, allowMinimized: false)).RootElement;

        await Assert.That(verification.GetProperty("ok").GetBoolean()).IsTrue();
        await Assert.That(verification.GetProperty("reason").GetString()).IsEqualTo("matched");
        await Assert.That(platform.ListWindowsFailuresRemaining).IsEqualTo(0);
    }

    [Test]
    public async Task VerifyWindow_ReturnsReasonForStaleHandle()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        var verification = JsonDocument.Parse(service.VerifyWindow(999, null, null, false, true)).RootElement;

        await Assert.That(verification.GetProperty("ok").GetBoolean()).IsFalse();
        await Assert.That(verification.GetProperty("reason").GetString()).IsEqualTo("not_found");
    }

    [Test]
    public async Task WaitForWindow_FindsMatchingWindowWithoutInput()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        var result = JsonDocument.Parse(service.WaitForWindow("notepad", "untitled", foregroundOnly: true, includeMinimized: false, timeoutMilliseconds: 0, pollMilliseconds: 25)).RootElement;

        await Assert.That(result.GetProperty("found").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("window").GetProperty("handle").GetInt64()).IsEqualTo(100);
    }

    [Test]
    public async Task WaitForWindow_RejectsUnboundedArguments()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.WaitForWindow(null, null, false, true, 30_001, 100))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => service.WaitForWindow(null, null, false, true, 0, 10))
            .Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ActivateWindow_DelegatesHandleAndRestoreFlag()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = service.ActivateWindow(100, restore: false);

        await Assert.That(result).Contains("100");
        await Assert.That(platform.ActivatedWindows.Single()).IsEqualTo((100L, false));
    }

    [Test]
    public async Task ActivateWindow_RejectsInvalidHandle()
    {
        var service = new ComputerRunService(new TestComputerRunPlatform());

        await Assert.That(() => service.ActivateWindow(0, restore: true)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task KeyboardInput_WithTargetHandle_AbortsWhenTargetIsNotForeground()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        await Assert.That(() => service.Hotkey("alt+f4", delay: null, targetHandle: 101))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("No keyboard input was sent");

        await Assert.That(platform.Hotkeys).IsEmpty();
    }

    [Test]
    public async Task CloseWindow_RequestsExactHandleAndReportsClosed()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = JsonDocument.Parse(service.CloseWindow(100, timeoutMilliseconds: 0)).RootElement;

        await Assert.That(result.GetProperty("requested").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("closed").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("reason").GetString()).IsEqualTo("closed");
        await Assert.That(platform.CloseRequests.Single()).IsEqualTo(100L);
    }

    [Test]
    public async Task CloseWindow_UnknownHandleIsIdempotent()
    {
        var platform = new TestComputerRunPlatform();
        var service = new ComputerRunService(platform);

        var result = JsonDocument.Parse(service.CloseWindow(999, timeoutMilliseconds: 0)).RootElement;

        await Assert.That(result.GetProperty("requested").GetBoolean()).IsFalse();
        await Assert.That(result.GetProperty("closed").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("reason").GetString()).IsEqualTo("not_found");
        await Assert.That(platform.CloseRequests).IsEmpty();
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
