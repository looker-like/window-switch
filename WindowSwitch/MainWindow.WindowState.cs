using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch;

public partial class MainWindow
{
    private void ApplyWindowPosition()
    {
        if (IsReasonablePosition(_initialSettings.Left, _initialSettings.Top))
        {
            Left = _initialSettings.Left;
            Top = _initialSettings.Top;
            return;
        }

        UpdateLayout();
        var workArea = SystemParameters.WorkArea;
        Left = Math.Max(workArea.Left + 12, workArea.Right - ActualWidth - 24);
        Top = workArea.Top + 80;
    }

    public void ShowFromBackground()
    {
        Show();
        WindowState = WindowState.Normal;
        PositionCenterOnMouse();
        ApplyRuntimeSettings();
        ApplyAltTabHiddenWindowStyles();
        PinWindowToAllVirtualDesktops();
        _viewModel.Refresh();
        StartRefreshTimer();
    }

    public void HideToBackground()
    {
        CloseSettingsWindow();
        _refreshTimer.Stop();
        Hide();
        SaveCurrentSettings();
    }

    private static bool IsReasonablePosition(double left, double top)
    {
        if (double.IsNaN(left) || double.IsInfinity(left) ||
            double.IsNaN(top) || double.IsInfinity(top))
        {
            return false;
        }

        var minLeft = SystemParameters.VirtualScreenLeft - 64;
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 64;
        var minTop = SystemParameters.VirtualScreenTop - 64;
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 64;

        return left >= minLeft && left <= maxLeft && top >= minTop && top <= maxTop;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.IsFloatingTopmost) or
            nameof(MainWindowViewModel.ColumnsPerRow) or
            nameof(MainWindowViewModel.EnableColoredDesktopLabels) or
            nameof(MainWindowViewModel.WindowOpacity) or
            nameof(MainWindowViewModel.StartHidden) or
            nameof(MainWindowViewModel.AutoHideAfterSwitch) or
            nameof(MainWindowViewModel.IsHotkeyEnabled) or
            nameof(MainWindowViewModel.IsDesktopHotkeysEnabled) or
            nameof(MainWindowViewModel.ShowHotkeyKind) or
            nameof(MainWindowViewModel.ShowHotkeyModifiers) or
            nameof(MainWindowViewModel.ShowHotkeyVirtualKey) or
            nameof(MainWindowViewModel.ShowHotkeyMouseButton) or
            nameof(MainWindowViewModel.DesktopHotkeyModifiers))
        {
            ApplyRuntimeSettings();
            if (e.PropertyName is nameof(MainWindowViewModel.IsHotkeyEnabled) or
                nameof(MainWindowViewModel.IsDesktopHotkeysEnabled) or
                nameof(MainWindowViewModel.ShowHotkeyKind) or
                nameof(MainWindowViewModel.ShowHotkeyModifiers) or
                nameof(MainWindowViewModel.ShowHotkeyVirtualKey) or
                nameof(MainWindowViewModel.ShowHotkeyMouseButton) or
                nameof(MainWindowViewModel.DesktopHotkeyModifiers))
            {
                ApplyHotkeyRegistration();
            }

            ScheduleSettingsSave();
        }
    }

    private void ViewModel_DesktopSwitchCompleted(object? sender, EventArgs e)
    {
        PinWindowToAllVirtualDesktops();
        if (_viewModel.AutoHideAfterSwitch)
        {
            HideToBackground();
        }
    }

    private void ApplyRuntimeSettings()
    {
        Topmost = _viewModel.IsFloatingTopmost;
        Opacity = _viewModel.WindowOpacity;
        ApplyTopmostWithoutActivation();
    }

    private void ApplyContentMinHeight()
    {
        var boundedContentMinHeight = ApplyContentHeightBounds();
        GrowToFitContent(boundedContentMinHeight);
    }

    private double ApplyContentHeightBounds()
    {
        var chromeVertical = RootChrome.Padding.Top + RootChrome.Padding.Bottom +
            RootChrome.BorderThickness.Top + RootChrome.BorderThickness.Bottom;
        var headerHeight = HeaderBar.ActualHeight + HeaderBar.Margin.Top + HeaderBar.Margin.Bottom;
        var statusHeight = StatusBlock.IsVisible
            ? StatusBlock.ActualHeight + StatusBlock.Margin.Top + StatusBlock.Margin.Bottom
            : 0;
        var overlayHeight = DesktopOverlay.GetRequiredContentHeight();
        var contentMinHeight = Math.Ceiling(chromeVertical + headerHeight + statusHeight + overlayHeight);
        var usableMaxHeight = GetUsableMaxHeight();
        var boundedContentMinHeight = Math.Min(contentMinHeight, usableMaxHeight);

        if (!MaxHeight.Equals(usableMaxHeight))
        {
            MaxHeight = usableMaxHeight;
        }

        if (boundedContentMinHeight > 0 && !MinHeight.Equals(boundedContentMinHeight))
        {
            MinHeight = boundedContentMinHeight;
        }

        return boundedContentMinHeight;
    }

    private double GetUsableMaxHeight()
    {
        return Math.Max(160, SystemParameters.WorkArea.Height);
    }

    private void GrowToFitContent(double contentMinHeight)
    {
        if (contentMinHeight <= 0)
        {
            return;
        }

        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
        if (currentHeight >= contentMinHeight)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var targetHeight = Math.Min(contentMinHeight, MaxHeight);
        if (targetHeight <= currentHeight)
        {
            return;
        }

        Height = targetHeight;
        if (Top + targetHeight > workArea.Bottom)
        {
            Top = Math.Max(workArea.Top, workArea.Bottom - targetHeight);
        }
    }

    private void FitHeightToContent(bool keepBottom)
    {
        UpdateLayout();
        var targetHeight = ApplyContentHeightBounds();
        if (targetHeight <= 0)
        {
            return;
        }

        var currentHeight = ActualHeight > 0 ? ActualHeight : Height;
        var bottom = Top + currentHeight;
        Height = targetHeight;

        var workArea = SystemParameters.WorkArea;
        if (keepBottom)
        {
            Top = Math.Min(Math.Max(bottom - targetHeight, workArea.Top), Math.Max(workArea.Top, workArea.Bottom - targetHeight));
            return;
        }

        if (Top + targetHeight > workArea.Bottom)
        {
            Top = Math.Max(workArea.Top, workArea.Bottom - targetHeight);
        }
    }

    private void StartRefreshTimer()
    {
        if (!_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
        }
    }

    private void ScheduleSettingsSave()
    {
        if (!IsLoaded)
        {
            return;
        }

        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private void SaveCurrentSettings()
    {
        _settingsStore.Save(new WindowSettings(
            Left,
            Top,
            _viewModel.IsFloatingTopmost,
            _viewModel.ColumnsPerRow,
            _viewModel.WindowOpacity,
            _viewModel.StartHidden,
            _viewModel.AutoHideAfterSwitch,
            _viewModel.EnableColoredDesktopLabels,
            _viewModel.IsHotkeyEnabled,
            _viewModel.IsDesktopHotkeysEnabled,
            _viewModel.ShowHotkeyModifiers,
            _viewModel.ShowHotkeyVirtualKey,
            _viewModel.DesktopHotkeyModifiers,
            (int)_viewModel.ShowHotkeyKind,
            (int)_viewModel.ShowHotkeyMouseButton));
    }

}
