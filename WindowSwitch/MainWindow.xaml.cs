using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;
using WindowsDesktop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WindowSwitch;

public partial class MainWindow : Window
{
    private const int HotkeyId = 0x5753;
    private const int DesktopHotkeyFirstId = 0x5801;
    private const int DesktopHotkeyLastId = 0x5809;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint VkSpace = 0x20;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly IWindowSettingsStore _settingsStore;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly MainWindowViewModel _viewModel;
    private readonly WindowSettings _initialSettings;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly List<int> _registeredHotkeyIds = [];
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private bool _isExitRequested;
    private bool _hasAppliedInitialVisibility;
    private bool _hasPinnedWindowToAllDesktops;

    public MainWindow(MainWindowViewModel viewModel, IWindowSettingsStore settingsStore, WindowSettings initialSettings)
    {
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        _initialSettings = initialSettings;

        InitializeComponent();
        DataContext = viewModel;

        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _refreshTimer.Tick += (_, _) =>
        {
            _viewModel.Refresh();
        };

        _settingsSaveTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _settingsSaveTimer.Tick += (_, _) =>
        {
            _settingsSaveTimer.Stop();
            SaveCurrentSettings();
        };

        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        LocationChanged += (_, _) => ScheduleSettingsSave();
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _viewModel.DesktopSwitchCompleted += ViewModel_DesktopSwitchCompleted;

        _notifyIcon = CreateNotifyIcon();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        _windowHandle = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);

        ApplyAltTabHiddenWindowStyles();
        ApplyHotkeyRegistration();
        PinWindowToAllVirtualDesktops();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyWindowPosition();
        ApplyRuntimeSettings();
        _viewModel.Refresh();
        PinWindowToAllVirtualDesktops();

        if (!_hasAppliedInitialVisibility)
        {
            _hasAppliedInitialVisibility = true;
            if (_viewModel.StartHidden)
            {
                Dispatcher.BeginInvoke(HideToBackground, DispatcherPriority.ApplicationIdle);
                return;
            }
        }

        StartRefreshTimer();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExitRequested)
        {
            e.Cancel = true;
            HideToBackground();
            return;
        }

        TearDown();
    }

    private void RootChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed || IsInsideButton(e.OriginalSource))
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw if the mouse state changes during the drag.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        HideToBackground();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = SettingsButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

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
        CloseSettingsPanel();
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
            nameof(MainWindowViewModel.WindowOpacity) or
            nameof(MainWindowViewModel.StartHidden) or
            nameof(MainWindowViewModel.AutoHideAfterSwitch) or
            nameof(MainWindowViewModel.IsHotkeyEnabled) or
            nameof(MainWindowViewModel.IsDesktopHotkeysEnabled))
        {
            ApplyRuntimeSettings();
            if (e.PropertyName is nameof(MainWindowViewModel.IsHotkeyEnabled) or
                nameof(MainWindowViewModel.IsDesktopHotkeysEnabled))
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
            _viewModel.IsHotkeyEnabled,
            _viewModel.IsDesktopHotkeysEnabled));
    }

    private void PositionCenterOnMouse()
    {
        UpdateLayout();

        var mouse = Forms.Cursor.Position;
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var center = transform.Transform(new System.Windows.Point(mouse.X, mouse.Y));

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        var targetLeft = center.X - width / 2;
        var targetTop = center.Y - height / 2;

        var minLeft = SystemParameters.VirtualScreenLeft;
        var maxLeft = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - width;
        var minTop = SystemParameters.VirtualScreenTop;
        var maxTop = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - height;

        Left = Math.Min(Math.Max(targetLeft, minLeft), maxLeft);
        Top = Math.Min(Math.Max(targetTop, minTop), maxTop);
    }

    private void PinWindowToAllVirtualDesktops()
    {
        if (_windowHandle == IntPtr.Zero || _hasPinnedWindowToAllDesktops)
        {
            return;
        }

        try
        {
            if (!VirtualDesktop.IsPinnedWindow(_windowHandle))
            {
                VirtualDesktop.PinWindow(_windowHandle);
            }

            _hasPinnedWindowToAllDesktops = VirtualDesktop.IsPinnedWindow(_windowHandle);
            if (!_hasPinnedWindowToAllDesktops)
            {
                _viewModel.SetStatus("窗口跨虚拟桌面显示未生效：系统没有确认该窗口已固定到所有桌面。");
            }
        }
        catch (Exception ex)
        {
            _viewModel.SetStatus($"窗口跨虚拟桌面显示失败：{ex.Message}");
        }
    }

    private void ApplyAltTabHiddenWindowStyles()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var exStyle = GetWindowLongPtr(_windowHandle, GwlExStyle).ToInt64();
        exStyle |= WsExToolWindow | WsExNoActivate;
        exStyle &= ~WsExAppWindow;
        SetWindowLongPtr(_windowHandle, GwlExStyle, new IntPtr(exStyle));
        SetWindowPos(
            _windowHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate | SwpFrameChanged);
    }

    private void ApplyTopmostWithoutActivation()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            _windowHandle,
            _viewModel.IsFloatingTopmost ? HwndTopMost : HwndNoTopMost,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    private void CloseSettingsPanel()
    {
        SettingsButton.IsChecked = false;
        SettingsPanel.Visibility = Visibility.Collapsed;
    }

    private void ApplyHotkeyRegistration()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        UnregisterHotkeys();

        var failedHotkeys = new List<string>();
        if (_viewModel.IsHotkeyEnabled &&
            !TryRegisterHotkey(HotkeyId, ModControl | ModAlt, VkSpace))
        {
            failedHotkeys.Add(_viewModel.HotkeyText);
        }

        if (_viewModel.IsDesktopHotkeysEnabled)
        {
            for (var index = 1; index <= 9; index++)
            {
                var key = (uint)('0' + index);
                if (!TryRegisterHotkey(DesktopHotkeyFirstId + index - 1, ModControl | ModAlt, key))
                {
                    failedHotkeys.Add($"Ctrl + Alt + {index}");
                }
            }
        }

        if (failedHotkeys.Count > 0)
        {
            _viewModel.SetStatus($"快捷键注册失败：{string.Join(", ", failedHotkeys)}。可能已被其他应用占用。");
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var hotkeyId = wParam.ToInt32();
        if (hotkeyId == HotkeyId)
        {
            ShowFromBackground();
            handled = true;
        }
        else if (hotkeyId is >= DesktopHotkeyFirstId and <= DesktopHotkeyLastId)
        {
            SwitchDesktopFromHotkey(hotkeyId - DesktopHotkeyFirstId + 1);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private bool TryRegisterHotkey(int id, uint modifiers, uint key)
    {
        if (!RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            return false;
        }

        _registeredHotkeyIds.Add(id);
        return true;
    }

    private void UnregisterHotkeys()
    {
        foreach (var id in _registeredHotkeyIds)
        {
            UnregisterHotKey(_windowHandle, id);
        }

        _registeredHotkeyIds.Clear();
    }

    private void SwitchDesktopFromHotkey(int desktopIndex)
    {
        var desktop = _viewModel.Desktops.FirstOrDefault(item => item.Index == desktopIndex);
        if (desktop is not null)
        {
            _viewModel.SwitchDesktopCommand.Execute(desktop.Id);
        }
    }

    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => Dispatcher.Invoke(ShowFromBackground));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Text = "WindowSwitch",
            Visible = true,
            ContextMenuStrip = menu,
        };

        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromBackground);
        return notifyIcon;
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        Close();
    }

    private void TearDown()
    {
        _refreshTimer.Stop();
        _settingsSaveTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.DesktopSwitchCompleted -= ViewModel_DesktopSwitchCompleted;
        _hwndSource?.RemoveHook(WndProc);
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotkeys();
        }

        SaveCurrentSettings();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    private static bool IsInsideButton(object source)
    {
        var current = source as DependencyObject;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase or Selector)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);
}
