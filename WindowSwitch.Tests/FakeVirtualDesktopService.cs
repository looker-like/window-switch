using WindowSwitch.Models;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch.Tests;

public sealed partial class MainWindowViewModelTests
{
    private sealed class FakeVirtualDesktopService : IVirtualDesktopService
    {
        public event EventHandler? DesktopsChanged;

        public IReadOnlyList<VirtualDesktopInfo> Desktops { get; set; } = [];

        public Guid? LastSwitchedTo { get; private set; }

        public VirtualDesktopAction? LastAction { get; private set; }

        public IReadOnlyList<VirtualDesktopInfo> GetDesktops()
        {
            return Desktops;
        }

        public void SwitchTo(Guid id)
        {
            LastSwitchedTo = id;
        }

        public void ExecuteAction(VirtualDesktopAction action)
        {
            LastAction = action;
        }

        public void RaiseDesktopsChanged()
        {
            DesktopsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
