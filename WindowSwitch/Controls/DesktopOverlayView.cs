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

public sealed class DesktopOverlayView : FrameworkElement
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

    protected override WpfSize MeasureOverride(WpfSize availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 360 : Math.Max(0, availableSize.Width);
        var columns = Math.Max(1, ColumnsPerRow);
        var rows = _desktops.Count == 0 ? 0 : (int)Math.Ceiling(_desktops.Count / (double)columns);
        var desktopHeight = rows == 0 ? 0 : rows * DesktopHeight + Math.Max(0, rows - 1) * DesktopGap;
        var actionHeight = _actions.Count == 0 ? 0 : SectionGap + ActionHeight;
        return new WpfSize(width, desktopHeight + actionHeight);
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

    private static void OnDesktopsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DesktopOverlayView)d;
        view.DetachDesktopSource();
        view.AttachDesktopSource(e.NewValue as IEnumerable);
    }

    private static void OnActionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (DesktopOverlayView)d;
        view.DetachActionSource();
        view.AttachActionSource(e.NewValue as IEnumerable);
    }

    private void AttachDesktopSource(IEnumerable? source)
    {
        if (source is INotifyCollectionChanged collection)
        {
            _desktopCollection = collection;
            collection.CollectionChanged += DesktopCollectionChanged;
        }

        RebuildDesktops(source);
    }

    private void DetachDesktopSource()
    {
        if (_desktopCollection is not null)
        {
            _desktopCollection.CollectionChanged -= DesktopCollectionChanged;
            _desktopCollection = null;
        }

        foreach (var desktop in _desktops)
        {
            desktop.PropertyChanged -= ItemPropertyChanged;
        }

        _desktops.Clear();
    }

    private void AttachActionSource(IEnumerable? source)
    {
        if (source is INotifyCollectionChanged collection)
        {
            _actionCollection = collection;
            collection.CollectionChanged += ActionCollectionChanged;
        }

        RebuildActions(source);
    }

    private void DetachActionSource()
    {
        if (_actionCollection is not null)
        {
            _actionCollection.CollectionChanged -= ActionCollectionChanged;
            _actionCollection = null;
        }

        foreach (var action in _actions)
        {
            action.PropertyChanged -= ItemPropertyChanged;
        }

        _actions.Clear();
    }

    private void DesktopCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildDesktops(Desktops);
    }

    private void ActionCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildActions(Actions);
    }

    private void RebuildDesktops(IEnumerable? source)
    {
        foreach (var desktop in _desktops)
        {
            desktop.PropertyChanged -= ItemPropertyChanged;
        }

        _desktops.Clear();
        if (source is not null)
        {
            foreach (var item in source.OfType<DesktopButtonViewModel>())
            {
                _desktops.Add(item);
                item.PropertyChanged += ItemPropertyChanged;
            }
        }

        ClearHitTargets();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void RebuildActions(IEnumerable? source)
    {
        foreach (var action in _actions)
        {
            action.PropertyChanged -= ItemPropertyChanged;
        }

        _actions.Clear();
        if (source is not null)
        {
            foreach (var item in source.OfType<VirtualDesktopActionButtonViewModel>())
            {
                _actions.Add(item);
                item.PropertyChanged += ItemPropertyChanged;
            }
        }

        ClearHitTargets();
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void ItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void DrawDesktop(DrawingContext dc, Rect rect, DesktopButtonViewModel desktop, DpiScale dpi)
    {
        var isGesture = desktop.IsGestureSelected;
        var isCurrent = desktop.IsCurrent;
        var isHover = desktop == _hoveredDesktop;
        var fill = isGesture ? GestureBrush : isCurrent ? CurrentBrush : isHover ? HoverBrush : PanelBrush;
        var pen = isGesture ? GesturePen : isCurrent ? CurrentPen : PanelPen;
        var textBrush = isGesture || isCurrent ? WhiteBrush : TextBrush;
        var mutedBrush = isGesture || isCurrent ? WhiteBrush : MutedTextBrush;

        dc.DrawRoundedRectangle(fill, pen, rect, CornerRadius, CornerRadius);

        if (isGesture)
        {
            var rail = new Rect(rect.X + 1, rect.Y + 1, 4, Math.Max(0, rect.Height - 2));
            dc.DrawRoundedRectangle(Solid("#FDE68A"), null, rail, 3, 3);
        }

        var content = new Rect(
            rect.X + TextPaddingX,
            rect.Y + TextPaddingY,
            Math.Max(0, rect.Width - TextPaddingX * 2),
            Math.Max(0, rect.Height - TextPaddingY * 2));
        var indexText = CreateText(desktop.Index.ToString(CultureInfo.InvariantCulture), 15, FontWeights.SemiBold, mutedBrush, dpi);
        var titleFullWidth = MeasureText(desktop.DisplayName, 13, FontWeights.Normal, dpi);
        var showIndex = titleFullWidth + 8 + indexText.WidthIncludingTrailingWhitespace <= content.Width;
        var titleWidth = showIndex
            ? Math.Max(0, content.Width - 8 - indexText.WidthIncludingTrailingWhitespace)
            : content.Width;

        var title = CreateText(desktop.DisplayName, 13, FontWeights.Normal, textBrush, dpi);
        title.MaxTextWidth = titleWidth;
        title.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(title, new System.Windows.Point(content.X, content.Y + 5));

        if (showIndex)
        {
            dc.DrawText(
                indexText,
                new System.Windows.Point(content.Right - indexText.WidthIncludingTrailingWhitespace, content.Y + 3));
        }
    }

    private void DrawAction(DrawingContext dc, Rect rect, VirtualDesktopActionButtonViewModel action, DpiScale dpi)
    {
        var isGesture = action.IsGestureSelected;
        var isHover = action == _hoveredAction;
        var fill = isGesture ? GestureBrush : isHover ? HoverBrush : PanelBrush;
        var pen = isGesture ? GesturePen : PanelPen;
        var brush = isGesture ? WhiteBrush : Solid("#334155");

        dc.DrawRoundedRectangle(fill, pen, rect, 6, 6);
        var text = CreateIconText(action.IconGlyph, 14, brush, dpi);
        dc.DrawText(
            text,
            new System.Windows.Point(
                rect.X + (rect.Width - text.WidthIncludingTrailingWhitespace) / 2,
                rect.Y + (rect.Height - text.Height) / 2));
    }

    private void DrawTooltip(DrawingContext dc, string text, System.Windows.Point mousePoint, DpiScale dpi)
    {
        var formatted = CreateText(text, 12, FontWeights.Normal, TooltipTextBrush, dpi);
        formatted.MaxTextWidth = 320;
        var width = Math.Min(320, formatted.WidthIncludingTrailingWhitespace) + 16;
        var height = formatted.Height + 10;
        var x = Math.Min(Math.Max(0, mousePoint.X + 10), Math.Max(0, ActualWidth - width));
        var y = Math.Max(0, mousePoint.Y - height - 10);
        var rect = new Rect(x, y, width, height);

        dc.DrawRoundedRectangle(TooltipBrush, TooltipPen, rect, 5, 5);
        dc.DrawText(formatted, new System.Windows.Point(rect.X + 8, rect.Y + 5));
    }

    private DesktopButtonViewModel? HitTestDesktop(System.Windows.Point point)
    {
        BuildHitTargets();
        return _desktopTargets.FirstOrDefault(target => target.Rect.Contains(point)).Desktop;
    }

    private VirtualDesktopActionButtonViewModel? HitTestAction(System.Windows.Point point)
    {
        BuildHitTargets();
        return _actionTargets.FirstOrDefault(target => target.Rect.Contains(point)).Action;
    }

    private void BuildHitTargets()
    {
        var width = Math.Max(0, ActualWidth);
        var columns = Math.Max(1, ColumnsPerRow);
        if (_targetWidth.Equals(width) &&
            _targetColumns == columns &&
            _desktopTargets.Count == _desktops.Count &&
            _actionTargets.Count == _actions.Count)
        {
            return;
        }

        _desktopTargets.Clear();
        _actionTargets.Clear();
        _targetWidth = width;
        _targetColumns = columns;

        var cellWidth = Math.Max(0, (width - (columns - 1) * DesktopGap) / columns);
        var y = 0.0;
        for (var i = 0; i < _desktops.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var rect = new Rect(
                column * (cellWidth + DesktopGap),
                row * (DesktopHeight + DesktopGap),
                cellWidth,
                DesktopHeight);
            _desktopTargets.Add(new DesktopHitTarget(rect, _desktops[i]));
            y = rect.Bottom;
        }

        if (_actions.Count == 0)
        {
            return;
        }

        y += SectionGap;
        var totalWidth = _actions.Count * ActionWidth + Math.Max(0, _actions.Count - 1) * ActionGap;
        var x = Math.Max(0, (width - totalWidth) / 2);
        foreach (var action in _actions)
        {
            var rect = new Rect(x, y, ActionWidth, ActionHeight);
            _actionTargets.Add(new ActionHitTarget(rect, action));
            x += ActionWidth + ActionGap;
        }
    }

    private void ClearHitTargets()
    {
        _targetWidth = -1;
        _targetColumns = -1;
        _desktopTargets.Clear();
        _actionTargets.Clear();
    }

    private bool IsDesktopTitleTrimmed(DesktopButtonViewModel desktop)
    {
        var target = _desktopTargets.FirstOrDefault(item => ReferenceEquals(item.Desktop, desktop));
        if (target.Desktop is null)
        {
            return false;
        }

        var contentWidth = Math.Max(0, target.Rect.Width - TextPaddingX * 2);
        return MeasureText(desktop.DisplayName, 13, FontWeights.Normal, VisualTreeHelper.GetDpi(this)) > contentWidth;
    }

    private static FormattedText CreateText(string text, double size, FontWeight weight, WpfBrush brush, DpiScale dpi)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(new WpfFontFamily("Segoe UI Variable Text, Segoe UI"), FontStyles.Normal, weight, FontStretches.Normal),
            size,
            brush,
            dpi.PixelsPerDip);
    }

    private static FormattedText CreateIconText(string text, double size, WpfBrush brush, DpiScale dpi)
    {
        return new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            WpfFlowDirection.LeftToRight,
            new Typeface(new WpfFontFamily("Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            size,
            brush,
            dpi.PixelsPerDip);
    }

    private static double MeasureText(string text, double size, FontWeight weight, DpiScale dpi)
    {
        return CreateText(text, size, weight, TextBrush, dpi).WidthIncludingTrailingWhitespace;
    }

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private readonly record struct DesktopHitTarget(Rect Rect, DesktopButtonViewModel? Desktop);

    private readonly record struct ActionHitTarget(Rect Rect, VirtualDesktopActionButtonViewModel? Action);
}
