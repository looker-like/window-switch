using WindowSwitch.Services;

namespace WindowSwitch.Tests;

public sealed class ModifierKeyHelperTests
{
    // ── AreRequiredModifiersPressed ────────────────────────────────────

    [Fact]
    public void AreRequiredModifiersPressed_CtrlAlt_WhenBothPressed_ReturnsTrue()
    {
        var state = new FakeModifierKeyState(0x11, 0x12); // Ctrl + Alt
        var modifiers = (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt);

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.True(result);
    }

    [Fact]
    public void AreRequiredModifiersPressed_CtrlAlt_WhenOnlyCtrlPressed_ReturnsFalse()
    {
        var state = new FakeModifierKeyState(0x11); // only Ctrl
        var modifiers = (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt);

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.False(result);
    }

    [Fact]
    public void AreRequiredModifiersPressed_Shift_WhenShiftPressed_ReturnsTrue()
    {
        var state = new FakeModifierKeyState(0x10); // Shift
        var modifiers = (int)HotkeyModifiers.Shift;

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.True(result);
    }

    [Fact]
    public void AreRequiredModifiersPressed_Win_WhenLeftWinPressed_ReturnsTrue()
    {
        var state = new FakeModifierKeyState(0x5B); // Left Windows
        var modifiers = (int)HotkeyModifiers.Windows;

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.True(result);
    }

    [Fact]
    public void AreRequiredModifiersPressed_Win_WhenRightWinPressed_ReturnsTrue()
    {
        var state = new FakeModifierKeyState(0x5C); // Right Windows
        var modifiers = (int)HotkeyModifiers.Windows;

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.True(result);
    }

    [Fact]
    public void AreRequiredModifiersPressed_WhenNonePressed_ReturnsFalse()
    {
        var state = new FakeModifierKeyState(); // nothing pressed
        var modifiers = (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt);

        var result = ModifierKeyHelper.AreRequiredModifiersPressed(modifiers, state);

        Assert.False(result);
    }

    // ── GetPressedHotkeyModifiers ──────────────────────────────────────

    [Fact]
    public void GetPressedHotkeyModifiers_WhenCtrlAndAltPressed_ReturnsCtrlAlt()
    {
        var state = new FakeModifierKeyState(0x11, 0x12); // Ctrl + Alt

        var result = ModifierKeyHelper.GetPressedHotkeyModifiers(state);

        Assert.Equal((int)(HotkeyModifiers.Control | HotkeyModifiers.Alt), result);
    }

    [Fact]
    public void GetPressedHotkeyModifiers_WhenNothingPressed_ReturnsNone()
    {
        var state = new FakeModifierKeyState();

        var result = ModifierKeyHelper.GetPressedHotkeyModifiers(state);

        Assert.Equal((int)HotkeyModifiers.None, result);
    }

    [Fact]
    public void GetPressedHotkeyModifiers_WhenAllPressed_ReturnsAllModifiers()
    {
        var state = new FakeModifierKeyState(0x10, 0x11, 0x12, 0x5B); // Shift+Ctrl+Alt+Win

        var result = ModifierKeyHelper.GetPressedHotkeyModifiers(state);

        var expected = (int)(HotkeyModifiers.Shift | HotkeyModifiers.Control |
                              HotkeyModifiers.Alt | HotkeyModifiers.Windows);
        Assert.Equal(expected, result);
    }
}
