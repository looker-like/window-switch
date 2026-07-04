using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using WindowSwitch.Controls;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;
using WindowsDesktop;
using Forms = System.Windows.Forms;

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
    private const int WmNchitTest = 0x0084;
    private const int WmNcLeftButtonDoubleClick = 0x00A3;
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
    private const int HtClient = 1;
    private const int HtLeft = 10;
    private const int HtRight = 11;
    private const int HtTop = 12;
    private const int HtTopLeft = 13;
    private const int HtTopRight = 14;
    private const int HtBottom = 15;
    private const int HtBottomLeft = 16;
    private const int HtBottomRight = 17;
    private const int ResizeBorderThickness = 8;
    private const int XButton1 = 0x0001;
    private const int XButton2 = 0x0002;
    private static readonly IntPtr HwndTopMost = new(-1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly LowLevelKeyboardProc _keyboardHookCallback;
    private readonly LowLevelMouseProc _mouseHookCallback;
    private readonly IWindowSettingsStore _settingsStore;
    private readonly object _mouseGestureQueueLock = new();
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
    private SettingsWindow? _settingsWindow;
    private bool _isMouseActivationGestureActive;
    private bool _wasVisibleBeforeMouseGesture;
    private Guid? _mouseGestureSelectedDesktopId;
    private VirtualDesktopAction? _mouseGestureSelectedAction;
    private MouseHotkeyButton? _activeMouseActivationButton;
    private NativePoint _pendingMouseGesturePoint;
    private bool _isMouseGestureUpdateQueued;

    public MainWindow(MainWindowViewModel viewModel, IWindowSettingsStore settingsStore, WindowSettings initialSettings)
    {
        _viewModel = viewModel;
        _settingsStore = settingsStore;
        _initialSettings = initialSettings;
        _keyboardHookCallback = KeyboardHookProc;
        _mouseHookCallback = MouseHookProc;

        InitializeComponent();
        AppIcons.ApplyTo(this);
        HeaderAppIcon.Source = AppIcons.ImageSource;
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
        LayoutUpdated += MainWindow_LayoutUpdated;
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

    private void MainWindow_LayoutUpdated(object? sender, EventArgs e)
    {
        ApplyContentMinHeight();
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
        ShowSettingsWindow();
    }

    private void DesktopOverlay_DesktopInvoked(object sender, OverlayDesktopInvokedEventArgs e)
    {
        _viewModel.SwitchDesktopCommand.Execute(e.DesktopId);
    }

    private void DesktopOverlay_ActionInvoked(object sender, OverlayActionInvokedEventArgs e)
    {
        _viewModel.ExecuteVirtualDesktopActionCommand.Execute(e.Action);
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_viewModel)
        {
            Owner = this,
        };
        settingsWindow.RecordShowHotkeyRequested += (_, _) => ToggleShowHotkeyCapture();
        settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow = settingsWindow;
        settingsWindow.Show();
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
}
