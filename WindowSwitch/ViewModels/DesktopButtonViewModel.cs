using WindowSwitch.Models;

namespace WindowSwitch.ViewModels;

public sealed class DesktopButtonViewModel
{
    public DesktopButtonViewModel(VirtualDesktopInfo desktop)
    {
        Id = desktop.Id;
        Index = desktop.Index;
        Name = desktop.Name;
        IsCurrent = desktop.IsCurrent;
    }

    public Guid Id { get; }

    public int Index { get; }

    public string Name { get; }

    public bool IsCurrent { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Desktop {Index}" : Name;
}
