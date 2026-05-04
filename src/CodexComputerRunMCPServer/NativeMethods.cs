using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides Win32 interop constants, structures, delegates, and native function imports
/// used for input simulation, clipboard operations, DPI awareness, and window enumeration.
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
    /// Clipboard format identifier for Unicode text (<c>CF_UNICODETEXT</c>).
    /// </summary>
    public const uint CfUnicodeText = 13;

    /// <summary>
    /// Movable global memory allocation flag (<c>GMEM_MOVEABLE</c>).
    /// </summary>
    public const uint GmemMoveable = 0x0002;

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
    /// Opens the clipboard for examination and modification.
    /// </summary>
    /// <param name="hWndNewOwner">Handle of the window opening the clipboard, or <see cref="IntPtr.Zero"/>.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenClipboard(IntPtr hWndNewOwner);

    /// <summary>
    /// Empties the clipboard and frees handles to data in the clipboard.
    /// </summary>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyClipboard();

    /// <summary>
    /// Places data on the clipboard in the specified format.
    /// </summary>
    /// <param name="format">Clipboard format identifier.</param>
    /// <param name="handle">Handle to data in global memory.</param>
    /// <returns>
    /// Handle to the data if successful; otherwise, <see cref="IntPtr.Zero"/>.
    /// </returns>
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetClipboardData(uint format, IntPtr handle);

    /// <summary>
    /// Closes the clipboard.
    /// </summary>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseClipboard();

    /// <summary>
    /// Allocates memory from the process default heap.
    /// </summary>
    /// <param name="flags">Allocation flags (for example, <see cref="GmemMoveable"/>).</param>
    /// <param name="bytes">Number of bytes to allocate.</param>
    /// <returns>
    /// Handle to the allocated memory block, or <see cref="IntPtr.Zero"/> on failure.
    /// </returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    /// <summary>
    /// Locks a global memory object and returns a pointer to the first byte.
    /// </summary>
    /// <param name="handle">Handle to the global memory object.</param>
    /// <returns>Pointer to the memory block, or <see cref="IntPtr.Zero"/> on failure.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalLock(IntPtr handle);

    /// <summary>
    /// Decrements the lock count associated with a global memory object.
    /// </summary>
    /// <param name="handle">Handle to the global memory object.</param>
    /// <returns><see langword="true"/> if successful; otherwise, <see langword="false"/>.</returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalUnlock(IntPtr handle);

    /// <summary>
    /// Frees the specified global memory object.
    /// </summary>
    /// <param name="handle">Handle to the global memory object.</param>
    /// <returns>
    /// <see cref="IntPtr.Zero"/> if successful; otherwise, the original handle.
    /// </returns>
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GlobalFree(IntPtr handle);

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
}
