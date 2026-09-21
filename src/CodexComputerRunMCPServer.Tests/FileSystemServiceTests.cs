using System.Text.Json;

namespace CodexComputerRunMCPServer.Tests;

public class FileSystemServiceTests
{
    [Test]
    public async Task ListDirectoryReturnsBoundedMetadata()
    {
        var root = CreateTestDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "nested"));
            File.WriteAllText(Path.Combine(root, "b.txt"), "hello");
            File.WriteAllText(Path.Combine(root, "nested", "a.txt"), "world");

            var result = JsonDocument.Parse(FileSystemService.ListDirectory(root, recursive: true, maxEntries: 10)).RootElement;
            var entries = result.GetProperty("entries");

            await Assert.That(result.GetProperty("path").GetString()).IsEqualTo(Path.GetFullPath(root));
            await Assert.That(entries.GetArrayLength()).IsEqualTo(3);
            await Assert.That(entries[0].GetProperty("name").GetString()).IsEqualTo("b.txt");
            await Assert.That(entries[1].GetProperty("name").GetString()).IsEqualTo("nested");
            await Assert.That(result.GetProperty("truncated").GetBoolean()).IsFalse();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Test]
    public async Task MutatingOperationsAreDryRunByDefaultAndCanBeApplied()
    {
        var root = CreateTestDirectory();
        try
        {
            var source = Path.Combine(root, "source.txt");
            var copied = Path.Combine(root, "copied.txt");
            var moved = Path.Combine(root, "renamed.txt");
            File.WriteAllText(source, "content");

            var dryCopy = JsonDocument.Parse(FileSystemService.CopyPath(source, copied, overwrite: false, dryRun: true)).RootElement;
            await Assert.That(dryCopy.GetProperty("changed").GetBoolean()).IsFalse();
            await Assert.That(File.Exists(copied)).IsFalse();

            var appliedCopy = JsonDocument.Parse(FileSystemService.CopyPath(source, copied, overwrite: false, dryRun: false)).RootElement;
            await Assert.That(appliedCopy.GetProperty("changed").GetBoolean()).IsTrue();
            await Assert.That(File.ReadAllText(copied)).IsEqualTo("content");

            _ = FileSystemService.MovePath(copied, moved, overwrite: false, dryRun: false);
            await Assert.That(File.Exists(copied)).IsFalse();
            await Assert.That(File.Exists(moved)).IsTrue();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Test]
    public async Task DirectoryMoveIntoItselfIsRejectedBeforeMutation()
    {
        var root = CreateTestDirectory();
        try
        {
            var nested = Path.Combine(root, "nested");
            Directory.CreateDirectory(nested);

            await Assert.That(() => FileSystemService.MovePath(root, nested, overwrite: false, dryRun: true))
                .Throws<IOException>()
                .WithMessageContaining("into itself");
            await Assert.That(Directory.Exists(root)).IsTrue();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Test]
    public async Task DeleteRequiresDryRunFalseAndExactPath()
    {
        var root = CreateTestDirectory();
        try
        {
            var file = Path.Combine(root, "delete-me.txt");
            File.WriteAllText(file, "temporary");

            var plan = JsonDocument.Parse(FileSystemService.DeletePath(file, recursive: false, dryRun: true)).RootElement;
            await Assert.That(plan.GetProperty("changed").GetBoolean()).IsFalse();
            await Assert.That(File.Exists(file)).IsTrue();

            _ = FileSystemService.DeletePath(file, recursive: false, dryRun: false);
            await Assert.That(File.Exists(file)).IsFalse();
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Test]
    public async Task DeleteRefusesFilesystemRoot()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()));
        await Assert.That(() => FileSystemService.DeletePath(root!, recursive: true, dryRun: false))
            .Throws<IOException>()
            .WithMessageContaining("filesystem root");
    }

    private static string CreateTestDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "codex-computer-run-files", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
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
}
