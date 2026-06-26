using WindowSwitch.Services;

namespace WindowSwitch.Tests;

public sealed class DesktopHotkeySequenceTests
{
    [Fact]
    public void ZeroSwitchesToTenthDesktopWhenItExists()
    {
        var sequence = new DesktopHotkeySequence();

        var target = sequence.HandleDigit(0, Enumerable.Range(1, 10).ToArray());

        Assert.Equal(10, target);
        Assert.False(sequence.IsListening);
    }

    [Fact]
    public void TenOrFewerDesktopsDoNotArmTwoDigitSequence()
    {
        var sequence = new DesktopHotkeySequence();
        var desktops = Enumerable.Range(1, 10).ToArray();

        var first = sequence.HandleDigit(1, desktops);
        var second = sequence.HandleDigit(5, desktops);

        Assert.Equal(1, first);
        Assert.Equal(5, second);
        Assert.False(sequence.IsListening);
    }

    [Fact]
    public void FirstDigitSwitchesImmediatelyAndSecondDigitCanSelectExistingDesktop()
    {
        var sequence = new DesktopHotkeySequence();
        var desktops = Enumerable.Range(1, 15).ToArray();

        var first = sequence.HandleDigit(1, desktops);
        var second = sequence.HandleDigit(5, desktops);

        Assert.Equal(1, first);
        Assert.Equal(15, second);
        Assert.False(sequence.IsListening);
    }

    [Fact]
    public void FirstDigitOnlyListensWhenMatchingTwoDigitDesktopExists()
    {
        var sequence = new DesktopHotkeySequence();
        var desktops = Enumerable.Range(1, 15).ToArray();

        var first = sequence.HandleDigit(3, desktops);
        var second = sequence.HandleDigit(5, desktops);

        Assert.Equal(3, first);
        Assert.Equal(5, second);
        Assert.False(sequence.IsListening);
    }

    [Fact]
    public void ListeningCanContinueUntilModifiersAreReset()
    {
        var sequence = new DesktopHotkeySequence();
        var desktops = Enumerable.Range(1, 25).ToArray();

        var first = sequence.HandleDigit(1, desktops);
        var second = sequence.HandleDigit(2, desktops);
        var third = sequence.HandleDigit(5, desktops);

        Assert.Equal(1, first);
        Assert.Equal(12, second);
        Assert.Equal(25, third);
        Assert.False(sequence.IsListening);
    }

    [Fact]
    public void ResetStopsPendingTwoDigitSequence()
    {
        var sequence = new DesktopHotkeySequence();
        var desktops = Enumerable.Range(1, 15).ToArray();

        sequence.HandleDigit(1, desktops);
        sequence.Reset();
        var target = sequence.HandleDigit(5, desktops);

        Assert.Equal(5, target);
        Assert.False(sequence.IsListening);
    }
}
