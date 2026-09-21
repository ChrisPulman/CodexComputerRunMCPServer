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
        using var runtimeLock = await RuntimeMutationLock.AcquireAsync();
        using var host = Program.CreateHost([]);
        var hostedServices = host.Services.GetServices<IHostedService>();

        await Assert.That(hostedServices.Any()).IsTrue();
        await Assert.That(hostedServices.OfType<IdleShutdownService>().Any()).IsTrue();
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

        await Assert.That(tools.Length).IsEqualTo(31);
        await Assert.That(string.Join("|", tools)).IsEqualTo(
            "activate_window|click|close_window|copy_path|create_directory|cursor_position|delete_path|find_windows|git_clone|git_commit|git_create_branch|git_init|git_status|hotkey|launch_application|list_directory|list_processes|list_windows|move_mouse|move_path|open_url|press_key|read_text_file|screenshot|screenshot_window|scroll|type_text|verify_window|wait_for_process|wait_for_window|write_text_file");
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
        using var runtimeLock = await RuntimeMutationLock.AcquireAsync();
        var service = new TestComputerRunService();
        using var restore = ComputerRunToolRuntime.ReplaceServiceForTests(service);
        using var restoreLease = ComputerRunToolRuntime.ReplaceControlLeaseForTests(
            new DesktopControlLease(enabled: false, TimeSpan.Zero));

        _ = ComputerRunTools.move_mouse(1, 2);
        _ = ComputerRunTools.click(button: "middle");
        _ = ComputerRunTools.scroll();
        _ = ComputerRunTools.press_key("enter");
        _ = ComputerRunTools.hotkey("ctrl+l");
        _ = ComputerRunTools.type_text("abc");
        _ = ComputerRunTools.cursor_position();
        _ = ComputerRunTools.list_windows();
        _ = ComputerRunTools.find_windows(process_name: "notepad", title_contains: "untitled", foreground_only: false, include_minimized: true, limit: 10);
        _ = ComputerRunTools.activate_window(100, restore: false);
        _ = ComputerRunTools.screenshot(include_image: false);
        _ = ComputerRunTools.screenshot_window(100, include_image: false);
        _ = ComputerRunTools.verify_window(100, process_name: "notepad", title_contains: "untitled", require_foreground: true, allow_minimized: false);
        _ = ComputerRunTools.wait_for_window(process_name: "notepad", title_contains: "untitled", foreground_only: false, include_minimized: true, timeout_ms: 0, poll_ms: 25);
        _ = ComputerRunTools.close_window(100, timeout_ms: 0);
        _ = ComputerRunTools.list_directory(Path.GetTempPath(), max_entries: 1);
        _ = ComputerRunTools.read_text_file(Path.Combine(FindRepositoryRoot(), "README.md"), max_bytes: 128);
        _ = ComputerRunTools.write_text_file(Path.Combine(Path.GetTempPath(), "codex-computer-run-text-dry-run.txt"), "dry run", dry_run: true);
        _ = ComputerRunTools.create_directory(Path.Combine(Path.GetTempPath(), "codex-computer-run-dry-run"), dry_run: true);
        _ = ComputerRunTools.copy_path(AppContext.BaseDirectory, Path.Combine(Path.GetTempPath(), "codex-computer-run-copy-dry-run"), dry_run: true);
        _ = ComputerRunTools.move_path(AppContext.BaseDirectory, Path.Combine(Path.GetTempPath(), "codex-computer-run-move-dry-run"), dry_run: true);
        _ = ComputerRunTools.delete_path(Path.GetTempPath(), dry_run: true);
        var repositoryRoot = FindRepositoryRoot();
        _ = ComputerRunTools.git_status(repositoryRoot);
        _ = ComputerRunTools.git_init(Path.Combine(Path.GetTempPath(), "codex-computer-run-git-dry-run"), dry_run: true);
        _ = ComputerRunTools.git_clone("https://example.test/repo.git", Path.Combine(Path.GetTempPath(), "codex-computer-run-clone-dry-run"), dry_run: true);
        _ = ComputerRunTools.git_create_branch(repositoryRoot, "feature/codex-dry-run", dry_run: true);
        _ = ComputerRunTools.git_commit(repositoryRoot, "dry-run commit", dry_run: true);
        _ = ComputerRunTools.list_processes(process_name: "dotnet", limit: 1);
        _ = ComputerRunTools.wait_for_process(process_id: Environment.ProcessId, timeout_ms: 0, poll_ms: 25);
        _ = ComputerRunTools.launch_application("dotnet", ["--version"], dry_run: true);
        _ = ComputerRunTools.open_url("https://example.test/", dry_run: true);

        await Assert.That(service.Calls).IsEqualTo(15);
    }

    [Test]
    public async Task McpManifest_UsesNuGetPackageAndStdioTransport()
    {
        var manifestPath = Path.Combine(FindRepositoryRoot(), ".mcp", "server.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = manifest.RootElement;
        var package = root.GetProperty("packages")[0];

        await Assert.That(root.GetProperty("name").GetString()).IsEqualTo("io.github.chrispulman/codex-computer-run-mcp-server");
        await Assert.That(root.GetProperty("version").GetString()).IsEqualTo("1.2.0");
        await Assert.That(package.GetProperty("identifier").GetString()).IsEqualTo("CP.CodexComputerRun.Mcp.Server");
        await Assert.That(package.GetProperty("version").GetString()).IsEqualTo("1.2.0");
        await Assert.That(package.GetProperty("transport").GetProperty("type").GetString()).IsEqualTo("stdio");
    }

    [Test]
    public async Task Repository_IncludesBundledCodexSkill()
    {
        var skillDirectory = Path.Combine(FindRepositoryRoot(), "skills", CodexSkillInstaller.SkillName);
        var skillFile = Path.Combine(skillDirectory, "SKILL.md");
        var metadataFile = Path.Combine(skillDirectory, "agents", "openai.yaml");

        await Assert.That(File.Exists(skillFile)).IsTrue();
        await Assert.That(File.Exists(metadataFile)).IsTrue();
        await Assert.That(File.ReadAllText(skillFile)).Contains("name: codex-computer-run");
        await Assert.That(File.ReadAllText(metadataFile)).Contains("$codex-computer-run");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "README.md"))
                && File.Exists(Path.Combine(directory.FullName, "CodexComputerRunMCPServer.slnx"))
                && Directory.Exists(Path.Combine(directory.FullName, ".mcp"))
                && Directory.Exists(Path.Combine(directory.FullName, "skills")))
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

        public CallToolResult Screenshot(string? path, bool includeImage, System.Drawing.Rectangle? region)
        {
            Calls++;
            return new CallToolResult { Content = [] };
        }

        public string MoveMouse(int x, int y, double? delay, long? targetHandle)
        {
            Calls++;
            return "move";
        }

        public string Click(int? x, int? y, string button, int clicks, double interval, double? delay, long? targetHandle)
        {
            Calls++;
            return "click";
        }

        public string Scroll(int amount, int? x, int? y, double? delay, long? targetHandle)
        {
            Calls++;
            return "scroll";
        }

        public string PressKey(string key, double duration, double? delay, long? targetHandle)
        {
            Calls++;
            return "press";
        }

        public string Hotkey(string keys, double? delay, long? targetHandle)
        {
            Calls++;
            return "hotkey";
        }

        public string TypeText(string text, double? delay, long? targetHandle)
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

        public string FindWindows(string? processName, string? titleContains, bool foregroundOnly, bool includeMinimized, int limit)
        {
            Calls++;
            return "[]";
        }

        public CallToolResult ScreenshotWindow(long handle, string? path, bool includeImage)
        {
            Calls++;
            return new CallToolResult { Content = [] };
        }

        public string VerifyWindow(long handle, string? processName, string? titleContains, bool requireForeground, bool allowMinimized)
        {
            Calls++;
            return "{}";
        }

        public string WaitForWindow(string? processName, string? titleContains, bool foregroundOnly, bool includeMinimized, int timeoutMilliseconds, int pollMilliseconds)
        {
            Calls++;
            return "{}";
        }

        public string ActivateWindow(long handle, bool restore)
        {
            Calls++;
            return "activate";
        }

        public string CloseWindow(long handle, int timeoutMilliseconds)
        {
            Calls++;
            return "close";
        }
    }
}
