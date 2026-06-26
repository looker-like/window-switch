namespace WindowSwitch.Services;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
}

public enum ShowHotkeyKind
{
    Keyboard = 0,
    MouseButton = 1,
}

public enum MouseHotkeyButton
{
    Left = 0,
    Right = 1,
    Middle = 2,
    XButton1 = 3,
    XButton2 = 4,
}

public sealed record HotkeyModifierOption(int Value, string DisplayName);

public sealed record HotkeyKeyOption(int VirtualKey, string DisplayName);

public sealed record CapturedShowHotkey(
    ShowHotkeyKind Kind,
    int Modifiers,
    int VirtualKey,
    MouseHotkeyButton MouseButton);

public static class HotkeyDefinitions
{
    public const int DefaultHotkeyModifiers =
        (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt);
    public const int DefaultShowHotkeyVirtualKey = 0x20;
    public const ShowHotkeyKind DefaultShowHotkeyKind = ShowHotkeyKind.Keyboard;
    public const MouseHotkeyButton DefaultShowHotkeyMouseButton = MouseHotkeyButton.Middle;
    public const uint NoRepeatModifier = 0x4000;

    private static readonly int AllSupportedModifierBits =
        (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.Windows);

    public static IReadOnlyList<HotkeyModifierOption> ModifierOptions { get; } =
        Enumerable.Range(1, AllSupportedModifierBits)
            .Where(value => (value & ~AllSupportedModifierBits) == 0)
            .Select(value => new HotkeyModifierOption(value, FormatModifiers(value)))
            .OrderBy(option => option.DisplayName.Length)
            .ThenBy(option => option.DisplayName)
            .ToArray();

    public static IReadOnlyList<HotkeyKeyOption> KeyOptions { get; } =
    [
        new(DefaultShowHotkeyVirtualKey, "Space"),
        new(0x0D, "Enter"),
        new(0x09, "Tab"),
        .. Enumerable.Range('0', 10).Select(key => new HotkeyKeyOption(key, ((char)key).ToString())),
        .. Enumerable.Range('A', 26).Select(key => new HotkeyKeyOption(key, ((char)key).ToString())),
        .. Enumerable.Range(1, 12).Select(index => new HotkeyKeyOption(0x70 + index - 1, $"F{index}")),
    ];

    public static ShowHotkeyKind NormalizeShowHotkeyKind(int kind)
    {
        return Enum.IsDefined(typeof(ShowHotkeyKind), kind)
            ? (ShowHotkeyKind)kind
            : DefaultShowHotkeyKind;
    }

    public static MouseHotkeyButton NormalizeMouseButton(int button)
    {
        return Enum.IsDefined(typeof(MouseHotkeyButton), button) &&
            (MouseHotkeyButton)button != MouseHotkeyButton.Left
            ? (MouseHotkeyButton)button
            : DefaultShowHotkeyMouseButton;
    }

    public static int NormalizeModifiers(int modifiers)
    {
        if ((modifiers & ~AllSupportedModifierBits) != 0 || modifiers == 0)
        {
            return DefaultHotkeyModifiers;
        }

        return modifiers;
    }

    public static int NormalizeVirtualKey(int virtualKey)
    {
        return IsSupportedMainVirtualKey(virtualKey)
            ? virtualKey
            : DefaultShowHotkeyVirtualKey;
    }

    public static string FormatHotkey(
        ShowHotkeyKind kind,
        int modifiers,
        int virtualKey,
        MouseHotkeyButton mouseButton)
    {
        return kind == ShowHotkeyKind.MouseButton
            ? FormatMouseButton(mouseButton)
            : FormatKeyboardHotkey(modifiers, virtualKey);
    }

    public static string FormatKeyboardHotkey(int modifiers, int virtualKey)
    {
        var keyName = FormatVirtualKey(virtualKey);
        return $"{FormatModifiers(modifiers)} + {keyName}";
    }

    public static string FormatDesktopHotkey(int modifiers)
    {
        return $"{FormatModifiers(modifiers)} + 数字";
    }

    public static uint ToRegisterHotkeyModifiers(int modifiers)
    {
        return (uint)NormalizeModifiers(modifiers) | NoRepeatModifier;
    }

    public static bool IsModifierVirtualKey(int virtualKey)
    {
        return virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or
            0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5;
    }

    public static bool IsSupportedMainVirtualKey(int virtualKey)
    {
        return virtualKey is > 0x06 and <= 0xFE && !IsModifierVirtualKey(virtualKey);
    }

    public static string FormatModifiers(int modifiers)
    {
        var normalized = (HotkeyModifiers)NormalizeModifiers(modifiers);
        var parts = new List<string>();
        if (normalized.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (normalized.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (normalized.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (normalized.HasFlag(HotkeyModifiers.Windows))
        {
            parts.Add("Win");
        }

        return string.Join(" + ", parts);
    }

    public static string FormatMouseButton(MouseHotkeyButton button)
    {
        return button switch
        {
            MouseHotkeyButton.Left => "鼠标左键",
            MouseHotkeyButton.Right => "鼠标右键",
            MouseHotkeyButton.Middle => "鼠标中键",
            MouseHotkeyButton.XButton1 => "鼠标侧键 1",
            MouseHotkeyButton.XButton2 => "鼠标侧键 2",
            _ => FormatMouseButton(DefaultShowHotkeyMouseButton),
        };
    }

    public static string FormatVirtualKey(int virtualKey)
    {
        if (KeyOptions.FirstOrDefault(option => option.VirtualKey == virtualKey) is { } option)
        {
            return option.DisplayName;
        }

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x1B => "Esc",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            >= 0x60 and <= 0x69 => $"NumPad{virtualKey - 0x60}",
            >= 0xBA and <= 0xC0 => $"VK 0x{virtualKey:X2}",
            >= 0xDB and <= 0xDF => $"VK 0x{virtualKey:X2}",
            _ => $"VK 0x{virtualKey:X2}",
        };
    }
}
