using System.Text;
using System.Text.Json;

namespace CodexComputerRunMCPServer.Tests;

public class GitServiceTests
{
    [Test]
    public async Task MutatingGitOperationsDefaultToPlansAndUseArgumentLists()
    {
        var runner = new RecordingGitRunner();
        var service = new GitService(runner);
        var root = Path.Combine(Path.GetTempPath(), "codex-computer-run-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var initPlan = JsonDocument.Parse(service.Init(Path.Combine(root, "repo"), bare: false, dryRun: true)).RootElement;
            await Assert.That(initPlan.GetProperty("dryRun").GetBoolean()).IsTrue();
            await Assert.That(runner.Invocations).IsEmpty();

            Directory.CreateDirectory(Path.Combine(root, "repo"));
            var branchPlan = JsonDocument.Parse(service.CreateBranch(Path.Combine(root, "repo"), "feature/test", checkout: false, dryRun: true)).RootElement;
            await Assert.That(branchPlan.GetProperty("changed").GetBoolean()).IsFalse();
            await Assert.That(runner.Invocations.Single().Arguments).Contains("check-ref-format");

            _ = service.Commit(Path.Combine(root, "repo"), "safe message", stageAll: true, dryRun: true);
            await Assert.That(runner.Invocations.Last().Arguments).Contains("status");

            _ = service.Clone("https://example.test/repo.git", Path.Combine(root, "clone"), dryRun: true);
            await Assert.That(runner.Invocations.Count).IsEqualTo(2);
            await Assert.That(runner.Invocations[0].Arguments[^1]).IsEqualTo("feature/test");
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Test]
    public async Task GitCommitAppliesStageAndMessageAsSeparateArguments()
    {
        var runner = new RecordingGitRunner();
        var service = new GitService(runner);
        var root = Path.Combine(Path.GetTempPath(), "codex-computer-run-git", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var result = JsonDocument.Parse(service.Commit(root, "message with spaces", stageAll: true, dryRun: false)).RootElement;

            await Assert.That(result.GetProperty("changed").GetBoolean()).IsTrue();
            await Assert.That(string.Join("|", runner.Invocations.Select(invocation => invocation.Arguments[2])))
                .IsEqualTo("status|add|commit");
            await Assert.That(string.Join("|", runner.Invocations.Last().Arguments))
                .IsEqualTo($"-C|{Path.GetFullPath(root)}|commit|-m|message with spaces");
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Test cleanup only.
        }
    }

    private sealed class RecordingGitRunner : IExternalCommandRunner
    {
        public List<Invocation> Invocations { get; } = [];

        public bool CommandExists(string fileName) => fileName is "git" or "git.exe";

        public ExternalCommandResult Run(string fileName, IReadOnlyList<string> arguments, string? standardInput = null, TimeSpan? timeout = null)
        {
            Invocations.Add(new Invocation(fileName, arguments.ToArray(), standardInput));
            return new ExternalCommandResult(0, Encoding.UTF8.GetBytes("On branch main\n"), string.Empty);
        }
    }

    private sealed record Invocation(string FileName, string[] Arguments, string? StandardInput);
}
