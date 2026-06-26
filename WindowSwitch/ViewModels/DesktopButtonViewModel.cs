using WindowSwitch.Models;

namespace WindowSwitch.ViewModels;

public sealed class DesktopButtonViewModel : ObservableObject
{
    private bool _isGestureSelected;

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

    public bool IsGestureSelected
    {
        get => _isGestureSelected;
        set => SetProperty(ref _isGestureSelected, value);
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"Desktop {Index}" : Name;
}
