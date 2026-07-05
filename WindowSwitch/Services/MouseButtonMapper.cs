namespace WindowSwitch.Services;

public static class MouseButtonMapper
{
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;

    public static bool TryMap(
        int message,
        int mouseData,
        out MouseHotkeyButton button,
        out bool isDown,
        out bool isUp)
    {
        isDown = message is WmRightButtonDown or WmMiddleButtonDown or WmXButtonDown;
        isUp = message is WmRightButtonUp or WmMiddleButtonUp or WmXButtonUp;
        button = MouseHotkeyButton.Middle;

        switch (message)
        {
            case WmRightButtonDown:
            case WmRightButtonUp:
                button = MouseHotkeyButton.Right;
                return true;
            case WmMiddleButtonDown:
            case WmMiddleButtonUp:
                button = MouseHotkeyButton.Middle;
                return true;
            case WmXButtonDown:
            case WmXButtonUp:
                var xButton = (mouseData >> 16) & 0xFFFF;
                button = xButton == XButton2 ? MouseHotkeyButton.XButton2 : MouseHotkeyButton.XButton1;
                return xButton is XButton1 or XButton2;
            default:
                return false;
        }
    }
}

