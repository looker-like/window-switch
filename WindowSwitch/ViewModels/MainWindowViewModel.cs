using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowSwitch.Commands;
using WindowSwitch.Services;

namespace WindowSwitch.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int MinColumnsPerRow = 1;
    private const int MaxColumnsPerRow = 4;
    private const double MinWindowOpacity = 0.35;
    private const double MaxWindowOpacity = 1.0;

    private readonly IVirtualDesktopService _desktopService;
    private readonly Action<Action> _dispatch;
    private readonly EventHandler _desktopsChangedHandler;
    private string _statusMessage = string.Empty;
    private bool _hasStatus;
    private bool _isFloatingTopmost = true;
    private bool _startHidden = true;
    private bool _autoHideAfterSwitch;
    private bool _enableColoredDesktopLabels;
    private bool _isHotkeyEnabled = true;
    private bool _isDesktopHotkeysEnabled = true;
    private string _hotkeyStatusMessage = string.Empty;
    private bool _hasHotkeyStatus;
    private bool _isCapturingShowHotkey;
    private ShowHotkeyKind _showHotkeyKind = HotkeyDefinitions.DefaultShowHotkeyKind;
    private int _showHotkeyModifiers = HotkeyDefinitions.DefaultHotkeyModifiers;
    private int _showHotkeyVirtualKey = HotkeyDefinitions.DefaultShowHotkeyVirtualKey;
    private MouseHotkeyButton _showHotkeyMouseButton = HotkeyDefinitions.DefaultShowHotkeyMouseButton;
    private int _desktopHotkeyModifiers = HotkeyDefinitions.DefaultHotkeyModifiers;
    private int _columnsPerRow = WindowSettings.DefaultColumnsPerRow;
    private double _windowOpacity = WindowSettings.DefaultWindowOpacity;

    public MainWindowViewModel(
        IVirtualDesktopService desktopService,
        WindowSettings? settings = null,
        Action<Action>? dispatch = null)
    {
        _desktopService = desktopService;
        _dispatch = dispatch ?? (action => action());
        _desktopsChangedHandler = (_, _) => _dispatch(Refresh);

        ApplySettings(settings ?? new WindowSettings());

        SwitchDesktopCommand = new RelayCommand(SwitchDesktop, parameter => parameter is Guid);
        ExecuteVirtualDesktopActionCommand = new RelayCommand(ExecuteVirtualDesktopAction, parameter => parameter is VirtualDesktopAction);
        RefreshCommand = new RelayCommand(_ => Refresh());

        _desktopService.DesktopsChanged += _desktopsChangedHandler;
        Refresh();
    }

    public event EventHandler? DesktopSwitchCompleted;

    public ObservableCollection<DesktopButtonViewModel> Desktops { get; } = [];

    public string? CurrentDesktopName => Desktops.FirstOrDefault(d => d.IsCurrent)?.Name;

    public IReadOnlyList<VirtualDesktopActionButtonViewModel> VirtualDesktopActions { get; } =
    [
        new(VirtualDesktopAction.OpenTaskView, "\uE7C4", "打开任务视图", "Win + Tab"),
        new(VirtualDesktopAction.CreateDesktop, "\uE710", "新建虚拟桌面", "Win + Ctrl + D"),
        new(VirtualDesktopAction.SwitchLeft, "\uE72B", "切到左侧桌面", "Win + Ctrl + ←"),
        new(VirtualDesktopAction.SwitchRight, "\uE72A", "切到右侧桌面", "Win + Ctrl + →"),
        new(VirtualDesktopAction.CloseCurrentDesktop, "\uE711", "关闭当前虚拟桌面", "Win + Ctrl + F4"),
    ];

    public IReadOnlyList<int> ColumnOptions { get; } = [1, 2, 3, 4];

    public IReadOnlyList<HotkeyModifierOption> HotkeyModifierOptions => HotkeyDefinitions.ModifierOptions;

    public IReadOnlyList<HotkeyKeyOption> HotkeyKeyOptions => HotkeyDefinitions.KeyOptions;

    public ICommand SwitchDesktopCommand { get; }

    public ICommand ExecuteVirtualDesktopActionCommand { get; }

    public ICommand RefreshCommand { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasStatus
    {
        get => _hasStatus;
        private set => SetProperty(ref _hasStatus, value);
    }

    public bool IsFloatingTopmost
    {
        get => _isFloatingTopmost;
        set => SetProperty(ref _isFloatingTopmost, value);
    }

    public bool StartHidden
    {
        get => _startHidden;
        set => SetProperty(ref _startHidden, value);
    }

    public bool AutoHideAfterSwitch
    {
        get => _autoHideAfterSwitch;
        set => SetProperty(ref _autoHideAfterSwitch, value);
    }

    public bool EnableColoredDesktopLabels
    {
        get => _enableColoredDesktopLabels;
        set => SetProperty(ref _enableColoredDesktopLabels, value);
    }

    public bool IsHotkeyEnabled
    {
        get => _isHotkeyEnabled;
        set => SetProperty(ref _isHotkeyEnabled, value);
    }

    public ShowHotkeyKind ShowHotkeyKind
    {
        get => _showHotkeyKind;
        private set
        {
            if (SetProperty(ref _showHotkeyKind, value))
            {
                OnPropertyChanged(nameof(HotkeyText));
            }
        }
    }

    public int ShowHotkeyModifiers
    {
        get => _showHotkeyModifiers;
        set
        {
            if (SetProperty(ref _showHotkeyModifiers, HotkeyDefinitions.NormalizeModifiers(value)))
            {
                OnPropertyChanged(nameof(HotkeyText));
            }
        }
    }

    public int ShowHotkeyVirtualKey
    {
        get => _showHotkeyVirtualKey;
        set
        {
            if (SetProperty(ref _showHotkeyVirtualKey, HotkeyDefinitions.NormalizeVirtualKey(value)))
            {
                OnPropertyChanged(nameof(HotkeyText));
            }
        }
    }

    public MouseHotkeyButton ShowHotkeyMouseButton
    {
        get => _showHotkeyMouseButton;
        private set
        {
            if (SetProperty(ref _showHotkeyMouseButton, value))
            {
                OnPropertyChanged(nameof(HotkeyText));
            }
        }
    }

    public string HotkeyText => HotkeyDefinitions.FormatHotkey(
        ShowHotkeyKind,
        ShowHotkeyModifiers,
        ShowHotkeyVirtualKey,
        ShowHotkeyMouseButton);

    public bool IsCapturingShowHotkey
    {
        get => _isCapturingShowHotkey;
        set
        {
            if (SetProperty(ref _isCapturingShowHotkey, value))
            {
                OnPropertyChanged(nameof(ShowHotkeyCaptureButtonText));
            }
        }
    }

    public string ShowHotkeyCaptureButtonText => IsCapturingShowHotkey ? "取消" : "监听";

    public bool IsDesktopHotkeysEnabled
    {
        get => _isDesktopHotkeysEnabled;
        set => SetProperty(ref _isDesktopHotkeysEnabled, value);
    }

    public int DesktopHotkeyModifiers
    {
        get => _desktopHotkeyModifiers;
        set
        {
            if (SetProperty(ref _desktopHotkeyModifiers, HotkeyDefinitions.NormalizeModifiers(value)))
            {
                OnPropertyChanged(nameof(DesktopHotkeyText));
            }
        }
    }

    public string DesktopHotkeyText => HotkeyDefinitions.FormatDesktopHotkey(DesktopHotkeyModifiers);

    public string HotkeyStatusMessage
    {
        get => _hotkeyStatusMessage;
        private set => SetProperty(ref _hotkeyStatusMessage, value);
    }

    public bool HasHotkeyStatus
    {
        get => _hasHotkeyStatus;
        private set => SetProperty(ref _hasHotkeyStatus, value);
    }

    public int ColumnsPerRow
    {
        get => _columnsPerRow;
        set => SetProperty(ref _columnsPerRow, Clamp(value, MinColumnsPerRow, MaxColumnsPerRow));
    }

    public double WindowOpacity
    {
        get => _windowOpacity;
        set
        {
            if (SetProperty(ref _windowOpacity, Clamp(value, MinWindowOpacity, MaxWindowOpacity)))
            {
                OnPropertyChanged(nameof(WindowOpacityPercent));
            }
        }
    }

    public int WindowOpacityPercent => (int)Math.Round(WindowOpacity * 100);
}
