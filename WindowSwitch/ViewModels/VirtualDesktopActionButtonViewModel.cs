using WindowSwitch.Services;

namespace WindowSwitch.ViewModels;

public sealed class VirtualDesktopActionButtonViewModel : ObservableObject
{
    private bool _isGestureSelected;

    public VirtualDesktopActionButtonViewModel(
        VirtualDesktopAction action,
        string iconGlyph,
        string displayName,
        string shortcutText)
    {
        Action = action;
        IconGlyph = iconGlyph;
        DisplayName = displayName;
        ShortcutText = shortcutText;
    }

    public VirtualDesktopAction Action { get; }

    public string IconGlyph { get; }

    public string DisplayName { get; }

    public string ShortcutText { get; }

    public string ToolTipText => $"{DisplayName} ({ShortcutText})";

    public bool IsGestureSelected
    {
        get => _isGestureSelected;
        set => SetProperty(ref _isGestureSelected, value);
    }
}
