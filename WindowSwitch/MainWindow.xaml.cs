using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.IO;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;
using WindowsDesktop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace WindowSwitch;

public partial class MainWindow : Window
{
    private const int ShowHotkeyId = 0x5753;
    private const int DesktopHotkeyFirstId = 0x5800;
    private const int DesktopHotkeyLastId = 0x5809;
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int HcAction = 0;
    private const int WmHotkey = 0x0312;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmMouseMove = 0x0200;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const uint Vk0 = 0x30;
    private const int VkEscape = 0x1B;
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLeftWindows = 0x5B;
    private const int VkRightWindows = 0x5C;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExAppWindow = 0x00040000;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly LowLevelKeyboardProc _keyboardHookCallback;
    private readonly LowLevelMouseProc _mouseHookCallback;
    private readonly IWindowSettingsStore _settingsStore;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _settingsSaveTimer;
    private readonly DispatcherTimer _desktopHotkeySequenceTimer;
    private readonly DesktopHotkeySequence _desktopHotkeySequence = new();
    private readonly MainWindowViewModel _viewModel;
    private readonly WindowSettings _initialSettings;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly List<int> _registeredHotkeyIds = [];
    private HwndSource? _hwndSource;
    private IntPtr _windowHandle;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private bool _isExitRequested;
    private bool _hasAppliedInitialVisibility;
    private bool _hasPinnedWindowToAllDesktops;
    private bool _isMouseActivationGestureActive;
    private bool _wasVisibleBeforeMouseGesture;
    private Guid? _mouseGestureSelectedDesktopId;
    private MouseHotkeyButton? _activeMouseActivationButton;

    public MainWindow(MainWindowViewModel viewModel, IWindowSettingsStore settingsStore, WindowSettings initialSettings)
    {
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        _initialSettings = initialSettings;
        _keyboardHookCallback = KeyboardHookProc;
        _mouseHookCallback = MouseHookProc;

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

        _desktopHotkeySequenceTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(50),
        };
        _desktopHotkeySequenceTimer.Tick += (_, _) =>
        {
            if (!AreRequiredModifiersPressed(_viewModel.DesktopHotkeyModifiers))
            {
                ResetDesktopHotkeySequence();
            }
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

    private void RecordShowHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsCapturingShowHotkey)
        {
            EndShowHotkeyCapture("已取消监听。");
            return;
        }

        BeginShowHotkeyCapture();
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
            _viewModel.IsDesktopHotkeysEnabled,
            _viewModel.ShowHotkeyModifiers,
            _viewModel.ShowHotkeyVirtualKey,
            _viewModel.DesktopHotkeyModifiers,
            (int)_viewModel.ShowHotkeyKind,
            (int)_viewModel.ShowHotkeyMouseButton));
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
            if (IsNonActionableVirtualDesktopPinError(ex))
            {
                return;
            }

            _viewModel.SetStatus($"窗口跨虚拟桌面显示失败：{ex.Message}");
        }
    }

    private static bool IsNonActionableVirtualDesktopPinError(Exception ex)
    {
        return ex is COMException { HResult: unchecked((int)0x8002802B) };
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

    private void BeginShowHotkeyCapture()
    {
        _viewModel.IsCapturingShowHotkey = true;
        _viewModel.SetHotkeyStatus("正在监听唤起快捷键。");

        var keyboardReady = EnsureKeyboardHook();
        var mouseReady = EnsureMouseHook();
        if (!keyboardReady || !mouseReady)
        {
            EndShowHotkeyCapture("监听启动失败，无法录制新的唤起快捷键。");
        }
    }

    private void EndShowHotkeyCapture(string? statusMessage = null)
    {
        _viewModel.IsCapturingShowHotkey = false;
        if (statusMessage is not null)
        {
            _viewModel.SetHotkeyStatus(statusMessage);
        }

        UpdateInputHookLifetime();
    }

    private void CompleteShowHotkeyCapture(CapturedShowHotkey hotkey)
    {
        _viewModel.IsCapturingShowHotkey = false;
        _viewModel.ApplyCapturedShowHotkey(hotkey);
        ScheduleSettingsSave();
        UpdateInputHookLifetime();
    }

    private void CaptureKeyboardHotkey(int virtualKey)
    {
        if (!_viewModel.IsCapturingShowHotkey)
        {
            return;
        }

        if (virtualKey == VkEscape)
        {
            EndShowHotkeyCapture("已取消监听。");
            return;
        }

        if (!HotkeyDefinitions.IsSupportedMainVirtualKey(virtualKey))
        {
            return;
        }

        var modifiers = GetPressedHotkeyModifiers();
        if (modifiers == 0)
        {
            _viewModel.SetHotkeyStatus("键盘唤起快捷键需要同时按住 Ctrl、Alt、Shift 或 Win。");
            return;
        }

        CompleteShowHotkeyCapture(new CapturedShowHotkey(
            ShowHotkeyKind.Keyboard,
            modifiers,
            virtualKey,
            _viewModel.ShowHotkeyMouseButton));
    }

    private void CaptureMouseHotkey(MouseHotkeyButton button)
    {
        if (!_viewModel.IsCapturingShowHotkey)
        {
            return;
        }

        CompleteShowHotkeyCapture(new CapturedShowHotkey(
            ShowHotkeyKind.MouseButton,
            _viewModel.ShowHotkeyModifiers,
            _viewModel.ShowHotkeyVirtualKey,
            button));
    }

    private void BeginMouseActivationGesture(MouseHotkeyButton button, NativePoint point)
    {
        if (_isMouseActivationGestureActive || _viewModel.IsCapturingShowHotkey)
        {
            return;
        }

        _isMouseActivationGestureActive = true;
        _activeMouseActivationButton = button;
        _wasVisibleBeforeMouseGesture = IsVisible;
        _mouseGestureSelectedDesktopId = null;

        ShowFromBackground();
        UpdateMouseGestureSelection(point);
    }

    private void UpdateMouseGestureSelection(NativePoint point)
    {
        if (!_isMouseActivationGestureActive)
        {
            return;
        }

        var desktop = GetDesktopUnderScreenPoint(point);
        _mouseGestureSelectedDesktopId = desktop?.Id;
        _viewModel.SetGestureSelectedDesktop(_mouseGestureSelectedDesktopId);
    }

    private void CompleteMouseActivationGesture(NativePoint point)
    {
        if (!_isMouseActivationGestureActive)
        {
            return;
        }

        UpdateMouseGestureSelection(point);
        var selectedId = _mouseGestureSelectedDesktopId;
        var shouldHideAfterGesture = !_wasVisibleBeforeMouseGesture;

        _isMouseActivationGestureActive = false;
        _activeMouseActivationButton = null;
        _mouseGestureSelectedDesktopId = null;
        _viewModel.ClearGestureSelection();

        if (selectedId is Guid id)
        {
            _viewModel.SwitchDesktopCommand.Execute(id);
        }

        if (shouldHideAfterGesture && !_viewModel.AutoHideAfterSwitch)
        {
            HideToBackground();
        }
    }

    private DesktopButtonViewModel? GetDesktopUnderScreenPoint(NativePoint point)
    {
        if (!IsVisible)
        {
            return null;
        }

        var hitPoint = DesktopItems.PointFromScreen(new System.Windows.Point(point.X, point.Y));
        var hit = DesktopItems.InputHitTest(hitPoint) as DependencyObject;
        while (hit is not null)
        {
            if (hit is FrameworkElement { DataContext: DesktopButtonViewModel desktop })
            {
                return desktop;
            }

            hit = VisualTreeHelper.GetParent(hit);
        }

        return null;
    }

    private void ApplyHotkeyRegistration()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        ResetDesktopHotkeySequence();
        UnregisterHotkeys();

        var failedHotkeys = new List<string>();
        var registeredHotkeys = new List<string>();

        if (_viewModel.IsHotkeyEnabled)
        {
            if (_viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton)
            {
                if (EnsureMouseHook())
                {
                    registeredHotkeys.Add($"{_viewModel.HotkeyText}（按住滑动选择）");
                }
                else
                {
                    failedHotkeys.Add(_viewModel.HotkeyText);
                }
            }
            else
            {
                if (TryRegisterHotkey(
                    ShowHotkeyId,
                    HotkeyDefinitions.ToRegisterHotkeyModifiers(_viewModel.ShowHotkeyModifiers),
                    (uint)_viewModel.ShowHotkeyVirtualKey))
                {
                    registeredHotkeys.Add(_viewModel.HotkeyText);
                }
                else
                {
                    failedHotkeys.Add(_viewModel.HotkeyText);
                }
            }
        }

        if (_viewModel.IsDesktopHotkeysEnabled)
        {
            for (var index = 1; index <= 9; index++)
            {
                RegisterDesktopDigitHotkey(index, failedHotkeys);
            }

            RegisterDesktopDigitHotkey(0, failedHotkeys);
            registeredHotkeys.Add($"{_viewModel.DesktopHotkeyText}（0=第10桌面，支持两位）");
        }

        if (failedHotkeys.Count > 0)
        {
            _viewModel.SetHotkeyStatus($"快捷键冲突：{string.Join(", ", failedHotkeys)} 已被其他应用占用或被系统保留。");
        }
        else if (registeredHotkeys.Count > 0)
        {
            _viewModel.SetHotkeyStatus($"快捷键可用：{string.Join("；", registeredHotkeys)}");
        }
        else
        {
            _viewModel.SetHotkeyStatus(string.Empty);
        }

        UpdateInputHookLifetime();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var hotkeyId = wParam.ToInt32();
        if (hotkeyId == ShowHotkeyId)
        {
            ShowFromBackground();
            handled = true;
        }
        else if (hotkeyId is >= DesktopHotkeyFirstId and <= DesktopHotkeyLastId)
        {
            HandleDesktopHotkeyDigit(hotkeyId - DesktopHotkeyFirstId);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private void RegisterDesktopDigitHotkey(int digit, List<string> failedHotkeys)
    {
        if (TryRegisterHotkey(
            DesktopHotkeyFirstId + digit,
            HotkeyDefinitions.ToRegisterHotkeyModifiers(_viewModel.DesktopHotkeyModifiers),
            DigitToVirtualKey(digit)))
        {
            return;
        }

        failedHotkeys.Add($"{HotkeyDefinitions.FormatModifiers(_viewModel.DesktopHotkeyModifiers)} + {digit}");
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

    private bool EnsureKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            return true;
        }

        _keyboardHook = SetKeyboardHook(
            WhKeyboardLl,
            _keyboardHookCallback,
            GetModuleHandle(null),
            0);
        return _keyboardHook != IntPtr.Zero;
    }

    private bool EnsureMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            return true;
        }

        _mouseHook = SetMouseHook(
            WhMouseLl,
            _mouseHookCallback,
            GetModuleHandle(null),
            0);
        return _mouseHook != IntPtr.Zero;
    }

    private void UpdateInputHookLifetime()
    {
        if (_viewModel.IsCapturingShowHotkey)
        {
            EnsureKeyboardHook();
            EnsureMouseHook();
            return;
        }

        ReleaseKeyboardHook();
        if (_viewModel.IsHotkeyEnabled && _viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton)
        {
            EnsureMouseHook();
            return;
        }

        ReleaseMouseHook();
    }

    private void ReleaseKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private void ReleaseMouseHook()
    {
        if (_mouseHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HcAction &&
            _viewModel.IsCapturingShowHotkey &&
            wParam.ToInt32() is WmKeyDown or WmSysKeyDown)
        {
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            Dispatcher.BeginInvoke(() => CaptureKeyboardHotkey(data.VirtualKeyCode), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HcAction)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<MouseHookData>(lParam);

        if (_viewModel.IsCapturingShowHotkey &&
            TryGetMouseButton(message, data.MouseData, out var capturedButton, out var isCaptureDown, out _) &&
            isCaptureDown)
        {
            Dispatcher.BeginInvoke(() => CaptureMouseHotkey(capturedButton), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        if (_isMouseActivationGestureActive)
        {
            if (message == WmMouseMove)
            {
                Dispatcher.BeginInvoke(() => UpdateMouseGestureSelection(data.Point), DispatcherPriority.Input);
            }
            else if (TryGetMouseButton(message, data.MouseData, out var button, out _, out var isUp) &&
                isUp &&
                button == _activeMouseActivationButton)
            {
                Dispatcher.BeginInvoke(() => CompleteMouseActivationGesture(data.Point), DispatcherPriority.Input);
                return new IntPtr(1);
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        if (_viewModel.IsHotkeyEnabled &&
            _viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton &&
            TryGetMouseButton(message, data.MouseData, out var activationButton, out var isDown, out _) &&
            isDown &&
            activationButton == _viewModel.ShowHotkeyMouseButton)
        {
            Dispatcher.BeginInvoke(() => BeginMouseActivationGesture(activationButton, data.Point), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool TryGetMouseButton(
        int message,
        int mouseData,
        out MouseHotkeyButton button,
        out bool isDown,
        out bool isUp)
    {
        isDown = message is WmLeftButtonDown or WmRightButtonDown or WmMiddleButtonDown or WmXButtonDown;
        isUp = message is WmLeftButtonUp or WmRightButtonUp or WmMiddleButtonUp or WmXButtonUp;
        button = MouseHotkeyButton.Left;

        switch (message)
        {
            case WmLeftButtonDown:
            case WmLeftButtonUp:
                button = MouseHotkeyButton.Left;
                return true;
            case WmRightButtonDown:
            case WmRightButtonUp:
                button = MouseHotkeyButton.Right;
                return true;
            case WmMiddleButtonDown:
            case WmMiddleButtonUp:
                button = MouseHotkeyButton.Middle;
                return true;
            case WmXButtonDown:
            case WmXButtonUp:
                var xButton = (mouseData >> 16) & 0xFFFF;
                button = xButton == XButton2 ? MouseHotkeyButton.XButton2 : MouseHotkeyButton.XButton1;
                return xButton is XButton1 or XButton2;
            default:
                return false;
        }
    }

    private void HandleDesktopHotkeyDigit(int digit)
    {
        _viewModel.Refresh();
        var availableDesktopIndexes = _viewModel.Desktops.Select(desktop => desktop.Index).ToArray();
        var desktopIndex = _desktopHotkeySequence.HandleDigit(digit, availableDesktopIndexes);

        if (_desktopHotkeySequence.IsListening)
        {
            if (!_desktopHotkeySequenceTimer.IsEnabled)
            {
                _desktopHotkeySequenceTimer.Start();
            }
        }
        else
        {
            _desktopHotkeySequenceTimer.Stop();
        }

        if (desktopIndex is int targetIndex)
        {
            SwitchDesktopFromHotkey(targetIndex);
        }
    }

    private void ResetDesktopHotkeySequence()
    {
        _desktopHotkeySequence.Reset();
        _desktopHotkeySequenceTimer.Stop();
    }

    private static uint DigitToVirtualKey(int digit)
    {
        return Vk0 + (uint)digit;
    }

    private static bool AreRequiredModifiersPressed(int modifiers)
    {
        var normalized = (HotkeyModifiers)HotkeyDefinitions.NormalizeModifiers(modifiers);

        if (normalized.HasFlag(HotkeyModifiers.Control) && !IsKeyPressed(VkControl))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Alt) && !IsKeyPressed(VkMenu))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Shift) && !IsKeyPressed(VkShift))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Windows) &&
            !IsKeyPressed(VkLeftWindows) &&
            !IsKeyPressed(VkRightWindows))
        {
            return false;
        }

        return true;
    }

    private static int GetPressedHotkeyModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if (IsKeyPressed(VkControl))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (IsKeyPressed(VkMenu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (IsKeyPressed(VkShift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (IsKeyPressed(VkLeftWindows) || IsKeyPressed(VkRightWindows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return (int)modifiers;
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
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
            Icon = LoadAppIcon(),
            Text = "WindowSwitch",
            Visible = true,
            ContextMenuStrip = menu,
        };

        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromBackground);
        return notifyIcon;
    }

    private static Drawing.Icon LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        return new Drawing.Icon(iconPath);
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
        _desktopHotkeySequenceTimer.Stop();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.DesktopSwitchCompleted -= ViewModel_DesktopSwitchCompleted;
        _hwndSource?.RemoveHook(WndProc);
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotkeys();
        }

        ReleaseKeyboardHook();
        ReleaseMouseHook();
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

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardHookData
    {
        public readonly int VirtualKeyCode;
        public readonly int ScanCode;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseHookData
    {
        public readonly NativePoint Point;
        public readonly int MouseData;
        public readonly int Flags;
        public readonly int Time;
        public readonly IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetKeyboardHook(
        int idHook,
        LowLevelKeyboardProc lpfn,
        IntPtr hmod,
        uint dwThreadId);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetMouseHook(
        int idHook,
        LowLevelMouseProc lpfn,
        IntPtr hmod,
        uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

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
