using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;
using WindowSwitch.Services;
using WindowsDesktop;
using Forms = System.Windows.Forms;

namespace WindowSwitch;

public partial class MainWindow
{
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

    private void CloseSettingsWindow()
    {
        if (_settingsWindow is null)
        {
            return;
        }

        var settingsWindow = _settingsWindow;
        _settingsWindow = null;
        settingsWindow.Close();
    }
}
