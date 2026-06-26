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

public sealed record HotkeyModifierOption(int Value, string DisplayName);

public sealed record HotkeyKeyOption(int VirtualKey, string DisplayName);

public static class HotkeyDefinitions
{
    public const int DefaultHotkeyModifiers =
        (int)(HotkeyModifiers.Control | HotkeyModifiers.Alt);
    public const int DefaultShowHotkeyVirtualKey = 0x20;
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
        .. Enumerable.Range('A', 26).Select(key => new HotkeyKeyOption(key, ((char)key).ToString())),
        .. Enumerable.Range(1, 12).Select(index => new HotkeyKeyOption(0x70 + index - 1, $"F{index}")),
    ];

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
        return KeyOptions.Any(option => option.VirtualKey == virtualKey)
            ? virtualKey
            : DefaultShowHotkeyVirtualKey;
    }

    public static string FormatHotkey(int modifiers, int virtualKey)
    {
        var keyName = KeyOptions.FirstOrDefault(option => option.VirtualKey == virtualKey)?.DisplayName
            ?? $"0x{virtualKey:X2}";
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
}
