using WindowSwitch.Services;

namespace WindowSwitch.Controls;

public sealed class OverlayActionInvokedEventArgs(VirtualDesktopAction action) : EventArgs
{
    public VirtualDesktopAction Action { get; } = action;
}
