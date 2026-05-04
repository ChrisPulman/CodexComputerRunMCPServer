using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace CodexComputerRunMCPServer.Tests;

public class McpIntegrationTests
{
    [Test]
    public async Task Host_CreatesMcpHostedService()
    {
        using var host = Program.CreateHost([]);
        var hostedServices = host.Services.GetServices<IHostedService>();

        await Assert.That(hostedServices.Any()).IsTrue();
    }

    [Test]
    public async Task ToolSurface_ExposesExpectedCodexDesktopTools()
    {
        var tools = typeof(ComputerRunTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Select(method => method.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        await Assert.That(tools.Length).IsEqualTo(9);
        await Assert.That(string.Join("|", tools)).IsEqualTo(
            "click|cursor_position|hotkey|list_windows|move_mouse|press_key|screenshot|scroll|type_text");
    }

    [Test]
    public async Task ToolSurface_HasDescriptionsForCodexToolDiscovery()
    {
        var missingDescriptions = typeof(ComputerRunTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
            .Where(method => method.GetCustomAttribute<DescriptionAttribute>() is null)
            .Select(method => method.Name)
            .ToArray();

        await Assert.That(missingDescriptions.Length).IsEqualTo(0);
    }

    [Test]
    public async Task StaticToolFacade_DelegatesToRuntimeService()
    {
        var service = new TestComputerRunService();
        using var restore = ComputerRunToolRuntime.ReplaceServiceForTests(service);

        _ = ComputerRunTools.move_mouse(1, 2);
        _ = ComputerRunTools.click(button: "middle");
        _ = ComputerRunTools.scroll();
        _ = ComputerRunTools.press_key("enter");
        _ = ComputerRunTools.hotkey("ctrl+l");
        _ = ComputerRunTools.type_text("abc");
        _ = ComputerRunTools.cursor_position();
        _ = ComputerRunTools.list_windows();
        _ = ComputerRunTools.screenshot(include_image: false);

        await Assert.That(service.Calls).IsEqualTo(9);
    }

    [Test]
    public async Task McpManifest_UsesNuGetPackageAndStdioTransport()
    {
        var manifestPath = Path.Combine(FindRepositoryRoot(), ".mcp", "server.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var package = root.GetProperty("packages")[0];

        await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("io.github.chrispulman/codex-computer-run-mcp-server");
        await Assert.That(package.GetProperty("identifier").GetString()).IsEqualTo("CP.CodexComputerRun.Mcp.Server");
        await Assert.That(package.GetProperty("transport").GetProperty("type").GetString()).IsEqualTo("stdio");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Readme.md")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class TestComputerRunService : IComputerRunService
    {
        public int Calls { get; private set; }

        public CallToolResult Screenshot(string? path, bool includeImage)
        {
            Calls++;
            return new CallToolResult { Content = [] };
        }

        public string MoveMouse(int x, int y, double? delay)
        {
            Calls++;
            return "move";
        }

        public string Click(int? x, int? y, string button, int clicks, double interval, double? delay)
        {
            Calls++;
            return "click";
        }

        public string Scroll(int amount, int? x, int? y, double? delay)
        {
            Calls++;
            return "scroll";
        }

        public string PressKey(string key, double duration, double? delay)
        {
            Calls++;
            return "press";
        }

        public string Hotkey(string keys, double? delay)
        {
            Calls++;
            return "hotkey";
        }

        public string TypeText(string text, double? delay)
        {
            Calls++;
            return "type";
        }

        public string CursorPosition()
        {
            Calls++;
            return "{}";
        }

        public string ListWindows(int limit)
        {
            Calls++;
            return "[]";
        }
    }
}
