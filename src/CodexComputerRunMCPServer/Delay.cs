namespace CodexComputerRunMCPServer;

internal static class Delay
{
    public static void Sleep(double? seconds)
    {
        if (seconds is > 0)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds.Value));
        }
    }
}
