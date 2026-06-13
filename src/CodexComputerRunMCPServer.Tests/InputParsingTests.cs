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
        await Assert.That(() => MouseButtonParser.Parse("extra")).Throws<ArgumentException>();
        await Assert.That(() => KeyboardInput.ResolveKeyChord("unknown-key", _ => -1)).Throws<ArgumentException>();
        await Assert.That(() => KeyboardInput.ResolveHotkey("+,", _ => -1)).Throws<ArgumentException>();
        await Assert.That(() => Delay.FromSeconds(double.NaN, "bad")).Throws<ArgumentOutOfRangeException>();
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
}
