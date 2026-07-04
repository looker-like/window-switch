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
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPen = System.Windows.Media.Pen;

namespace WindowSwitch.Controls;

public sealed partial class DesktopOverlayView
{
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

    private static LabelPalette GetPalette(int desktopIndex)
    {
        var index = Math.Abs(desktopIndex - 1) % ColorPalettes.Length;
        return ColorPalettes[index];
    }

    private static SolidColorBrush Solid(string hex)
    {
        var brush = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private readonly record struct DesktopHitTarget(Rect Rect, DesktopButtonViewModel? Desktop);

    private readonly record struct ActionHitTarget(Rect Rect, VirtualDesktopActionButtonViewModel? Action);

    private sealed record LabelPalette(
        WpfBrush FillBrush,
        WpfBrush BorderBrush,
        WpfBrush HoverBrush,
        WpfBrush StrongBrush,
        WpfBrush StrongBorderBrush,
        WpfBrush TextBrush)
    {
        public WpfPen Pen { get; } = new(BorderBrush, 1);

        public WpfPen StrongPen { get; } = new(StrongBorderBrush, 1);
    }
}
