using WindowSwitch.Models;

namespace WindowSwitch.Services;

public interface IVirtualDesktopService
{
    event EventHandler? DesktopsChanged;

    IReadOnlyList<VirtualDesktopInfo> GetDesktops();

    void SwitchTo(Guid id);
}
