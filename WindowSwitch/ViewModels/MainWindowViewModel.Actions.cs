using WindowSwitch.Services;

namespace WindowSwitch.ViewModels;

public sealed partial class MainWindowViewModel
{
    public void Refresh()
    {
        try
        {
            var desktops = _desktopService.GetDesktops()
                .OrderBy(desktop => desktop.Index)
                .Select(desktop => new DesktopButtonViewModel(desktop))
                .ToArray();

            Desktops.Clear();
            foreach (var desktop in desktops)
            {
                Desktops.Add(desktop);
            }

            OnPropertyChanged(nameof(CurrentDesktopName));
        }

        catch (Exception ex)
        {
            Desktops.Clear();
            SetStatus(ex.Message);
        }
    }

    public void SetStatus(string message)
    {
        StatusMessage = message;
        HasStatus = !string.IsNullOrWhiteSpace(message);
    }

    public void SetHotkeyStatus(string message)
    {
        HotkeyStatusMessage = message;
        HasHotkeyStatus = !string.IsNullOrWhiteSpace(message);
    }

    public void ApplyCapturedShowHotkey(CapturedShowHotkey hotkey)
    {
        ShowHotkeyKind = hotkey.Kind;
        if (hotkey.Kind == ShowHotkeyKind.MouseButton)
        {
            ShowHotkeyMouseButton = hotkey.MouseButton;
            return;
        }

        ShowHotkeyModifiers = hotkey.Modifiers;
        ShowHotkeyVirtualKey = hotkey.VirtualKey;
    }

    public void ClearGestureSelection()
    {
        SetGestureSelectedDesktop(null);
        SetGestureSelectedVirtualDesktopAction(null);
    }

    public void SetGestureSelectedDesktop(Guid? id)
    {
        foreach (var desktop in Desktops)
        {
            desktop.IsGestureSelected = id.HasValue && desktop.Id == id.Value;
        }
    }

    public void SetGestureSelectedVirtualDesktopAction(VirtualDesktopAction? action)
    {
        foreach (var virtualDesktopAction in VirtualDesktopActions)
        {
            virtualDesktopAction.IsGestureSelected = action.HasValue && virtualDesktopAction.Action == action.Value;
        }
    }

    public void Dispose()
    {
        _desktopService.DesktopsChanged -= _desktopsChangedHandler;
    }

    private void SwitchDesktop(object? parameter)
    {
        if (parameter is not Guid id)
        {
            return;
        }

        try
        {
            _desktopService.SwitchTo(id);
            Refresh();
            DesktopSwitchCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void ExecuteVirtualDesktopAction(object? parameter)
    {
        if (parameter is not VirtualDesktopAction action)
        {
            return;
        }

        try
        {
            _desktopService.ExecuteAction(action);
            Refresh();
            DesktopSwitchCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
    }

    private void ApplySettings(WindowSettings settings)
    {
        _isFloatingTopmost = settings.IsFloatingTopmost;
        _startHidden = settings.StartHidden;
        _autoHideAfterSwitch = settings.AutoHideAfterSwitch;
        _enableColoredDesktopLabels = settings.EnableColoredDesktopLabels;
        _isHotkeyEnabled = settings.IsHotkeyEnabled;
        _isDesktopHotkeysEnabled = settings.IsDesktopHotkeysEnabled;
        _showHotkeyKind = HotkeyDefinitions.NormalizeShowHotkeyKind(settings.ShowHotkeyKind);
        _showHotkeyModifiers = HotkeyDefinitions.NormalizeModifiers(settings.ShowHotkeyModifiers);
        _showHotkeyVirtualKey = HotkeyDefinitions.NormalizeVirtualKey(settings.ShowHotkeyVirtualKey);
        _showHotkeyMouseButton = HotkeyDefinitions.NormalizeMouseButton(settings.ShowHotkeyMouseButton);
        _desktopHotkeyModifiers = HotkeyDefinitions.NormalizeModifiers(settings.DesktopHotkeyModifiers);
        _columnsPerRow = Clamp(settings.ColumnsPerRow, MinColumnsPerRow, MaxColumnsPerRow);
        _windowOpacity = Clamp(settings.WindowOpacity, MinWindowOpacity, MaxWindowOpacity);
    }

    private static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    private static double Clamp(double value, double min, double max)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return WindowSettings.DefaultWindowOpacity;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
