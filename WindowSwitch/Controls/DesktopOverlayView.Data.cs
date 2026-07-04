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
}
