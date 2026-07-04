using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfCursors = System.Windows.Input.Cursors;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPen = System.Windows.Media.Pen;
using WpfSize = System.Windows.Size;

namespace WindowSwitch.Controls;

public sealed partial class DesktopOverlayView : FrameworkElement
{
    public static readonly DependencyProperty DesktopsProperty = DependencyProperty.Register(
        nameof(Desktops),
        typeof(IEnumerable),
        typeof(DesktopOverlayView),
        new FrameworkPropertyMetadata(null, OnDesktopsChanged));

    public static readonly DependencyProperty ActionsProperty = DependencyProperty.Register(
        nameof(Actions),
        typeof(IEnumerable),
        typeof(DesktopOverlayView),
        new FrameworkPropertyMetadata(null, OnActionsChanged));

    public static readonly DependencyProperty ColumnsPerRowProperty = DependencyProperty.Register(
        nameof(ColumnsPerRow),
        typeof(int),
        typeof(DesktopOverlayView),
        new FrameworkPropertyMetadata(2, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EnableColoredDesktopLabelsProperty = DependencyProperty.Register(
        nameof(EnableColoredDesktopLabels),
        typeof(bool),
        typeof(DesktopOverlayView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    private const double DesktopHeight = 46;
    private const double DesktopGap = 8;
    private const double ActionHeight = 32;
    private const double ActionWidth = 38;
    private const double ActionGap = 6;
    private const double SectionGap = 4;
    private const double TextPaddingX = 10;
    private const double TextPaddingY = 7;
    private const double CornerRadius = 7;

    private static readonly WpfBrush PanelBrush = Solid("#F8FAFC");
    private static readonly WpfBrush PanelBorderBrush = Solid("#D8E0EA");
    private static readonly WpfBrush HoverBrush = Solid("#E6EDF6");
    private static readonly WpfBrush CurrentBrush = Solid("#2563EB");
    private static readonly WpfBrush CurrentBorderBrush = Solid("#1D4ED8");
    private static readonly WpfBrush GestureBrush = Solid("#F59E0B");
    private static readonly WpfBrush GestureBorderBrush = Solid("#D97706");
    private static readonly WpfBrush TextBrush = Solid("#1F2937");
    private static readonly WpfBrush MutedTextBrush = Solid("#64748B");
    private static readonly WpfBrush WhiteBrush = Solid("#FFFFFF");
    private static readonly WpfBrush TooltipBrush = Solid("#0F172A");
    private static readonly WpfBrush TooltipTextBrush = Solid("#F8FAFC");
    private static readonly WpfPen PanelPen = new(PanelBorderBrush, 1);
    private static readonly WpfPen CurrentPen = new(CurrentBorderBrush, 1);
    private static readonly WpfPen GesturePen = new(GestureBorderBrush, 1);
    private static readonly WpfPen TooltipPen = new(Solid("#334155"), 1);
    private static readonly LabelPalette[] ColorPalettes =
    [
        new(Solid("#DBEAFE"), Solid("#BFDBFE"), Solid("#C7D2FE"), Solid("#2563EB"), Solid("#1D4ED8"), Solid("#1E3A8A")),
        new(Solid("#D1FAE5"), Solid("#A7F3D0"), Solid("#BBF7D0"), Solid("#059669"), Solid("#047857"), Solid("#064E3B")),
        new(Solid("#FEF3C7"), Solid("#FDE68A"), Solid("#FED7AA"), Solid("#D97706"), Solid("#B45309"), Solid("#78350F")),
        new(Solid("#FFE4E6"), Solid("#FDA4AF"), Solid("#FBCFE8"), Solid("#E11D48"), Solid("#BE123C"), Solid("#881337")),
        new(Solid("#EDE9FE"), Solid("#DDD6FE"), Solid("#F5D0FE"), Solid("#7C3AED"), Solid("#6D28D9"), Solid("#4C1D95")),
        new(Solid("#CCFBF1"), Solid("#99F6E4"), Solid("#BAE6FD"), Solid("#0D9488"), Solid("#0F766E"), Solid("#134E4A")),
        new(Solid("#E0F2FE"), Solid("#BAE6FD"), Solid("#CFFAFE"), Solid("#0284C7"), Solid("#0369A1"), Solid("#0C4A6E")),
        new(Solid("#FCE7F3"), Solid("#FBCFE8"), Solid("#FECACA"), Solid("#DB2777"), Solid("#BE185D"), Solid("#831843")),
    ];

    private readonly List<DesktopButtonViewModel> _desktops = [];
    private readonly List<VirtualDesktopActionButtonViewModel> _actions = [];
    private readonly List<DesktopHitTarget> _desktopTargets = [];
    private readonly List<ActionHitTarget> _actionTargets = [];

    private INotifyCollectionChanged? _desktopCollection;
    private INotifyCollectionChanged? _actionCollection;
    private DesktopButtonViewModel? _hoveredDesktop;
    private VirtualDesktopActionButtonViewModel? _hoveredAction;
    private DesktopButtonViewModel? _tooltipDesktop;
    private System.Windows.Point _lastMousePoint;
    private double _targetWidth = -1;
    private int _targetColumns = -1;

    public DesktopOverlayView()
    {
        Focusable = false;
        Cursor = WpfCursors.Hand;
        SnapsToDevicePixels = true;
        MouseLeave += (_, _) =>
        {
            _hoveredDesktop = null;
            _hoveredAction = null;
            _tooltipDesktop = null;
            InvalidateVisual();
        };
    }

    public event EventHandler<OverlayDesktopInvokedEventArgs>? DesktopInvoked;

    public event EventHandler<OverlayActionInvokedEventArgs>? ActionInvoked;

    public IEnumerable? Desktops
    {
        get => (IEnumerable?)GetValue(DesktopsProperty);
        set => SetValue(DesktopsProperty, value);
    }

    public IEnumerable? Actions
    {
        get => (IEnumerable?)GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    public int ColumnsPerRow
    {
        get => (int)GetValue(ColumnsPerRowProperty);
        set => SetValue(ColumnsPerRowProperty, value);
    }

    public bool EnableColoredDesktopLabels
    {
        get => (bool)GetValue(EnableColoredDesktopLabelsProperty);
        set => SetValue(EnableColoredDesktopLabelsProperty, value);
    }

    public DesktopButtonViewModel? GetDesktopAtScreenPoint(System.Windows.Point screenPoint)
    {
        var point = PointFromScreen(screenPoint);
        return HitTestDesktop(point);
    }

    public VirtualDesktopActionButtonViewModel? GetActionAtScreenPoint(System.Windows.Point screenPoint)
    {
        var point = PointFromScreen(screenPoint);
        return HitTestAction(point);
    }

    public double GetRequiredContentHeight()
    {
        return CalculateRequiredContentHeight(_desktops.Count, _actions.Count, Math.Max(1, ColumnsPerRow));
    }

    public static double CalculateRequiredContentHeight(int desktopCount, int actionCount, int columns)
    {
        var normalizedColumns = Math.Max(1, columns);
        var rows = desktopCount == 0 ? 0 : (int)Math.Ceiling(desktopCount / (double)normalizedColumns);
        var desktopHeight = rows == 0 ? 0 : rows * DesktopHeight + Math.Max(0, rows - 1) * DesktopGap;
        var actionHeight = actionCount == 0 ? 0 : SectionGap + ActionHeight;
        return desktopHeight + actionHeight;
    }

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 360 : Math.Max(0, availableSize.Width);
        return new WpfSize(width, GetRequiredContentHeight());
    }

    protected override void OnMouseMove(WpfMouseEventArgs e)
    {
        base.OnMouseMove(e);
        _lastMousePoint = e.GetPosition(this);
        var desktop = HitTestDesktop(_lastMousePoint);
        var action = desktop is null ? HitTestAction(_lastMousePoint) : null;
        var tooltipDesktop = desktop is not null && IsDesktopTitleTrimmed(desktop) ? desktop : null;

        if (_hoveredDesktop == desktop && _hoveredAction == action && _tooltipDesktop == tooltipDesktop)
        {
            return;
        }

        _hoveredDesktop = desktop;
        _hoveredAction = action;
        _tooltipDesktop = tooltipDesktop;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        var point = e.GetPosition(this);
        if (HitTestDesktop(point) is { } desktop)
        {
            DesktopInvoked?.Invoke(this, new OverlayDesktopInvokedEventArgs(desktop.Id));
            e.Handled = true;
            return;
        }

        if (HitTestAction(point) is { } action)
        {
            ActionInvoked?.Invoke(this, new OverlayActionInvokedEventArgs(action.Action));
            e.Handled = true;
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        BuildHitTargets();

        var dpi = VisualTreeHelper.GetDpi(this);
        foreach (var target in _desktopTargets)
        {
            if (target.Desktop is not null)
            {
                DrawDesktop(dc, target.Rect, target.Desktop, dpi);
            }
        }

        foreach (var target in _actionTargets)
        {
            if (target.Action is not null)
            {
                DrawAction(dc, target.Rect, target.Action, dpi);
            }
        }

        if (_tooltipDesktop is not null)
        {
            DrawTooltip(dc, _tooltipDesktop.DisplayName, _lastMousePoint, dpi);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ClearHitTargets();
    }
}
