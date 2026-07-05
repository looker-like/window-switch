namespace WindowSwitch.Services;

public static class MouseButtonMapper
{
    public static bool TryMap(
        int message,
        int mouseData,
        out MouseHotkeyButton button,
        out bool isDown,
        out bool isUp)
    {
        button = MouseHotkeyButton.Middle;
        isDown = false;
        isUp = false;
        throw new NotImplementedException();
    }
}
