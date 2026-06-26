using System.Collections.ObjectModel;
using System.Windows.Input;
using WindowSwitch.Commands;
using WindowSwitch.Services;

namespace WindowSwitch.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
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
    private bool _isHotkeyEnabled = true;
    private bool _isDesktopHotkeysEnabled = true;
    private string _hotkeyStatusMessage = string.Empty;
    private bool _hasHotkeyStatus;
    private int _showHotkeyModifiers = HotkeyDefinitions.DefaultHotkeyModifiers;
    private int _showHotkeyVirtualKey = HotkeyDefinitions.DefaultShowHotkeyVirtualKey;
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
        RefreshCommand = new RelayCommand(_ => Refresh());

        _desktopService.DesktopsChanged += _desktopsChangedHandler;
        Refresh();
    }

    public event EventHandler? DesktopSwitchCompleted;

    public ObservableCollection<DesktopButtonViewModel> Desktops { get; } = [];

    public IReadOnlyList<int> ColumnOptions { get; } = [1, 2, 3, 4];

    public IReadOnlyList<HotkeyModifierOption> HotkeyModifierOptions => HotkeyDefinitions.ModifierOptions;

    public IReadOnlyList<HotkeyKeyOption> HotkeyKeyOptions => HotkeyDefinitions.KeyOptions;

    public ICommand SwitchDesktopCommand { get; }

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

    public bool IsHotkeyEnabled
    {
        get => _isHotkeyEnabled;
        set => SetProperty(ref _isHotkeyEnabled, value);
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

    public string HotkeyText => HotkeyDefinitions.FormatHotkey(ShowHotkeyModifiers, ShowHotkeyVirtualKey);

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

    private void ApplySettings(WindowSettings settings)
    {
        _isFloatingTopmost = settings.IsFloatingTopmost;
        _startHidden = settings.StartHidden;
        _autoHideAfterSwitch = settings.AutoHideAfterSwitch;
        _isHotkeyEnabled = settings.IsHotkeyEnabled;
        _isDesktopHotkeysEnabled = settings.IsDesktopHotkeysEnabled;
        _showHotkeyModifiers = HotkeyDefinitions.NormalizeModifiers(settings.ShowHotkeyModifiers);
        _showHotkeyVirtualKey = HotkeyDefinitions.NormalizeVirtualKey(settings.ShowHotkeyVirtualKey);
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
