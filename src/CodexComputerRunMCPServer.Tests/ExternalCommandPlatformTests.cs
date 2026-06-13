using System.Text;

namespace CodexComputerRunMCPServer.Tests;

public class ExternalCommandPlatformTests
{
    [Test]
    public async Task Factory_SelectsPlatformForCurrentOperatingSystem()
    {
        var platform = ComputerRunPlatformFactory.CreateDefault();
        var expected = OperatingSystem.IsWindows()
            ? "Windows"
            : OperatingSystem.IsLinux()
                ? "Linux"
                : OperatingSystem.IsMacOS()
                    ? "macOS"
                    : "Unsupported";

        await Assert.That(platform.PlatformName).IsEqualTo(expected);
    }

    [Test]
    public async Task LinuxPlatform_ParsesDisplayGeometryAndSavesScreenshot()
    {
        var output = Path.Combine(Path.GetTempPath(), "codex-computer-run-tests", Guid.NewGuid().ToString("N"), "linux.png");
        var runner = new RecordingCommandRunner("xdotool", "gnome-screenshot")
        {
            OnRun = invocation =>
            {
                if (invocation.FileName == "xdotool")
                {
                    return RecordingCommandRunner.Text("800 600\n");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                File.WriteAllBytes(invocation.Arguments[1], [0x89, 0x50, 0x4E, 0x47]);
                return RecordingCommandRunner.Text(string.Empty);
            }
        };
        var platform = new LinuxComputerRunPlatform(runner);

        try
        {
            var bounds = platform.GetVirtualScreenBounds();
            platform.SaveScreenshotPng(bounds, output);

            await Assert.That(bounds.Width).IsEqualTo(800);
            await Assert.That(bounds.Height).IsEqualTo(600);
            await Assert.That(File.Exists(output)).IsTrue();
            await Assert.That(string.Join("|", runner.Invocations.Select(call => call.FileName)))
                .IsEqualTo("xdotool|gnome-screenshot");
        }
        finally
        {
            TryDelete(output);
        }
    }

    [Test]
    public async Task LinuxPlatform_UsesXrandrWhenXdotoolGeometryIsUnavailable()
    {
        var runner = new RecordingCommandRunner("xrandr")
        {
            OnRun = _ => RecordingCommandRunner.Text("Screen 0: minimum 8 x 8, current 1920 x 1080, maximum 32767 x 32767\n")
        };
        var platform = new LinuxComputerRunPlatform(runner);

        var bounds = platform.GetVirtualScreenBounds();

        await Assert.That(bounds.Width).IsEqualTo(1920);
        await Assert.That(bounds.Height).IsEqualTo(1080);
    }

    [Test]
    public async Task LinuxPlatform_CapturePngReadsTemporaryScreenshot()
    {
        var runner = new RecordingCommandRunner("grim")
        {
            OnRun = invocation =>
            {
                File.WriteAllBytes(invocation.Arguments[0], [1, 2, 3]);
                return RecordingCommandRunner.Text(string.Empty);
            }
        };
        var platform = new LinuxComputerRunPlatform(runner);

        var bytes = platform.CapturePng(new(0, 0, 10, 10));

        await Assert.That(string.Join(",", bytes)).IsEqualTo("1,2,3");
    }

    [Test]
    public async Task LinuxPlatform_PointerKeyboardAndScrollUseXdotool()
    {
        var runner = new RecordingCommandRunner("xdotool")
        {
            OnRun = invocation => invocation.ArgumentText switch
            {
                "getmouselocation --shell" => RecordingCommandRunner.Text("X=11\nY=22\nSCREEN=0\nWINDOW=1\n"),
                _ => RecordingCommandRunner.Text(string.Empty),
            }
        };
        var platform = new LinuxComputerRunPlatform(runner);

        platform.MoveCursor(3, 4);
        var position = platform.GetCursorPosition();
        platform.Click(MouseButton.Right, 2, TimeSpan.FromMilliseconds(15));
        platform.Scroll(-3);
        platform.PressHotkey([KeyboardInput.ControlKey, KeyboardInput.VKey]);
        platform.PressKey([KeyboardInput.ShiftKey, 0x41], TimeSpan.FromMilliseconds(1));

        await Assert.That(position.X).IsEqualTo(11);
        await Assert.That(position.Y).IsEqualTo(22);
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "mousemove --sync 3 4")).IsTrue();
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "click --repeat 2 --delay 15 3")).IsTrue();
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "click --repeat 3 5")).IsTrue();
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "key --clearmodifiers Control_L+v")).IsTrue();
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "keydown Shift_L")).IsTrue();
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "keyup Shift_L")).IsTrue();
    }

    [Test]
    public async Task LinuxPlatform_PasteUsesClipboardThenControlV()
    {
        var runner = new RecordingCommandRunner("wl-copy", "xdotool");
        var platform = new LinuxComputerRunPlatform(runner);

        platform.PasteText("hello");

        await Assert.That(runner.Invocations[0].FileName).IsEqualTo("wl-copy");
        await Assert.That(runner.Invocations[0].StandardInput).IsEqualTo("hello");
        await Assert.That(string.Join(" ", runner.Invocations[1].Arguments)).IsEqualTo("key --clearmodifiers Control_L+v");
    }

    [Test]
    public async Task LinuxPlatform_PasteFallsBackToXdotoolTypeWhenClipboardToolsAreMissing()
    {
        var runner = new RecordingCommandRunner("xdotool");
        var platform = new LinuxComputerRunPlatform(runner);

        platform.PasteText("hello");

        await Assert.That(runner.Invocations[0].ArgumentText).IsEqualTo("type --clearmodifiers --delay 0 -- hello");
    }

    [Test]
    public async Task LinuxPlatform_ListWindowsParsesWmctrlOutput()
    {
        var runner = new RecordingCommandRunner("wmctrl")
        {
            OnRun = _ => RecordingCommandRunner.Text("0x01200007  0 4242 host Terminal Window\n")
        };
        var platform = new LinuxComputerRunPlatform(runner);

        var windows = platform.ListWindows(5);

        await Assert.That(windows.Count).IsEqualTo(1);
        await Assert.That(windows[0].Handle).IsEqualTo(0x01200007);
        await Assert.That(windows[0].ProcessId).IsEqualTo(4242);
        await Assert.That(windows[0].Title).IsEqualTo("Terminal Window");
    }

    [Test]
    public async Task LinuxPlatform_ListWindowsFallsBackToXdotool()
    {
        var runner = new RecordingCommandRunner("xdotool")
        {
            OnRun = invocation => invocation.ArgumentText switch
            {
                "search --onlyvisible --name ." => RecordingCommandRunner.Text("100\n101\n"),
                "getwindowname 100" => RecordingCommandRunner.Text("Editor\n"),
                "getwindowpid 100" => RecordingCommandRunner.Text("500\n"),
                "getwindowname 101" => RecordingCommandRunner.Text("Browser\n"),
                "getwindowpid 101" => RecordingCommandRunner.Text("501\n"),
                _ => RecordingCommandRunner.Text(string.Empty),
            }
        };
        var platform = new LinuxComputerRunPlatform(runner);

        var windows = platform.ListWindows(1);

        await Assert.That(windows.Count).IsEqualTo(1);
        await Assert.That(windows[0].Handle).IsEqualTo(100);
        await Assert.That(windows[0].ProcessId).IsEqualTo(500);
        await Assert.That(windows[0].Title).IsEqualTo("Editor");
    }

    [Test]
    public async Task LinuxPlatform_MissingPointerDependencyThrowsActionableError()
    {
        var platform = new LinuxComputerRunPlatform(new RecordingCommandRunner());

        await Assert.That(() => platform.MoveCursor(1, 2))
            .Throws<PlatformNotSupportedException>()
            .WithMessageContaining("xdotool");
    }

    [Test]
    public async Task LinuxPlatform_FailedCommandIncludesStderr()
    {
        var runner = new RecordingCommandRunner("xdotool")
        {
            OnRun = _ => RecordingCommandRunner.Result(1, string.Empty, "display unavailable")
        };
        var platform = new LinuxComputerRunPlatform(runner);

        await Assert.That(() => platform.MoveCursor(1, 2))
            .Throws<InvalidOperationException>()
            .WithMessageContaining("display unavailable");
    }

    [Test]
    public async Task MacPlatform_ParsesFinderDesktopBounds()
    {
        var runner = new RecordingCommandRunner("osascript")
        {
            OnRun = _ => RecordingCommandRunner.Text("0, 0, 1440, 900\n")
        };
        var platform = new MacComputerRunPlatform(runner);

        var bounds = platform.GetVirtualScreenBounds();

        await Assert.That(bounds.Width).IsEqualTo(1440);
        await Assert.That(bounds.Height).IsEqualTo(900);
    }

    [Test]
    public async Task MacPlatform_CapturesScreenshotWithScreencapture()
    {
        var runner = new RecordingCommandRunner("screencapture")
        {
            OnRun = invocation =>
            {
                File.WriteAllBytes(invocation.Arguments[1], [4, 5, 6]);
                return RecordingCommandRunner.Text(string.Empty);
            }
        };
        var platform = new MacComputerRunPlatform(runner);

        var bytes = platform.CapturePng(new(0, 0, 10, 10));

        await Assert.That(string.Join(",", bytes)).IsEqualTo("4,5,6");
    }

    [Test]
    public async Task MacPlatform_PointerActionsUseCliclick()
    {
        var runner = new RecordingCommandRunner("cliclick")
        {
            OnRun = invocation => invocation.ArgumentText == "p"
                ? RecordingCommandRunner.Text("7,8\n")
                : RecordingCommandRunner.Text(string.Empty)
        };
        var platform = new MacComputerRunPlatform(runner);

        platform.MoveCursor(1, 2);
        var position = platform.GetCursorPosition();
        platform.Click(MouseButton.Left, 2, TimeSpan.Zero);
        platform.Scroll(4);

        await Assert.That(position.X).IsEqualTo(7);
        await Assert.That(position.Y).IsEqualTo(8);
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "m:1,2")).IsTrue();
        await Assert.That(runner.Invocations.Count(call => call.ArgumentText == "c:7,8")).IsEqualTo(2);
        await Assert.That(runner.Invocations.Any(call => call.ArgumentText == "w:0,-4")).IsTrue();
    }

    [Test]
    public async Task MacPlatform_PressHotkeyUsesAppleScriptModifiers()
    {
        var runner = new RecordingCommandRunner("osascript");
        var platform = new MacComputerRunPlatform(runner);

        platform.PressHotkey([0x5B, KeyboardInput.ShiftKey, 0x4C]);

        await Assert.That(runner.Invocations[0].Arguments[1])
            .Contains("key code 37 using {command down, shift down}");
    }

    [Test]
    public async Task MacPlatform_PasteUsesPbcopyAndCommandV()
    {
        var runner = new RecordingCommandRunner("pbcopy", "osascript");
        var platform = new MacComputerRunPlatform(runner);

        platform.PasteText("hello");

        await Assert.That(runner.Invocations[0].FileName).IsEqualTo("pbcopy");
        await Assert.That(runner.Invocations[0].StandardInput).IsEqualTo("hello");
        await Assert.That(runner.Invocations[1].Arguments[1]).Contains("key code 9 using {command down}");
    }

    [Test]
    public async Task MacPlatform_ListWindowsParsesAppleScriptOutput()
    {
        var runner = new RecordingCommandRunner("osascript")
        {
            OnRun = _ => RecordingCommandRunner.Text("123\tFinder\tDesktop\n")
        };
        var platform = new MacComputerRunPlatform(runner);

        var windows = platform.ListWindows(5);

        await Assert.That(windows.Count).IsEqualTo(1);
        await Assert.That(windows[0].ProcessId).IsEqualTo(123);
        await Assert.That(windows[0].ProcessName).IsEqualTo("Finder");
        await Assert.That(windows[0].Title).IsEqualTo("Desktop");
    }

    [Test]
    public async Task MacPlatform_MissingPointerDependencyThrowsActionableError()
    {
        var platform = new MacComputerRunPlatform(new RecordingCommandRunner("osascript", "screencapture", "pbcopy"));

        await Assert.That(() => platform.MoveCursor(1, 2))
            .Throws<PlatformNotSupportedException>()
            .WithMessageContaining("cliclick");
    }

    [Test]
    public async Task MacPlatform_MiddleClickThrowsStableUnsupportedError()
    {
        var platform = new MacComputerRunPlatform(new RecordingCommandRunner("cliclick"));

        await Assert.That(() => platform.Click(MouseButton.Middle, 1, TimeSpan.Zero))
            .Throws<PlatformNotSupportedException>()
            .WithMessageContaining("middle-click");
    }

    [Test]
    public async Task KeyboardFallbacks_MapCommonCharactersAndUnknownInput()
    {
        await Assert.That(KeyboardInputDefaults.KeyScan('a')).IsEqualTo((short)0x41);
        await Assert.That(KeyboardInputDefaults.KeyScan('A')).IsEqualTo((short)((1 << 8) | 0x41));
        await Assert.That(KeyboardInputDefaults.KeyScan('?')).IsEqualTo((short)((1 << 8) | 0xBF));
        await Assert.That(KeyboardInputDefaults.KeyScan('\u2603')).IsEqualTo((short)-1);
    }

    [Test]
    public async Task ExternalCommandRunner_DetectsAndRunsDotnetCommand()
    {
        var command = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var runner = new ExternalCommandRunner();

        var result = runner.Run(command, ["--version"], timeout: TimeSpan.FromSeconds(10));

        await Assert.That(runner.CommandExists(command)).IsTrue();
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.StandardOutputText.Trim()).IsNotEmpty();
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

    private sealed class RecordingCommandRunner(params string[] availableCommands) : IExternalCommandRunner
    {
        private readonly HashSet<string> _availableCommands = new(availableCommands, StringComparer.Ordinal);

        public Func<Invocation, ExternalCommandResult>? OnRun { get; init; }

        public List<Invocation> Invocations { get; } = [];

        public bool CommandExists(string fileName) => _availableCommands.Contains(fileName);

        public ExternalCommandResult Run(
            string fileName,
            IReadOnlyList<string> arguments,
            string? standardInput = null,
            TimeSpan? timeout = null)
        {
            var invocation = new Invocation(fileName, arguments.ToArray(), standardInput);
            Invocations.Add(invocation);
            return OnRun?.Invoke(invocation) ?? Text(string.Empty);
        }

        public static ExternalCommandResult Text(string text, int exitCode = 0)
            => Result(exitCode, text, string.Empty);

        public static ExternalCommandResult Result(int exitCode, string stdout, string stderr)
            => new(exitCode, Encoding.UTF8.GetBytes(stdout), stderr);
    }

    private sealed record Invocation(string FileName, string[] Arguments, string? StandardInput)
    {
        public string ArgumentText => string.Join(' ', Arguments);
    }
}
