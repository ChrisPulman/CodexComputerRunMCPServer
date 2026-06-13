using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;

namespace CodexComputerRunMCPServer.Tests;

public class ComputerRunLifecycleTests
{
    [Test]
    public async Task ActivityTracker_TracksActiveInvocationAndCompletion()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero));
        var tracker = new ComputerRunActivityTracker(clock);

        clock.Advance(TimeSpan.FromSeconds(1));
        var invocation = tracker.BeginInvocation();
        var running = tracker.GetSnapshot();

        clock.Advance(TimeSpan.FromSeconds(2));
        invocation.Dispose();
        var completed = tracker.GetSnapshot();

        await Assert.That(running.ActiveInvocations).IsEqualTo(1);
        await Assert.That(running.LastActivityUtc).IsEqualTo(new DateTimeOffset(2026, 5, 12, 12, 0, 1, TimeSpan.Zero));
        await Assert.That(completed.ActiveInvocations).IsEqualTo(0);
        await Assert.That(completed.LastActivityUtc).IsEqualTo(new DateTimeOffset(2026, 5, 12, 12, 0, 3, TimeSpan.Zero));
    }

    [Test]
    public async Task StaticTools_TrackActivityDuringServiceInvocation()
    {
        using var runtimeLock = await RuntimeMutationLock.AcquireAsync();
        var service = new ActivityInspectingService();
        var tracker = new ComputerRunActivityTracker();
        using var restoreService = ComputerRunToolRuntime.ReplaceServiceForTests(service);
        using var restoreTracker = ComputerRunToolRuntime.ReplaceActivityTrackerForTests(tracker);

        _ = ComputerRunTools.cursor_position();

        await Assert.That(service.ActiveInvocationsDuringCall).IsEqualTo(1);
        await Assert.That(tracker.GetSnapshot().ActiveInvocations).IsEqualTo(0);
    }

    [Test]
    public async Task IdleShutdown_StopsOnlyWhenNoActiveInvocationsExceedTimeout()
    {
        var options = ComputerRunLifecycleOptions.Default with
        {
            IdleShutdownEnabled = true,
            IdleTimeout = TimeSpan.FromMinutes(5)
        };
        var now = new DateTimeOffset(2026, 5, 12, 12, 10, 0, TimeSpan.Zero);
        var idleSnapshot = new ComputerRunActivitySnapshot(now - TimeSpan.FromMinutes(5), ActiveInvocations: 0);
        var activeSnapshot = new ComputerRunActivitySnapshot(now - TimeSpan.FromMinutes(30), ActiveInvocations: 1);
        var recentSnapshot = new ComputerRunActivitySnapshot(now - TimeSpan.FromMinutes(4), ActiveInvocations: 0);

        await Assert.That(IdleShutdownService.ShouldStopForIdle(options, idleSnapshot, now)).IsTrue();
        await Assert.That(IdleShutdownService.ShouldStopForIdle(options, activeSnapshot, now)).IsFalse();
        await Assert.That(IdleShutdownService.ShouldStopForIdle(options, recentSnapshot, now)).IsFalse();
    }

    [Test]
    public async Task LifecycleOptions_DefaultKeepsLongLivedMcpTransportsOpen()
    {
        var options = ComputerRunLifecycleOptions.Default;

        await Assert.That(options.ControlLockEnabled).IsTrue();
        await Assert.That(options.ControlLeaseDuration).IsEqualTo(ComputerRunLifecycleOptions.DefaultControlLeaseDuration);
        await Assert.That(options.IdleShutdownEnabled).IsFalse();
        await Assert.That(options.IdleTimeout).IsEqualTo(ComputerRunLifecycleOptions.DefaultIdleTimeout);
    }

    [Test]
    public async Task LifecycleOptions_ReadsConfigurationOverrides()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComputerRun:ControlLockEnabled"] = "false",
                ["ComputerRun:ControlLeaseSeconds"] = "0",
                ["CODEX_COMPUTER_RUN_IDLE_SHUTDOWN"] = "false",
                ["ComputerRun:IdleTimeoutSeconds"] = "12.5",
                ["ComputerRun:IdleCheckIntervalSeconds"] = "1",
            })
            .Build();

        var options = ComputerRunLifecycleOptions.FromConfiguration(configuration);

        await Assert.That(options.ControlLockEnabled).IsFalse();
        await Assert.That(options.ControlLeaseDuration).IsEqualTo(TimeSpan.Zero);
        await Assert.That(options.IdleShutdownEnabled).IsFalse();
        await Assert.That(options.IdleTimeout).IsEqualTo(TimeSpan.FromSeconds(12.5));
        await Assert.That(options.IdleCheckInterval).IsEqualTo(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task DesktopControlLease_RejectsSecondConcurrentOwner()
    {
        var lockFilePath = Path.Combine(
            Path.GetTempPath(),
            "codex-computer-run-tests",
            Guid.NewGuid().ToString("N"),
            "control.lock");

        using var first = new DesktopControlLease(enabled: true, TimeSpan.FromMinutes(1), lockFilePath);
        using var second = new DesktopControlLease(enabled: true, TimeSpan.FromMinutes(1), lockFilePath);
        using var firstControl = first.BeginControlInvocation();

        await Assert.That(() =>
        {
            using var secondControl = second.BeginControlInvocation();
        }).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task DesktopControlLease_ReleasesAfterIdleLeaseExpires()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 5, 12, 12, 0, 0, TimeSpan.Zero));
        var lockFilePath = Path.Combine(
            Path.GetTempPath(),
            "codex-computer-run-tests",
            Guid.NewGuid().ToString("N"),
            "control.lock");

        using var first = new DesktopControlLease(enabled: true, TimeSpan.FromDays(1), lockFilePath, clock);
        using var second = new DesktopControlLease(enabled: true, TimeSpan.FromDays(1), lockFilePath, clock);

        first.BeginControlInvocation().Dispose();
        clock.Advance(TimeSpan.FromDays(2));

        await Assert.That(first.ReleaseExpiredIdleLease()).IsTrue();
        using var secondControl = second.BeginControlInvocation();
    }

    [Test]
    public async Task StaticTools_ApplyControlLeaseOnlyToInputChangingActions()
    {
        using var runtimeLock = await RuntimeMutationLock.AcquireAsync();
        var lockFilePath = Path.Combine(
            Path.GetTempPath(),
            "codex-computer-run-tests",
            Guid.NewGuid().ToString("N"),
            "control.lock");
        var service = new ControlLeaseInspectingService();

        using var competingLease = new DesktopControlLease(enabled: true, TimeSpan.FromMinutes(1), lockFilePath);
        using var competingControl = competingLease.BeginControlInvocation();
        using var restoreService = ComputerRunToolRuntime.ReplaceServiceForTests(service);
        using var restoreLease = ComputerRunToolRuntime.ReplaceControlLeaseForTests(
            new DesktopControlLease(enabled: true, TimeSpan.FromMinutes(1), lockFilePath));

        _ = ComputerRunTools.cursor_position();
        await Assert.That(() => ComputerRunTools.move_mouse(1, 2)).Throws<InvalidOperationException>();

        await Assert.That(service.CursorCalls).IsEqualTo(1);
        await Assert.That(service.MoveCalls).IsEqualTo(0);
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed class ActivityInspectingService : IComputerRunService
    {
        public int ActiveInvocationsDuringCall { get; private set; }

        public string CursorPosition()
        {
            ActiveInvocationsDuringCall = ComputerRunToolRuntime.ActivityTracker.GetSnapshot().ActiveInvocations;
            return "{}";
        }

        public CallToolResult Screenshot(string? path, bool includeImage) => throw new NotSupportedException();

        public string MoveMouse(int x, int y, double? delay) => throw new NotSupportedException();

        public string Click(int? x, int? y, string button, int clicks, double interval, double? delay)
            => throw new NotSupportedException();

        public string Scroll(int amount, int? x, int? y, double? delay) => throw new NotSupportedException();

        public string PressKey(string key, double duration, double? delay) => throw new NotSupportedException();

        public string Hotkey(string keys, double? delay) => throw new NotSupportedException();

        public string TypeText(string text, double? delay) => throw new NotSupportedException();

        public string ListWindows(int limit) => throw new NotSupportedException();
    }

    private sealed class ControlLeaseInspectingService : IComputerRunService
    {
        public int CursorCalls { get; private set; }

        public int MoveCalls { get; private set; }

        public string CursorPosition()
        {
            CursorCalls++;
            return "{}";
        }

        public string MoveMouse(int x, int y, double? delay)
        {
            MoveCalls++;
            return "move";
        }

        public CallToolResult Screenshot(string? path, bool includeImage) => throw new NotSupportedException();

        public string Click(int? x, int? y, string button, int clicks, double interval, double? delay)
            => throw new NotSupportedException();

        public string Scroll(int amount, int? x, int? y, double? delay) => throw new NotSupportedException();

        public string PressKey(string key, double duration, double? delay) => throw new NotSupportedException();

        public string Hotkey(string keys, double? delay) => throw new NotSupportedException();

        public string TypeText(string text, double? delay) => throw new NotSupportedException();

        public string ListWindows(int limit) => throw new NotSupportedException();
    }
}
