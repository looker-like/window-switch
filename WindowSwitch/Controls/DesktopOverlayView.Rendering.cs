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
    private void DrawDesktop(DrawingContext dc, Rect rect, DesktopButtonViewModel desktop, DpiScale dpi)
    {
        var isGesture = desktop.IsGestureSelected;
        var isCurrent = desktop.IsCurrent;
        var isHover = desktop == _hoveredDesktop;
        var palette = EnableColoredDesktopLabels ? GetPalette(desktop.Index) : null;
        var fill = isGesture
            ? GestureBrush
            : isCurrent
                ? palette?.StrongBrush ?? CurrentBrush
                : isHover
                    ? palette?.HoverBrush ?? HoverBrush
                    : palette?.FillBrush ?? PanelBrush;
        var pen = isGesture ? GesturePen : isCurrent ? palette?.StrongPen ?? CurrentPen : palette?.Pen ?? PanelPen;
        var textBrush = isGesture || isCurrent ? WhiteBrush : palette?.TextBrush ?? TextBrush;
        var mutedBrush = isGesture || isCurrent ? WhiteBrush : palette?.StrongBrush ?? MutedTextBrush;

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
        title.MaxTextHeight = content.Height;
        title.Trimming = TextTrimming.CharacterEllipsis;
        title.TextAlignment = showIndex ? TextAlignment.Left : TextAlignment.Center;
        var titleY = rect.Y + (rect.Height - title.Height) / 2;
        dc.DrawText(title, new System.Windows.Point(content.X, titleY));

        if (showIndex)
        {
            dc.DrawText(
                indexText,
                new System.Windows.Point(
                    content.Right - indexText.WidthIncludingTrailingWhitespace,
                    rect.Y + (rect.Height - indexText.Height) / 2));
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
}
