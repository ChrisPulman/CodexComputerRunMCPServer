namespace CodexComputerRunMCPServer.Tests;

public class CodexSkillInstallerTests
{
    private static readonly object EnvironmentLock = new();

    [Test]
    public async Task Arguments_DetectInstallAndForceFlags()
    {
        await Assert.That(CodexSkillInstaller.IsInstallRequested(["--install-codex-skill"])).IsTrue();
        await Assert.That(CodexSkillInstaller.IsInstallRequested(["--other"])).IsFalse();
        await Assert.That(CodexSkillInstaller.IsForceRequested(["--install-codex-skill", "--force"])).IsTrue();
    }

    [Test]
    public async Task InstallBundledSkill_UsesCodexHomeEnvironment()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var codexHome = Path.Combine(tempRoot, ".codex");
            SkillInstallResult result;
            lock (EnvironmentLock)
            {
                result = WithCodexHome(codexHome, () => CodexSkillInstaller.InstallBundledSkill(createCodexHome: true, overwrite: false));
            }

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Installed).IsTrue();
            await Assert.That(File.Exists(Path.Combine(codexHome, "skills", CodexSkillInstaller.SkillName, "SKILL.md"))).IsTrue();
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task InstallBundledSkill_SkipsWhenCodexHomeDoesNotExist()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var codexHome = Path.Combine(tempRoot, "missing-codex-home");
            SkillInstallResult result;
            lock (EnvironmentLock)
            {
                result = WithCodexHome(codexHome, () => CodexSkillInstaller.InstallBundledSkill(createCodexHome: false, overwrite: false));
            }

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Skipped).IsTrue();
            await Assert.That(Directory.Exists(codexHome)).IsFalse();
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task TryAutoInstall_WritesDiagnosticWhenSkillIsInstalled()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var codexHome = Path.Combine(tempRoot, ".codex");
            Directory.CreateDirectory(codexHome);
            using var diagnostics = new StringWriter();
            SkillInstallResult result;
            lock (EnvironmentLock)
            {
                result = WithCodexHome(codexHome, () => CodexSkillInstaller.TryAutoInstall(diagnostics));
            }

            await Assert.That(result.Installed).IsTrue();
            await Assert.That(diagnostics.ToString()).Contains("Installed");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task Install_CopiesSkillFilesIntoCodexHome()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sourceSkill = CreateSourceSkill(tempRoot);
            var codexHome = Path.Combine(tempRoot, ".codex");

            var result = CodexSkillInstaller.Install(sourceSkill, codexHome, overwrite: false);

            var targetSkill = Path.Combine(codexHome, "skills", CodexSkillInstaller.SkillName);
            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Installed).IsTrue();
            await Assert.That(File.Exists(Path.Combine(targetSkill, "SKILL.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(targetSkill, "agents", "openai.yaml"))).IsTrue();
            await Assert.That(File.ReadAllText(Path.Combine(targetSkill, "SKILL.md"))).Contains("codex-computer-run");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task Install_ReturnsFailureForInvalidSource()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var missingSource = Path.Combine(tempRoot, "missing-source");
            var emptySource = Path.Combine(tempRoot, "empty-source");
            Directory.CreateDirectory(emptySource);

            var missingResult = CodexSkillInstaller.Install(missingSource, Path.Combine(tempRoot, ".codex"), overwrite: false);
            var emptyResult = CodexSkillInstaller.Install(emptySource, Path.Combine(tempRoot, ".codex"), overwrite: false);

            await Assert.That(missingResult.Success).IsFalse();
            await Assert.That(emptyResult.Success).IsFalse();
            await Assert.That(emptyResult.Message).Contains("SKILL.md");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task Install_DoesNotOverwriteExistingSkillFilesUnlessForced()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sourceSkill = CreateSourceSkill(tempRoot);
            var codexHome = Path.Combine(tempRoot, ".codex");
            var targetSkill = Path.Combine(codexHome, "skills", CodexSkillInstaller.SkillName);
            Directory.CreateDirectory(targetSkill);
            var targetSkillFile = Path.Combine(targetSkill, "SKILL.md");
            File.WriteAllText(targetSkillFile, "user custom skill");

            var result = CodexSkillInstaller.Install(sourceSkill, codexHome, overwrite: false);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(File.ReadAllText(targetSkillFile)).IsEqualTo("user custom skill");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    [Test]
    public async Task Install_OverwritesExistingSkillFilesWhenForced()
    {
        var tempRoot = CreateTempRoot();

        try
        {
            var sourceSkill = CreateSourceSkill(tempRoot);
            var codexHome = Path.Combine(tempRoot, ".codex");
            var targetSkill = Path.Combine(codexHome, "skills", CodexSkillInstaller.SkillName);
            Directory.CreateDirectory(targetSkill);
            var targetSkillFile = Path.Combine(targetSkill, "SKILL.md");
            File.WriteAllText(targetSkillFile, "user custom skill");

            var result = CodexSkillInstaller.Install(sourceSkill, codexHome, overwrite: true);

            await Assert.That(result.Success).IsTrue();
            await Assert.That(result.Installed).IsTrue();
            await Assert.That(File.ReadAllText(targetSkillFile)).Contains("codex-computer-run");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateSourceSkill(string tempRoot)
    {
        var sourceSkill = Path.Combine(tempRoot, "source", CodexSkillInstaller.SkillName);
        Directory.CreateDirectory(Path.Combine(sourceSkill, "agents"));
        File.WriteAllText(Path.Combine(sourceSkill, "SKILL.md"), "---\nname: codex-computer-run\n---\n");
        File.WriteAllText(Path.Combine(sourceSkill, "agents", "openai.yaml"), "interface:\n  display_name: \"Codex Computer Run\"\n");
        return sourceSkill;
    }

    private static string CreateTempRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "codex-computer-run-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private static SkillInstallResult WithCodexHome(string codexHome, Func<SkillInstallResult> action)
    {
        var previousCodexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        try
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", codexHome);
            return action();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CODEX_HOME", previousCodexHome);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // Test cleanup only.
        }
    }
}
