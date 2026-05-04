namespace CodexComputerRunMCPServer.Tests;

public class InputParsingTests
{
    [Test]
    public async Task SplitKeys_AcceptsCommonSeparators()
    {
        var parts = KeyboardInput.SplitKeys("ctrl+shift, escape");

        await Assert.That(string.Join("|", parts)).IsEqualTo("ctrl|shift|escape");
    }

    [Test]
    public async Task ResolveKeyChord_UsesKeyboardLayoutShiftState()
    {
        short Scan(char c) => c == 'A' ? (short)((1 << 8) | 0x41) : (short)-1;

        var chord = KeyboardInput.ResolveKeyChord("A", Scan);

        await Assert.That(string.Join(",", chord)).IsEqualTo($"{KeyboardInput.ShiftKey},65");
    }

    [Test]
    public async Task ResolveHotkey_FlattensExplicitAndImplicitModifiers()
    {
        short Scan(char c) => c == 'A' ? (short)((1 << 8) | 0x41) : (short)-1;

        var chord = KeyboardInput.ResolveHotkey("ctrl+A", Scan);

        await Assert.That(string.Join(",", chord)).IsEqualTo($"{KeyboardInput.ControlKey},{KeyboardInput.ShiftKey},65");
    }

    [Test]
    public async Task ResolveKeyChord_IncludesControlAndAltShiftStates()
    {
        short Scan(char c) => c == '@' ? (short)(((2 | 4) << 8) | 0x32) : (short)-1;

        var chord = KeyboardInput.ResolveKeyChord("@", Scan);

        await Assert.That(string.Join(",", chord)).IsEqualTo($"{KeyboardInput.ControlKey},{KeyboardInput.AltKey},50");
    }

    [Test]
    public async Task MouseButtonParser_AcceptsAllSupportedButtons()
    {
        await Assert.That(MouseButtonParser.Parse("left")).IsEqualTo(MouseButton.Left);
        await Assert.That(MouseButtonParser.Parse(" right ")).IsEqualTo(MouseButton.Right);
        await Assert.That(MouseButtonParser.Parse("middle")).IsEqualTo(MouseButton.Middle);
    }

    [Test]
    public async Task Parsers_RejectInvalidInput()
    {
        var badMouse = Throws<ArgumentException>(() => MouseButtonParser.Parse("extra"));
        var badKey = Throws<ArgumentException>(() => KeyboardInput.ResolveKeyChord("unknown-key", _ => -1));
        var emptyHotkey = Throws<ArgumentException>(() => KeyboardInput.ResolveHotkey("+,", _ => -1));
        var badDelay = Throws<ArgumentOutOfRangeException>(() => Delay.FromSeconds(double.NaN, "bad"));

        await Assert.That(badMouse).IsTrue();
        await Assert.That(badKey).IsTrue();
        await Assert.That(emptyHotkey).IsTrue();
        await Assert.That(badDelay).IsTrue();
    }

    [Test]
    public async Task Delay_ClampsNegativeDurationsToZero()
    {
        await Assert.That(Delay.FromSeconds(-1, "delay")).IsEqualTo(TimeSpan.Zero);
        await Assert.That(Delay.FromSeconds(0, "delay")).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task Delay_SleepHandlesNullZeroAndPositiveValues()
    {
        var started = DateTimeOffset.UtcNow;

        Delay.Sleep(null);
        Delay.Sleep(0);
        Delay.Sleep(0.001);

        await Assert.That(DateTimeOffset.UtcNow >= started).IsTrue();
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }
}
