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
        bool isHotkeyEnabled = true)
    {
        Left = left;
        Top = top;
        IsFloatingTopmost = isFloatingTopmost;
        ColumnsPerRow = columnsPerRow;
        WindowOpacity = windowOpacity;
        StartHidden = startHidden;
        AutoHideAfterSwitch = autoHideAfterSwitch;
        IsHotkeyEnabled = isHotkeyEnabled;
    }

    public double Left { get; init; } = double.NaN;

    public double Top { get; init; } = double.NaN;

    public bool IsFloatingTopmost { get; init; } = true;

    public int ColumnsPerRow { get; init; } = DefaultColumnsPerRow;

    public double WindowOpacity { get; init; } = DefaultWindowOpacity;

    public bool StartHidden { get; init; } = true;

    public bool AutoHideAfterSwitch { get; init; }

    public bool IsHotkeyEnabled { get; init; } = true;
}
