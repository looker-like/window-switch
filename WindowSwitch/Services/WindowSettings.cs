namespace WindowSwitch.Services;

public sealed class WindowSettings
{
    public const int DefaultColumnsPerRow = 2;
    public const double DefaultWindowOpacity = 0.95;

    public WindowSettings()
    {
    }

    public WindowSettings(
        double left,
        double top,
        bool isFloatingTopmost = true,
        int columnsPerRow = DefaultColumnsPerRow,
        double windowOpacity = DefaultWindowOpacity,
        bool startHidden = true,
        bool autoHideAfterSwitch = false,
        bool enableColoredDesktopLabels = false,
        bool isHotkeyEnabled = true,
        bool isDesktopHotkeysEnabled = true,
        int showHotkeyModifiers = HotkeyDefinitions.DefaultHotkeyModifiers,
        int showHotkeyVirtualKey = HotkeyDefinitions.DefaultShowHotkeyVirtualKey,
        int desktopHotkeyModifiers = HotkeyDefinitions.DefaultHotkeyModifiers,
        int showHotkeyKind = (int)HotkeyDefinitions.DefaultShowHotkeyKind,
        int showHotkeyMouseButton = (int)HotkeyDefinitions.DefaultShowHotkeyMouseButton)
    {
        Left = left;
        Top = top;
        IsFloatingTopmost = isFloatingTopmost;
        ColumnsPerRow = columnsPerRow;
        WindowOpacity = windowOpacity;
        StartHidden = startHidden;
        AutoHideAfterSwitch = autoHideAfterSwitch;
        EnableColoredDesktopLabels = enableColoredDesktopLabels;
        IsHotkeyEnabled = isHotkeyEnabled;
        IsDesktopHotkeysEnabled = isDesktopHotkeysEnabled;
        ShowHotkeyModifiers = showHotkeyModifiers;
        ShowHotkeyVirtualKey = showHotkeyVirtualKey;
        DesktopHotkeyModifiers = desktopHotkeyModifiers;
        ShowHotkeyKind = showHotkeyKind;
        ShowHotkeyMouseButton = showHotkeyMouseButton;
    }

    public double Left { get; init; } = double.NaN;

    public double Top { get; init; } = double.NaN;

    public bool IsFloatingTopmost { get; init; } = true;

    public int ColumnsPerRow { get; init; } = DefaultColumnsPerRow;

    public double WindowOpacity { get; init; } = DefaultWindowOpacity;

    public bool StartHidden { get; init; } = true;

    public bool AutoHideAfterSwitch { get; init; }

    public bool EnableColoredDesktopLabels { get; init; }

    public bool IsHotkeyEnabled { get; init; } = true;

    public bool IsDesktopHotkeysEnabled { get; init; } = true;

    public int ShowHotkeyModifiers { get; init; } = HotkeyDefinitions.DefaultHotkeyModifiers;

    public int ShowHotkeyVirtualKey { get; init; } = HotkeyDefinitions.DefaultShowHotkeyVirtualKey;

    public int DesktopHotkeyModifiers { get; init; } = HotkeyDefinitions.DefaultHotkeyModifiers;

    public int ShowHotkeyKind { get; init; } = (int)HotkeyDefinitions.DefaultShowHotkeyKind;

    public int ShowHotkeyMouseButton { get; init; } = (int)HotkeyDefinitions.DefaultShowHotkeyMouseButton;
}
