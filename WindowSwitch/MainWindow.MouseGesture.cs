using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch;

public partial class MainWindow
{
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
        _mouseGestureSelectedAction = null;

        ShowFromBackground();
        _refreshTimer.Stop();
        UpdateMouseGestureSelection(point);
    }

    private void UpdateMouseGestureSelection(NativePoint point)
    {
        if (!_isMouseActivationGestureActive)
        {
            return;
        }

        if (!IsScreenPointInsideWindow(point))
        {
            if (_mouseGestureSelectedDesktopId is null && _mouseGestureSelectedAction is null)
            {
                return;
            }

            _mouseGestureSelectedDesktopId = null;
            _mouseGestureSelectedAction = null;
            _viewModel.ClearGestureSelection();
            return;
        }

        var action = GetVirtualDesktopActionUnderScreenPoint(point);
        var desktop = GetDesktopUnderScreenPoint(point);
        var selectedAction = action?.Action;
        var selectedDesktopId = action is null ? desktop?.Id : null;
        if (_mouseGestureSelectedAction == selectedAction &&
            _mouseGestureSelectedDesktopId == selectedDesktopId)
        {
            return;
        }

        _mouseGestureSelectedAction = selectedAction;
        _mouseGestureSelectedDesktopId = selectedDesktopId;
        _viewModel.SetGestureSelectedDesktop(_mouseGestureSelectedDesktopId);
        _viewModel.SetGestureSelectedVirtualDesktopAction(_mouseGestureSelectedAction);
    }

    private void QueueMouseGestureSelectionUpdate(NativePoint point)
    {
        lock (_mouseGestureQueueLock)
        {
            _pendingMouseGesturePoint = point;
            if (_isMouseGestureUpdateQueued)
            {
                return;
            }

            _isMouseGestureUpdateQueued = true;
        }

        Dispatcher.BeginInvoke(
            () =>
            {
                NativePoint latestPoint;
                lock (_mouseGestureQueueLock)
                {
                    latestPoint = _pendingMouseGesturePoint;
                    _isMouseGestureUpdateQueued = false;
                }

                UpdateMouseGestureSelection(latestPoint);
            },
            DispatcherPriority.Render);
    }

    private void CompleteMouseActivationGesture(NativePoint point)
    {
        if (!_isMouseActivationGestureActive)
        {
            return;
        }

        var isInsideWindow = IsScreenPointInsideWindow(point);
        UpdateMouseGestureSelection(point);
        var selectedId = _mouseGestureSelectedDesktopId;
        var selectedAction = _mouseGestureSelectedAction;
        var shouldHideAfterGesture = !_wasVisibleBeforeMouseGesture;

        _isMouseActivationGestureActive = false;
        _activeMouseActivationButton = null;
        _mouseGestureSelectedDesktopId = null;
        _mouseGestureSelectedAction = null;
        lock (_mouseGestureQueueLock)
        {
            _isMouseGestureUpdateQueued = false;
        }

        _viewModel.ClearGestureSelection();
        StartRefreshTimer();

        if (selectedId is Guid id)
        {
            _viewModel.SwitchDesktopCommand.Execute(id);
        }
        else if (selectedAction is VirtualDesktopAction action)
        {
            _viewModel.ExecuteVirtualDesktopActionCommand.Execute(action);
        }

        if (!isInsideWindow || (shouldHideAfterGesture && !_viewModel.AutoHideAfterSwitch))
        {
            HideToBackground();
        }
    }

    private bool IsScreenPointInsideWindow(NativePoint point)
    {
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var transformed = transform.Transform(new System.Windows.Point(point.X, point.Y));
        return transformed.X >= Left &&
            transformed.X <= Left + ActualWidth &&
            transformed.Y >= Top &&
            transformed.Y <= Top + ActualHeight;
    }

    private DesktopButtonViewModel? GetDesktopUnderScreenPoint(NativePoint point)
    {
        if (!IsVisible)
        {
            return null;
        }

        return DesktopOverlay.GetDesktopAtScreenPoint(new System.Windows.Point(point.X, point.Y));
    }

    private VirtualDesktopActionButtonViewModel? GetVirtualDesktopActionUnderScreenPoint(NativePoint point)
    {
        if (!IsVisible)
        {
            return null;
        }

        return DesktopOverlay.GetActionAtScreenPoint(new System.Windows.Point(point.X, point.Y));
    }
}
