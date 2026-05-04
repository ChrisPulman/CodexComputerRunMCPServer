using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Windows-specific implementation of <see cref="IComputerRunPlatform"/> that provides
/// desktop automation primitives such as screen capture, mouse/keyboard input, clipboard updates,
/// and top-level window enumeration through Win32 interop.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class WindowsComputerRunPlatform : IComputerRunPlatform
{
    /// <summary>
    /// Gets a value indicating whether the current process is running on Windows.
    /// </summary>
    public bool IsWindows => OperatingSystem.IsWindows();

    /// <summary>
    /// Gets the bounding rectangle that covers the entire virtual desktop across all monitors.
    /// </summary>
    /// <returns>
    /// A <see cref="Rectangle"/> representing the full virtual screen coordinates.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no screens are available from the current Windows session.
    /// </exception>
    public Rectangle GetVirtualScreenBounds()
    {
        var virtualScreen = SystemInformation.VirtualScreen;
        if (virtualScreen.Width > 0 && virtualScreen.Height > 0)
        {
            return virtualScreen;
        }

        var screens = Screen.AllScreens;
        if (screens.Length == 0)
        {
            return Screen.PrimaryScreen?.Bounds ?? throw new InvalidOperationException("No Windows screens are available.");
        }

        var bounds = screens[0].Bounds;
        for (var i = 1; i < screens.Length; i++)
        {
            bounds = Rectangle.Union(bounds, screens[i].Bounds);
        }

        return bounds;
    }

    /// <summary>
    /// Captures a region of the desktop and returns PNG bytes.
    /// </summary>
    /// <param name="bounds">The screen region to capture, in virtual desktop coordinates.</param>
    /// <returns>A byte array containing PNG-encoded image data.</returns>
    public byte[] CapturePng(Rectangle bounds)
    {
        using var stream = new MemoryStream(capacity: Math.Max(4096, bounds.Width * bounds.Height / 8));
        CapturePng(bounds, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Captures a region of the desktop and saves it as a PNG file.
    /// </summary>
    /// <param name="bounds">The screen region to capture, in virtual desktop coordinates.</param>
    /// <param name="path">The output file path for the PNG image.</param>
    public void SaveScreenshotPng(Rectangle bounds, string path)
    {
        using var stream = File.Create(path);
        CapturePng(bounds, stream);
    }

    /// <summary>
    /// Moves the mouse cursor to the specified virtual desktop coordinates.
    /// </summary>
    /// <param name="x">The target X coordinate.</param>
    /// <param name="y">The target Y coordinate.</param>
    /// <exception cref="Win32Exception">Thrown when the Win32 cursor operation fails.</exception>
    public void MoveCursor(int x, int y)
    {
        if (!NativeMethods.SetCursorPos(x, y))
        {
            ThrowLastWin32Error("SetCursorPos failed");
        }
    }

    /// <summary>
    /// Gets the current cursor position in virtual desktop coordinates.
    /// </summary>
    /// <returns>A <see cref="DesktopPoint"/> containing the current cursor coordinates.</returns>
    /// <exception cref="Win32Exception">Thrown when the Win32 cursor query fails.</exception>
    public DesktopPoint GetCursorPosition()
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            ThrowLastWin32Error("GetCursorPos failed");
        }

        return new DesktopPoint(point.X, point.Y);
    }

    /// <summary>
    /// Performs one or more mouse clicks using the specified button.
    /// </summary>
    /// <param name="button">The mouse button to click.</param>
    /// <param name="clicks">The number of clicks to send.</param>
    /// <param name="interval">The delay between clicks when <paramref name="clicks"/> is greater than one.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="button"/> is not a supported value.
    /// </exception>
    /// <exception cref="Win32Exception">Thrown when sending input fails.</exception>
    public void Click(MouseButton button, int clicks, TimeSpan interval)
    {
        var (down, up) = button switch
        {
            MouseButton.Left => (NativeMethods.MouseEventLeftDown, NativeMethods.MouseEventLeftUp),
            MouseButton.Right => (NativeMethods.MouseEventRightDown, NativeMethods.MouseEventRightUp),
            MouseButton.Middle => (NativeMethods.MouseEventMiddleDown, NativeMethods.MouseEventMiddleUp),
            _ => throw new ArgumentOutOfRangeException(nameof(button), button, "Unknown mouse button."),
        };

        var input = new[]
        {
            CreateMouseInput(down, 0),
            CreateMouseInput(up, 0),
        };

        for (var i = 0; i < clicks; i++)
        {
            SendInputs(input);
            if (i + 1 < clicks && interval > TimeSpan.Zero)
            {
                Thread.Sleep(interval);
            }
        }
    }

    /// <summary>
    /// Scrolls the mouse wheel by the specified number of detents.
    /// </summary>
    /// <param name="amount">
    /// Number of wheel steps to scroll. Positive values scroll forward/up; negative values scroll backward/down.
    /// </param>
    /// <exception cref="Win32Exception">Thrown when sending input fails.</exception>
    public void Scroll(int amount)
    {
        if (amount == 0)
        {
            return;
        }

        var wheelDelta = unchecked((uint)(amount * 120));
        SendInputs([CreateMouseInput(NativeMethods.MouseEventWheel, wheelDelta)]);
    }

    /// <summary>
    /// Presses a key chord and optionally holds it for a duration before releasing.
    /// </summary>
    /// <param name="keyChord">A sequence of virtual-key codes representing the chord.</param>
    /// <param name="duration">
    /// Hold duration. If zero or negative, keys are pressed and released immediately as a chord.
    /// </param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keyChord"/> is empty.</exception>
    /// <exception cref="Win32Exception">Thrown when sending input fails.</exception>
    public void PressKey(IReadOnlyList<byte> keyChord, TimeSpan duration)
    {
        if (keyChord.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(keyChord));
        }

        if (duration <= TimeSpan.Zero)
        {
            SendInputs(CreateKeyboardChordInputs(keyChord));
            return;
        }

        SendInputs(CreateKeyDownInputs(keyChord));
        Thread.Sleep(duration);
        SendInputs(CreateKeyUpInputs(keyChord));
    }

    /// <summary>
    /// Presses and releases a hotkey combination.
    /// </summary>
    /// <param name="virtualKeys">Ordered virtual-key codes for the hotkey combination.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="virtualKeys"/> is empty.</exception>
    /// <exception cref="Win32Exception">Thrown when sending input fails.</exception>
    public void PressHotkey(IReadOnlyList<byte> virtualKeys)
    {
        if (virtualKeys.Count == 0)
        {
            throw new ArgumentException("At least one key is required.", nameof(virtualKeys));
        }

        SendInputs(CreateKeyboardChordInputs(virtualKeys));
    }

    /// <summary>
    /// Replaces clipboard text with the supplied Unicode string.
    /// </summary>
    /// <param name="text">The text to place on the clipboard.</param>
    /// <exception cref="Win32Exception">
    /// Thrown when clipboard access, memory allocation, or clipboard update operations fail.
    /// </exception>
    public void SetClipboardText(string text)
    {
        var data = Encoding.Unicode.GetBytes(text + '\0');
        if (!TryOpenClipboard())
        {
            ThrowLastWin32Error("OpenClipboard failed");
        }

        IntPtr handle = IntPtr.Zero;
        var clipboardOwnsHandle = false;

        try
        {
            if (!NativeMethods.EmptyClipboard())
            {
                ThrowLastWin32Error("EmptyClipboard failed");
            }

            handle = NativeMethods.GlobalAlloc(NativeMethods.GmemMoveable, (UIntPtr)data.Length);
            if (handle == IntPtr.Zero)
            {
                ThrowLastWin32Error("GlobalAlloc failed");
            }

            var locked = NativeMethods.GlobalLock(handle);
            if (locked == IntPtr.Zero)
            {
                ThrowLastWin32Error("GlobalLock failed");
            }

            try
            {
                Marshal.Copy(data, 0, locked, data.Length);
            }
            finally
            {
                _ = NativeMethods.GlobalUnlock(handle);
            }

            if (NativeMethods.SetClipboardData(NativeMethods.CfUnicodeText, handle) == IntPtr.Zero)
            {
                ThrowLastWin32Error("SetClipboardData failed");
            }

            clipboardOwnsHandle = true;
        }
        finally
        {
            _ = NativeMethods.CloseClipboard();
            if (handle != IntPtr.Zero && !clipboardOwnsHandle)
            {
                _ = NativeMethods.GlobalFree(handle);
            }
        }
    }

    /// <summary>
    /// Enumerates visible top-level windows with non-empty titles.
    /// </summary>
    /// <param name="limit">Maximum number of windows to return.</param>
    /// <returns>A list of <see cref="WindowInfo"/> entries up to the specified limit.</returns>
    public IReadOnlyList<WindowInfo> ListWindows(int limit)
    {
        var windows = new List<WindowInfo>(Math.Min(limit, 128));
        var processNames = new Dictionary<int, string?>();

        NativeMethods.EnumWindows((hWnd, lParam) =>
        {
            if (windows.Count >= limit)
            {
                return false;
            }

            if (!NativeMethods.IsWindowVisible(hWnd))
            {
                return true;
            }

            var length = NativeMethods.GetWindowTextLength(hWnd);
            if (length <= 0)
            {
                return true;
            }

            var builder = new StringBuilder(length + 1);
            _ = NativeMethods.GetWindowText(hWnd, builder, builder.Capacity);
            var title = builder.ToString();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            _ = NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
            var pid = (int)processId;
            if (!processNames.TryGetValue(pid, out var processName))
            {
                processName = TryGetProcessName(pid);
                processNames[pid] = processName;
            }

            windows.Add(new WindowInfo(hWnd.ToInt64(), pid, processName, title));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Maps a character to a keyboard virtual-key value and shift-state as defined by Win32.
    /// </summary>
    /// <param name="character">The character to map.</param>
    /// <returns>The Win32 <c>VkKeyScan</c> result.</returns>
    public short KeyScan(char character) => NativeMethods.VkKeyScan(character);

    /// <summary>
    /// Captures the provided screen bounds and writes PNG-encoded image data to the output stream.
    /// </summary>
    /// <param name="bounds">The screen region to capture.</param>
    /// <param name="output">The destination stream for PNG data.</param>
    private static void CapturePng(Rectangle bounds, Stream output)
    {
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
        }

        bitmap.Save(output, ImageFormat.Png);
    }

    /// <summary>
    /// Creates input events for pressing all keys in order, then releasing them in reverse order.
    /// </summary>
    /// <param name="virtualKeys">Ordered virtual-key codes to include in the chord.</param>
    /// <returns>An array of keyboard input events suitable for <c>SendInput</c>.</returns>
    private static NativeMethods.Input[] CreateKeyboardChordInputs(IReadOnlyList<byte> virtualKeys)
    {
        var input = new NativeMethods.Input[virtualKeys.Count * 2];
        for (var i = 0; i < virtualKeys.Count; i++)
        {
            input[i] = CreateKeyboardInput(virtualKeys[i], keyUp: false);
        }

        for (var i = virtualKeys.Count - 1; i >= 0; i--)
        {
            input[virtualKeys.Count + (virtualKeys.Count - 1 - i)] = CreateKeyboardInput(virtualKeys[i], keyUp: true);
        }

        return input;
    }

    /// <summary>
    /// Creates keyboard input events that press keys without releasing them.
    /// </summary>
    /// <param name="virtualKeys">Ordered virtual-key codes to press.</param>
    /// <returns>An array of key-down events.</returns>
    private static NativeMethods.Input[] CreateKeyDownInputs(IReadOnlyList<byte> virtualKeys)
    {
        var input = new NativeMethods.Input[virtualKeys.Count];
        for (var i = 0; i < virtualKeys.Count; i++)
        {
            input[i] = CreateKeyboardInput(virtualKeys[i], keyUp: false);
        }

        return input;
    }

    /// <summary>
    /// Creates keyboard input events that release keys in reverse order.
    /// </summary>
    /// <param name="virtualKeys">Ordered virtual-key codes to release.</param>
    /// <returns>An array of key-up events.</returns>
    private static NativeMethods.Input[] CreateKeyUpInputs(IReadOnlyList<byte> virtualKeys)
    {
        var input = new NativeMethods.Input[virtualKeys.Count];
        for (var i = virtualKeys.Count - 1; i >= 0; i--)
        {
            input[virtualKeys.Count - 1 - i] = CreateKeyboardInput(virtualKeys[i], keyUp: true);
        }

        return input;
    }

    /// <summary>
    /// Creates a Win32 mouse input structure.
    /// </summary>
    /// <param name="flags">Mouse event flags.</param>
    /// <param name="mouseData">Additional mouse data such as wheel delta.</param>
    /// <returns>A populated <see cref="NativeMethods.Input"/> for mouse input.</returns>
    private static NativeMethods.Input CreateMouseInput(uint flags, uint mouseData)
        => new()
        {
            Type = NativeMethods.InputMouse,
            Anonymous = new NativeMethods.InputUnion
            {
                MouseInput = new NativeMethods.MouseInput
                {
                    MouseData = mouseData,
                    Flags = flags,
                },
            },
        };

    /// <summary>
    /// Creates a Win32 keyboard input structure.
    /// </summary>
    /// <param name="virtualKey">Virtual-key code.</param>
    /// <param name="keyUp"><see langword="true"/> to emit key-up; otherwise key-down.</param>
    /// <returns>A populated <see cref="NativeMethods.Input"/> for keyboard input.</returns>
    private static NativeMethods.Input CreateKeyboardInput(byte virtualKey, bool keyUp)
        => new()
        {
            Type = NativeMethods.InputKeyboard,
            Anonymous = new NativeMethods.InputUnion
            {
                KeyboardInput = new NativeMethods.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? NativeMethods.KeyEventKeyUp : 0,
                },
            },
        };

    /// <summary>
    /// Sends the provided input events through the Win32 <c>SendInput</c> API.
    /// </summary>
    /// <param name="inputs">Input events to inject.</param>
    /// <exception cref="Win32Exception">
    /// Thrown when the number of injected inputs does not match the requested count.
    /// </exception>
    private static void SendInputs(NativeMethods.Input[] inputs)
    {
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
        if (sent != inputs.Length)
        {
            ThrowLastWin32Error("SendInput failed");
        }
    }

    /// <summary>
    /// Attempts to open the clipboard with short retries to handle temporary lock contention.
    /// </summary>
    /// <returns><see langword="true"/> if the clipboard was opened; otherwise <see langword="false"/>.</returns>
    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (NativeMethods.OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(TimeSpan.FromMilliseconds(8));
        }

        return false;
    }

    /// <summary>
    /// Resolves a process name from a process ID.
    /// </summary>
    /// <param name="processId">The process identifier.</param>
    /// <returns>The process name when available; otherwise <see langword="null"/>.</returns>
    private static string? TryGetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Throws a <see cref="Win32Exception"/> using the current thread's last Win32 error code.
    /// </summary>
    /// <param name="message">Context message describing the failed operation.</param>
    private static void ThrowLastWin32Error(string message)
        => throw new Win32Exception(Marshal.GetLastWin32Error(), message);
}
