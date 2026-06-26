using WindowSwitch.Models;
using WindowsDesktop;

namespace WindowSwitch.Services;

public sealed class WindowsVirtualDesktopService : IVirtualDesktopService
{
    public event EventHandler? DesktopsChanged;

    public WindowsVirtualDesktopService()
    {
        TrySubscribeToDesktopEvents();
    }

    public IReadOnlyList<VirtualDesktopInfo> GetDesktops()
    {
        EnsureSupported();

        var current = VirtualDesktop.Current;
        return VirtualDesktop.GetDesktops()
            .Select((desktop, index) => new VirtualDesktopInfo(
                desktop.Id,
                index + 1,
                desktop.Name ?? string.Empty,
                current is not null && desktop.Id == current.Id))
            .ToArray();
    }

    public void SwitchTo(Guid id)
    {
        EnsureSupported();

        var desktop = VirtualDesktop.FromId(id);
        if (desktop is null)
        {
            return;
        }

        desktop.Switch();
    }

    private static void EnsureSupported()
    {
        if (!VirtualDesktop.IsSupported)
        {
            throw new PlatformNotSupportedException("Virtual desktop API is not supported on this Windows build.");
        }
    }

    private void TrySubscribeToDesktopEvents()
    {
        try
        {
            VirtualDesktop.Created += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Destroyed += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.CurrentChanged += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Switched += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Moved += (_, _) => RaiseDesktopsChanged();
            VirtualDesktop.Renamed += (_, _) => RaiseDesktopsChanged();
        }
        catch
        {
            // GetDesktops surfaces the compatibility error in the UI; startup should stay alive.
        }
    }

    private void RaiseDesktopsChanged()
    {
        DesktopsChanged?.Invoke(this, EventArgs.Empty);
    }
}
