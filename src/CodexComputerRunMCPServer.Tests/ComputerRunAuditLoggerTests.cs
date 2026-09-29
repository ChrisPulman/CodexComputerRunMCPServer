using System.Text.Json;

namespace CodexComputerRunMCPServer.Tests;

public class ComputerRunAuditLoggerTests
{
    [Test]
    public async Task EnabledAudit_WritesStructuredRecordWithoutTextContent()
    {
        var path = CreateTempPath();
        var options = new ComputerRunAuditOptions(true, path);

        try
        {
            var result = ComputerRunAuditLogger.Execute(
                "type_text",
                new { textLength = 12 },
                () => "ok",
                options);

            await Assert.That(result).IsEqualTo("ok");
            var record = ReadSingleRecord(path);
            await Assert.That(record.GetProperty("tool").GetString()).IsEqualTo("type_text");
            await Assert.That(record.GetProperty("success").GetBoolean()).IsTrue();
            await Assert.That(record.GetProperty("arguments").GetProperty("textLength").GetInt32()).IsEqualTo(12);
            await Assert.That(record.GetProperty("arguments").ToString()).DoesNotContain("secret message");
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Test]
    public async Task FailedAudit_WritesErrorAndRethrowsOriginalException()
    {
        var path = CreateTempPath();
        var options = new ComputerRunAuditOptions(true, path);

        try
        {
            Func<string> failingAction = () => throw new InvalidOperationException("expected failure");

            await Assert.That(() => ComputerRunAuditLogger.Execute(
                    "screenshot",
                    new { hasPath = false },
                    failingAction,
                    options))
                .Throws<InvalidOperationException>()
                .WithMessageContaining("expected failure");

            var record = ReadSingleRecord(path);
            await Assert.That(record.GetProperty("success").GetBoolean()).IsFalse();
            await Assert.That(record.GetProperty("error").GetProperty("type").GetString()).IsEqualTo(nameof(InvalidOperationException));
        }
        finally
        {
            TryDelete(path);
        }
    }

    [Test]
    public async Task DisabledAudit_DoesNotCreateLogFile()
    {
        var path = CreateTempPath();

        try
        {
            var result = ComputerRunAuditLogger.Execute("cursor_position", null, () => "{}", new ComputerRunAuditOptions(false, path));

            await Assert.That(result).IsEqualTo("{}");
            await Assert.That(File.Exists(path)).IsFalse();
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static JsonElement ReadSingleRecord(string path)
        => JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone();

    private static string CreateTempPath()
        => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "codex-computer-run-tests", Guid.NewGuid().ToString("N"), "actions.jsonl");

    private static void TryDelete(string path)
    {
        try
        {
            var directory = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Test cleanup only.
        }
    }
}
