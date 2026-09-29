using System.Text.Json;

namespace CodexComputerRunMCPServer;

/// <summary>
/// Provides local Git workflows without invoking a shell. Mutating operations default
/// to dry-run plans and use an argument list so paths and messages are not shell-parsed.
/// </summary>
internal sealed class GitService(IExternalCommandRunner commandRunner)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(60);

    public static GitService CreateDefault() => new(new ExternalCommandRunner());

    public string Status(string repositoryPath)
    {
        var repository = ResolveExistingDirectory(repositoryPath);
        var result = RunGit(repository, ["status", "--short", "--branch", "--untracked-files=all"]);
        return JsonSerializer.Serialize(new
        {
            repository,
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.StandardOutputText,
            error = result.StandardError,
        }, JsonOptions);
    }

    public string Init(string repositoryPath, bool bare, bool dryRun)
    {
        var repository = ResolvePath(repositoryPath);
        if (File.Exists(repository))
        {
            throw new IOException($"A file already exists at the repository path: {repository}");
        }

        var alreadyInitialized = Directory.Exists(Path.Combine(repository, ".git"))
            || bare && File.Exists(Path.Combine(repository, "HEAD")) && Directory.Exists(Path.Combine(repository, "objects"));
        if (!dryRun)
        {
            var parent = Path.GetDirectoryName(repository);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            var result = RunGit(null, bare ? ["init", "--bare", repository] : ["init", repository]);
            EnsureSuccess(result, "git init");
        }

        return JsonSerializer.Serialize(new
        {
            operation = "git_init",
            repository,
            bare,
            dryRun,
            alreadyInitialized,
            changed = !dryRun && !alreadyInitialized,
        }, JsonOptions);
    }

    public string Clone(string url, string destination, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("A non-empty clone URL or local source is required.", nameof(url));
        }

        var target = ResolvePath(destination);
        if (File.Exists(target) || Directory.Exists(target))
        {
            throw new IOException($"Clone destination already exists: {target}");
        }

        if (!dryRun)
        {
            var result = RunGit(null, ["clone", url.Trim(), target]);
            EnsureSuccess(result, "git clone");
        }

        return JsonSerializer.Serialize(new
        {
            operation = "git_clone",
            url = url.Trim(),
            destination = target,
            dryRun,
            changed = !dryRun,
        }, JsonOptions);
    }

    public string CreateBranch(string repositoryPath, string branch, bool checkout, bool dryRun)
    {
        var repository = ResolveExistingDirectory(repositoryPath);
        var normalizedBranch = branch?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedBranch))
        {
            throw new ArgumentException("A non-empty branch name is required.", nameof(branch));
        }

        var validation = RunGit(repository, ["check-ref-format", "--branch", normalizedBranch]);
        EnsureSuccess(validation, "git check-ref-format");

        if (!dryRun)
        {
            var arguments = checkout
                ? new[] { "switch", "-c", normalizedBranch }
                : new[] { "branch", normalizedBranch };
            var result = RunGit(repository, arguments);
            EnsureSuccess(result, checkout ? "git switch -c" : "git branch");
        }

        return JsonSerializer.Serialize(new
        {
            operation = "git_create_branch",
            repository,
            branch = normalizedBranch,
            checkout,
            dryRun,
            changed = !dryRun,
        }, JsonOptions);
    }

    public string Commit(string repositoryPath, string message, bool stageAll, bool dryRun)
    {
        var repository = ResolveExistingDirectory(repositoryPath);
        var normalizedMessage = message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedMessage))
        {
            throw new ArgumentException("A non-empty commit message is required.", nameof(message));
        }

        var status = RunGit(repository, ["status", "--short", "--untracked-files=all"]);
        EnsureSuccess(status, "git status");
        if (!dryRun)
        {
            if (stageAll)
            {
                var add = RunGit(repository, ["add", "--all"]);
                EnsureSuccess(add, "git add --all");
            }

            var commit = RunGit(repository, ["commit", "-m", normalizedMessage]);
            EnsureSuccess(commit, "git commit");
        }

        return JsonSerializer.Serialize(new
        {
            operation = "git_commit",
            repository,
            message = normalizedMessage,
            stageAll,
            dryRun,
            changed = !dryRun,
            status = status.StandardOutputText,
        }, JsonOptions);
    }

    private ExternalCommandResult RunGit(string? repository, IReadOnlyList<string> arguments)
    {
        if (!commandRunner.CommandExists("git") && !commandRunner.CommandExists("git.exe"))
        {
            throw new PlatformNotSupportedException("Git is not available on PATH.");
        }

        var executable = commandRunner.CommandExists("git") ? "git" : "git.exe";
        var fullArguments = repository is null
            ? arguments.ToArray()
            : ["-C", repository, .. arguments];
        return commandRunner.Run(executable, fullArguments, timeout: GitTimeout);
    }

    private static void EnsureSuccess(ExternalCommandResult result, string operation)
    {
        if (result.ExitCode == 0)
        {
            return;
        }

        var detail = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutputText.Trim()
            : result.StandardError.Trim();
        throw new InvalidOperationException($"{operation} failed: {detail}");
    }

    private static string ResolveExistingDirectory(string path)
    {
        var resolved = ResolvePath(path);
        if (!Directory.Exists(resolved))
        {
            throw new DirectoryNotFoundException($"Repository directory not found: {resolved}");
        }

        return resolved;
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A non-empty path is required.", nameof(path));
        }

        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim()));
    }
}
