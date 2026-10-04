using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides Win32 interop constants, structures, delegates, and native function imports
/// used for input simulation, DPI awareness, and window enumeration.
/// </summary>
/// <remarks>
/// This type is marked as excluded from code coverage because it contains platform invoke declarations.
/// </remarks>
[ExcludeFromCodeCoverage]
internal static partial class NativeMethods
{
    /// <summary>
    /// Indicates a mouse input record for <c>INPUT.Type</c>.
    /// </summary>
    public const uint InputMouse = 0;

    /// <summary>
    /// Indicates a keyboard input record for <c>INPUT.Type</c>.
    /// </summary>
    public const uint InputKeyboard = 1;

    /// <summary>
    /// Mouse left button down event flag.
    /// </summary>
    public const uint MouseEventLeftDown = 0x0002;

    /// <summary>
    /// Mouse left button up event flag.
    /// </summary>
    public const uint MouseEventLeftUp = 0x0004;

    /// <summary>
    /// Mouse right button down event flag.
    /// </summary>
    public const uint MouseEventRightDown = 0x0008;

    /// <summary>
    /// Mouse right button up event flag.
    /// </summary>
    public const uint MouseEventRightUp = 0x0010;

    /// <summary>
    /// Mouse middle button down event flag.
    /// </summary>
    public const uint MouseEventMiddleDown = 0x0020;

    /// <summary>
    /// Mouse middle button up event flag.
    /// </summary>
    public const uint MouseEventMiddleUp = 0x0040;

    /// <summary>
    /// Mouse wheel event flag.
    /// </summary>
    public const uint MouseEventWheel = 0x0800;

    /// <summary>
    /// Keyboard key-up event flag.
    /// </summary>
    public const uint KeyEventKeyUp = 0x0002;

    /// <summary>
    /// Keyboard Unicode scan-code event flag.
    /// </summary>
    public const uint KeyEventUnicode = 0x0004;

    /// <summary>
    /// X coordinate of the virtual screen.
    /// </summary>
    public const int SystemMetricVirtualScreenLeft = 76;

    /// <summary>
    /// Y coordinate of the virtual screen.
    /// </summary>
    public const int SystemMetricVirtualScreenTop = 77;

    /// <summary>
    /// Width of the virtual screen.
    /// </summary>
    public const int SystemMetricVirtualScreenWidth = 78;

    /// <summary>
    /// Height of the virtual screen.
    /// </summary>
    public const int SystemMetricVirtualScreenHeight = 79;

    /// <summary>
    /// Show-window command that restores a minimized window.
    /// </summary>
    public const int ShowWindowRestore = 9;

    /// <summary>Window message requesting a graceful close.</summary>
    public const uint WindowMessageClose = 0x0010;

    /// <summary>
    /// Callback delegate used by <see cref="EnumWindows"/> to enumerate top-level windows.
    /// </summary>
    /// <param name="hWnd">Handle to the current top-level window.</param>
    /// <param name="lParam">Application-defined value passed to <see cref="EnumWindows"/>.</param>
    /// <returns>
    /// <see langword="true"/> to continue enumeration; <see langword="false"/> to stop.
    /// </returns>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    /// <summary>
    /// Represents a two-dimensional point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        /// <summary>
        /// Horizontal coordinate.
        /// </summary>
        public int X;

        /// <summary>
        /// Vertical coordinate.
        /// </summary>
        public int Y;
    }

    /// <summary>
    /// Represents the screen-space rectangle returned by Win32 for a top-level window.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        /// <summary>
        /// Left edge in virtual desktop coordinates.
        /// </summary>
        public int Left;

        /// <summary>
        /// Top edge in virtual desktop coordinates.
        /// </summary>
        public int Top;

        /// <summary>
        /// Right edge in virtual desktop coordinates.
        /// </summary>
        public int Right;

        /// <summary>
        /// Bottom edge in virtual desktop coordinates.
        /// </summary>
        public int Bottom;
    }

    /// <summary>
    /// Represents an input event passed to <see cref="SendInput"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        /// <summary>
        /// Input type (for example, <see cref="InputMouse"/> or <see cref="InputKeyboard"/>).
        /// </summary>
        public uint Type;

        /// <summary>
        /// Union payload containing mouse or keyboard input data.
        /// </summary>
        public InputUnion Anonymous;
    }

    /// <summary>
    /// Union for mouse and keyboard input payloads.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        /// <summary>
        /// Mouse input payload.
        /// </summary>
        [FieldOffset(0)]
        public MouseInput MouseInput;

        /// <summary>
        /// Keyboard input payload.
        /// </summary>
        [FieldOffset(0)]
        public KeyboardInput KeyboardInput;
    }

    /// <summary>
    /// Represents mouse input data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        /// <summary>
        /// Absolute or relative x-coordinate, depending on <see cref="Flags"/>.
        /// </summary>
        public int X;

        /// <summary>
        /// Absolute or relative y-coordinate, depending on <see cref="Flags"/>.
        /// </summary>
        public int Y;

        /// <summary>
        /// Additional mouse data, such as wheel delta.
        /// </summary>
        public uint MouseData;

        /// <summary>
        /// Event flags describing the mouse action.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// Timestamp for the event, in milliseconds. Zero lets the system provide a timestamp.
        /// </summary>
        public uint Time;

        /// <summary>
        /// Additional application-defined information.
        /// </summary>
        public UIntPtr ExtraInfo;
    }

    /// <summary>
    /// Represents keyboard input data.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        /// <summary>
        /// Virtual-key code.
        /// </summary>
        public ushort VirtualKey;

        /// <summary>
        /// Hardware scan code.
        /// </summary>
        public ushort Scan;

        /// <summary>
        /// Event flags describing the keystroke.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// Timestamp for the event, in milliseconds. Zero lets the system provide a timestamp.
        /// </summary>
        public uint Time;

        /// <summary>
        /// Additional application-defined information.
        /// </summary>
        public UIntPtr ExtraInfo;
    }

    /// <summary>
    /// Attempts to enable per-monitor DPI awareness (v2 context) for the current process.
    /// </summary>
    /// <remarks>
    /// The call is skipped on operating systems earlier than Windows 10 build 14393.
    /// Failure is ignored because DPI awareness is an optional runtime enhancement.
    /// </remarks>
    public static void TryEnablePerMonitorDpiAwareness()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393))
        {
            return;
        }

        _ = SetProcessDpiAwarenessContext(new IntPtr(-4));
    }

    /// <summary>
    /// Sets the process-default DPI awareness context.
    /// </summary>
    /// <param name="dpiContext">Pointer value identifying the DPI awareness context.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    /// <summary>
    /// Synthesizes keyboard or mouse input events.
    /// </summary>
    /// <param name="inputCount">Number of elements in <paramref name="inputs"/>.</param>
    /// <param name="inputs">Input events to inject.</param>
    /// <param name="inputSize">Size, in bytes, of one <see cref="Input"/> structure.</param>
    /// <returns>The number of events successfully inserted into the input stream.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint inputCount, [In] Input[] inputs, int inputSize);

    /// <summary>
    /// Moves the cursor to the specified screen coordinates.
    /// </summary>
    /// <param name="x">Target x-coordinate in screen space.</param>
    /// <param name="y">Target y-coordinate in screen space.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    /// <summary>
    /// Retrieves the current cursor position in screen coordinates.
    /// </summary>
    /// <param name="point">Receives the cursor coordinates.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out Point point);

    /// <summary>
    /// Retrieves the specified system metric or system configuration setting.
    /// </summary>
    /// <param name="index">System metric index.</param>
    /// <returns>The requested system metric value.</returns>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int index);

    /// <summary>
    /// Translates a character to the corresponding virtual-key code and shift state.
    /// </summary>
    /// <param name="ch">Character to translate.</param>
    /// <returns>
    /// A packed value containing virtual-key code and shift state, or <c>-1</c> if no translation exists.
    /// </returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern short VkKeyScan(char ch);

    /// <summary>
    /// Enumerates all top-level windows on the screen.
    /// </summary>
    /// <param name="lpEnumFunc">Callback invoked for each window handle.</param>
    /// <param name="lParam">Application-defined value passed to the callback.</param>
    /// <returns><see langword="true"/> if enumeration completes; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    /// <summary>
    /// Determines whether the specified window is visible.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <returns><see langword="true"/> if the window is visible; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    /// <summary>
    /// Determines whether a native window handle is valid.
    /// </summary>
    /// <param name="hWnd">Handle to validate.</param>
    /// <returns><see langword="true"/> when the handle identifies a window.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    /// <summary>
    /// Shows or hides a window according to the supplied command.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <param name="nCmdShow">Show-window command.</param>
    /// <returns><see langword="true"/> when the window was previously visible.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    /// <summary>
    /// Brings a window to the foreground.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <returns><see langword="true"/> when the window was brought to the foreground.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Brings a window to the top of the Z order without changing its size or position.
    /// </summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    /// <summary>Sets the active window for the calling thread.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr SetActiveWindow(IntPtr hWnd);

    /// <summary>Returns the identifier of the calling thread.</summary>
    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    /// <summary>Temporarily shares input state between two GUI threads.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool attach);

    /// <summary>Posts a message to a window without changing the foreground window.</summary>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Gets the length, in characters, of the specified window's title text.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <returns>The title length in characters, excluding the terminating null character.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    /// <summary>
    /// Copies the specified window's title text into a buffer.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <param name="lpString">String builder buffer that receives the title text.</param>
    /// <param name="nMaxCount">Maximum number of characters to copy, including the null terminator.</param>
    /// <returns>The number of characters copied, excluding the null terminator.</returns>
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    /// <summary>
    /// Retrieves the identifier of the process that created the specified window.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <param name="processId">When this method returns, contains the process identifier.</param>
    /// <returns>The identifier of the thread that created the window.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    /// <summary>
    /// Retrieves the handle of the foreground window.
    /// </summary>
    /// <returns>The handle of the window receiving user input, or zero when unavailable.</returns>
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// Determines whether a window is minimized.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <returns><see langword="true"/> when the window is minimized.</returns>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    /// <summary>
    /// Retrieves the screen-space bounding rectangle of a window.
    /// </summary>
    /// <param name="hWnd">Handle to the window.</param>
    /// <param name="rect">Receives the window rectangle.</param>
    /// <returns><see langword="true"/> when the rectangle was retrieved.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out Rect rect);
}
