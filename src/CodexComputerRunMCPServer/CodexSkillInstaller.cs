namespace CodexComputerRunMCPServer;

/// <summary>
/// Installs the bundled Codex skill into a user's Codex skills directory.
/// </summary>
internal static class CodexSkillInstaller
{
    public const string SkillName = "codex-computer-run";

    private const string InstallArgument = "--install-codex-skill";
    private const string ForceArgument = "--force";

    public static bool IsInstallRequested(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, InstallArgument, StringComparison.OrdinalIgnoreCase));

    public static bool IsForceRequested(IEnumerable<string> args)
        => args.Any(arg => string.Equals(arg, ForceArgument, StringComparison.OrdinalIgnoreCase));

    public static SkillInstallResult InstallBundledSkill(bool createCodexHome, bool overwrite)
    {
        var sourceSkillDirectory = FindBundledSkillDirectory();
        if (sourceSkillDirectory is null)
        {
            return SkillInstallResult.Failure("The bundled codex-computer-run skill directory was not found.");
        }

        var codexHome = ResolveCodexHome(createCodexHome);
        if (codexHome is null)
        {
            return SkillInstallResult.SkippedWith("Codex home was not found. Set CODEX_HOME or create %USERPROFILE%\\.codex.");
        }

        return Install(sourceSkillDirectory, codexHome, overwrite);
    }

    public static SkillInstallResult TryAutoInstall(TextWriter diagnostics)
    {
        var result = InstallBundledSkill(createCodexHome: false, overwrite: false);
        if (result.Installed)
        {
            diagnostics.WriteLine(result.Message);
        }
        else if (!result.Success && !result.Skipped)
        {
            diagnostics.WriteLine($"Codex skill auto-install skipped: {result.Message}");
        }

        return result;
    }

    public static SkillInstallResult Install(string sourceSkillDirectory, string codexHome, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSkillDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(codexHome);

        if (!Directory.Exists(sourceSkillDirectory))
        {
            return SkillInstallResult.Failure($"Source skill directory does not exist: {sourceSkillDirectory}");
        }

        if (!File.Exists(Path.Combine(sourceSkillDirectory, "SKILL.md")))
        {
            return SkillInstallResult.Failure($"Source skill directory is missing SKILL.md: {sourceSkillDirectory}");
        }

        var targetSkillDirectory = Path.Combine(codexHome, "skills", SkillName);
        if (AreSameDirectory(sourceSkillDirectory, targetSkillDirectory))
        {
            return SkillInstallResult.SkippedWith($"Codex skill already points at {targetSkillDirectory}");
        }

        Directory.CreateDirectory(targetSkillDirectory);

        var copiedFiles = 0;
        var skippedFiles = 0;
        foreach (var sourceFile in Directory.EnumerateFiles(sourceSkillDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourceSkillDirectory, sourceFile);
            var targetFile = Path.Combine(targetSkillDirectory, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            if (File.Exists(targetFile) && !overwrite)
            {
                skippedFiles++;
                continue;
            }

            File.Copy(sourceFile, targetFile, overwrite: true);
            copiedFiles++;
        }

        if (copiedFiles == 0 && skippedFiles > 0)
        {
            return SkillInstallResult.SkippedWith($"Codex skill already installed at {targetSkillDirectory}");
        }

        var verb = overwrite ? "Installed or updated" : "Installed";
        return SkillInstallResult.InstalledAt($"{verb} Codex skill at {targetSkillDirectory}", targetSkillDirectory);
    }

    private static string? ResolveCodexHome(bool create)
    {
        var configuredHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        var codexHome = !string.IsNullOrWhiteSpace(configuredHome)
            ? configuredHome
            : GetDefaultCodexHome();

        if (string.IsNullOrWhiteSpace(codexHome))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(codexHome));
        if (!Directory.Exists(fullPath))
        {
            if (!create)
            {
                return null;
            }

            Directory.CreateDirectory(fullPath);
        }

        return fullPath;
    }

    private static string? GetDefaultCodexHome()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrWhiteSpace(userProfile) ? null : Path.Combine(userProfile, ".codex");
    }

    private static string? FindBundledSkillDirectory()
    {
        foreach (var root in CandidateRoots())
        {
            var candidate = Path.Combine(root, "skills", SkillName);
            if (File.Exists(Path.Combine(candidate, "SKILL.md")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateRoots()
    {
        yield return AppContext.BaseDirectory;
        yield return Environment.CurrentDirectory;

        foreach (var root in SiblingAnyDirectories(AppContext.BaseDirectory))
        {
            yield return root;
        }

        foreach (var root in Ancestors(AppContext.BaseDirectory))
        {
            yield return root;
        }

        foreach (var root in Ancestors(Environment.CurrentDirectory))
        {
            yield return root;
        }
    }

    private static IEnumerable<string> SiblingAnyDirectories(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            yield return Path.Combine(directory.FullName, "any");
            directory = directory.Parent;
        }
    }

    private static IEnumerable<string> Ancestors(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory.Parent is not null)
        {
            directory = directory.Parent;
            yield return directory.FullName;
        }
    }

    private static bool AreSameDirectory(string left, string right)
    {
        var normalizedLeft = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRight = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed record SkillInstallResult(bool Success, bool Installed, bool Skipped, string Message, string? TargetDirectory)
{
    public static SkillInstallResult InstalledAt(string message, string targetDirectory)
        => new(Success: true, Installed: true, Skipped: false, message, targetDirectory);

    public static SkillInstallResult SkippedWith(string message)
        => new(Success: true, Installed: false, Skipped: true, message, TargetDirectory: null);

    public static SkillInstallResult Failure(string message)
        => new(Success: false, Installed: false, Skipped: false, message, TargetDirectory: null);
}
