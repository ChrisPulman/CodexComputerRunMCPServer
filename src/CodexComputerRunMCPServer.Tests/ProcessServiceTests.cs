using System.Diagnostics;
using System.Text.Json;

namespace CodexComputerRunMCPServer.Tests;

public class ProcessServiceTests
{
    [Test]
    public async Task ProcessObservationFindsTheCurrentTestProcess()
    {
        using var current = Process.GetCurrentProcess();

        var listed = JsonDocument.Parse(ProcessService.ListProcesses(current.ProcessName, 10)).RootElement;
        var waited = JsonDocument.Parse(ProcessService.WaitForProcess(null, current.Id, 0, 25)).RootElement;

        await Assert.That(listed.GetProperty("entries").GetArrayLength()).IsGreaterThan(0);
        await Assert.That(waited.GetProperty("found").GetBoolean()).IsTrue();
        await Assert.That(waited.GetProperty("process").GetProperty("processId").GetInt32()).IsEqualTo(current.Id);
    }

    [Test]
    public async Task LaunchAndOpenUrlDefaultToDryRun()
    {
        var workingDirectory = Path.GetTempPath();
        var launch = JsonDocument.Parse(ProcessService.Launch("dotnet", ["--version"], workingDirectory, dryRun: true)).RootElement;
        var url = JsonDocument.Parse(ProcessService.OpenUrl("https://example.test/path", dryRun: true)).RootElement;

        await Assert.That(launch.GetProperty("launched").GetBoolean()).IsFalse();
        await Assert.That(launch.GetProperty("arguments")[0].GetString()).IsEqualTo("--version");
        await Assert.That(launch.GetProperty("processIdIsBestEffort").GetBoolean()).IsTrue();
        await Assert.That(url.GetProperty("url").GetString()).IsEqualTo("https://example.test/path");
        await Assert.That(url.GetProperty("opened").GetBoolean()).IsFalse();
        await Assert.That(url.GetProperty("processIdIsBestEffort").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task OpenUrlRejectsNonWebSchemes()
    {
        await Assert.That(() => ProcessService.OpenUrl("file:///C:/secret.txt", dryRun: true))
            .Throws<ArgumentException>()
            .WithMessageContaining("http and https");
    }
}
