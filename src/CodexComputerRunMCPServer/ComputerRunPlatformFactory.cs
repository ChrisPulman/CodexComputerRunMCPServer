using System.Runtime.InteropServices;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Creates the desktop automation platform implementation for the current operating system.
/// </summary>
internal static class ComputerRunPlatformFactory
{
    /// <summary>
    /// Creates the best available platform implementation for the current process OS.
    /// </summary>
    /// <returns>A platform implementation for Windows, Linux, macOS, or a deterministic unsupported platform.</returns>
    public static IComputerRunPlatform CreateDefault()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsComputerRunPlatform();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxComputerRunPlatform();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacComputerRunPlatform();
        }

        return new UnsupportedComputerRunPlatform(RuntimeInformation.OSDescription);
    }
}

