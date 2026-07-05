namespace WindowSwitch.Services;

public static class ModifierKeyHelper
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12; // Alt
    private const int VkLeftWindows = 0x5B;
    private const int VkRightWindows = 0x5C;

    public static bool AreRequiredModifiersPressed(int modifiers, IModifierKeyState state)
    {
        var normalized = (HotkeyModifiers)HotkeyDefinitions.NormalizeModifiers(modifiers);

        if (normalized.HasFlag(HotkeyModifiers.Control) && !state.IsPressed(VkControl))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Alt) && !state.IsPressed(VkMenu))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Shift) && !state.IsPressed(VkShift))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Windows) &&
            !state.IsPressed(VkLeftWindows) &&
            !state.IsPressed(VkRightWindows))
        {
            return false;
        }

        return true;
    }

    public static int GetPressedHotkeyModifiers(IModifierKeyState state)
    {
        var modifiers = HotkeyModifiers.None;
        if (state.IsPressed(VkControl))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (state.IsPressed(VkMenu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (state.IsPressed(VkShift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (state.IsPressed(VkLeftWindows) || state.IsPressed(VkRightWindows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return (int)modifiers;
    }
}

