namespace WindowSwitch.Controls;

public sealed class OverlayDesktopInvokedEventArgs(Guid desktopId) : EventArgs
{
    public Guid DesktopId { get; } = desktopId;
}
