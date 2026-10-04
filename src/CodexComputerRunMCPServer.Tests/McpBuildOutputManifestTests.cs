using System.Text.Json;

namespace CodexComputerRunMCPServer.Tests;

public class McpBuildOutputManifestTests
{
    /// <summary>Verifies that the generated build-output manifest is BOM-free JSON with matching package metadata.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildOutputManifest_HasValidUtf8JsonAndMatchingPackageMetadata()
    {
        var manifestPath = Path.Combine(AppContext.BaseDirectory, ".mcp", "server.json");
        var manifestBytes = await File.ReadAllBytesAsync(manifestPath);

        var hasUtf8Bom = manifestBytes.Length >= 3
            && manifestBytes[0] == 0xEF
            && manifestBytes[1] == 0xBB
            && manifestBytes[2] == 0xBF;

        await Assert.That(hasUtf8Bom).IsFalse();

        using var manifest = JsonDocument.Parse(manifestBytes);
        var root = manifest.RootElement;
        var package = root.GetProperty("packages")[0];
        var manifestVersion = root.GetProperty("version").GetString();
        var packageVersion = package.GetProperty("version").GetString();

        await Assert.That(package.GetProperty("identifier").GetString())
            .IsEqualTo("CP.CodexComputerRun.Mcp.Server");
        await Assert.That(manifestVersion).IsNotNull();
        await Assert.That(packageVersion).IsNotNull();
        await Assert.That(manifestVersion).IsEqualTo(packageVersion);
    }
}
