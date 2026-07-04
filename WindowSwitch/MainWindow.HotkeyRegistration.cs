using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using WindowSwitch.Services;

namespace WindowSwitch;

public partial class MainWindow
{
    private void ApplyHotkeyRegistration()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        ResetDesktopHotkeySequence();
        UnregisterHotkeys();

        var failedHotkeys = new List<string>();
        var registeredHotkeys = new List<string>();

        if (_viewModel.IsHotkeyEnabled)
        {
            if (_viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton)
            {
                if (EnsureMouseHook())
                {
                    registeredHotkeys.Add($"{_viewModel.HotkeyText}（按住滑动选择）");
                }
                else
                {
                    failedHotkeys.Add(_viewModel.HotkeyText);
                }
            }
            else
            {
                if (TryRegisterHotkey(
                    ShowHotkeyId,
                    HotkeyDefinitions.ToRegisterHotkeyModifiers(_viewModel.ShowHotkeyModifiers),
                    (uint)_viewModel.ShowHotkeyVirtualKey))
                {
                    registeredHotkeys.Add(_viewModel.HotkeyText);
                }
                else
                {
                    failedHotkeys.Add(_viewModel.HotkeyText);
                }
            }
        }

        if (_viewModel.IsDesktopHotkeysEnabled)
        {
            for (var index = 1; index <= 9; index++)
            {
                RegisterDesktopDigitHotkey(index, failedHotkeys);
            }

            RegisterDesktopDigitHotkey(0, failedHotkeys);
            registeredHotkeys.Add($"{_viewModel.DesktopHotkeyText}（0=第10桌面，支持两位）");
        }

        if (failedHotkeys.Count > 0)
        {
            _viewModel.SetHotkeyStatus($"快捷键冲突：{string.Join(", ", failedHotkeys)} 已被其他应用占用或被系统保留。");
        }
        else if (registeredHotkeys.Count > 0)
        {
            _viewModel.SetHotkeyStatus($"快捷键可用：{string.Join("；", registeredHotkeys)}");
        }
        else
        {
            _viewModel.SetHotkeyStatus(string.Empty);
        }

        UpdateInputHookLifetime();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmNchitTest)
        {
            handled = TryHandleNchitTest(lParam, out var hitTest);
            return hitTest;
        }

        if (msg == WmNcLeftButtonDoubleClick && TryHandleResizeBorderDoubleClick(wParam))
        {
            handled = true;
            return IntPtr.Zero;
        }

        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        var hotkeyId = wParam.ToInt32();
        if (hotkeyId == ShowHotkeyId)
        {
            ShowFromBackground();
            handled = true;
        }
        else if (hotkeyId is >= DesktopHotkeyFirstId and <= DesktopHotkeyLastId)
        {
            HandleDesktopHotkeyDigit(hotkeyId - DesktopHotkeyFirstId);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private bool TryHandleResizeBorderDoubleClick(IntPtr hitTest)
    {
        return hitTest.ToInt32() switch
        {
            HtTop or HtTopLeft or HtTopRight => HandleResizeBorderDoubleClick(keepBottom: false),
            HtBottom or HtBottomLeft or HtBottomRight => HandleResizeBorderDoubleClick(keepBottom: true),
            _ => false,
        };
    }

    private bool HandleResizeBorderDoubleClick(bool keepBottom)
    {
        FitHeightToContent(keepBottom);
        ScheduleSettingsSave();
        return true;
    }

    private bool TryHandleNchitTest(IntPtr lParam, out IntPtr hitTest)
    {
        hitTest = IntPtr.Zero;
        if (_windowHandle == IntPtr.Zero)
        {
            return false;
        }

        var screenPoint = GetPointFromLParam(lParam);
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice
            ?? Matrix.Identity;
        var point = transform.Transform(screenPoint);
        var left = Left;
        var top = Top;
        var right = left + ActualWidth;
        var bottom = top + ActualHeight;

        if (point.X < left || point.X > right || point.Y < top || point.Y > bottom)
        {
            return false;
        }

        var onLeft = point.X <= left + ResizeBorderThickness;
        var onRight = point.X >= right - ResizeBorderThickness;
        var onTop = point.Y <= top + ResizeBorderThickness;
        var onBottom = point.Y >= bottom - ResizeBorderThickness;

        hitTest = (onLeft, onRight, onTop, onBottom) switch
        {
            (true, false, true, false) => new IntPtr(HtTopLeft),
            (false, true, true, false) => new IntPtr(HtTopRight),
            (true, false, false, true) => new IntPtr(HtBottomLeft),
            (false, true, false, true) => new IntPtr(HtBottomRight),
            (true, false, false, false) => new IntPtr(HtLeft),
            (false, true, false, false) => new IntPtr(HtRight),
            (false, false, true, false) => new IntPtr(HtTop),
            (false, false, false, true) => new IntPtr(HtBottom),
            _ => new IntPtr(HtClient),
        };

        return true;
    }

    private void RegisterDesktopDigitHotkey(int digit, List<string> failedHotkeys)
    {
        if (TryRegisterHotkey(
            DesktopHotkeyFirstId + digit,
            HotkeyDefinitions.ToRegisterHotkeyModifiers(_viewModel.DesktopHotkeyModifiers),
            DigitToVirtualKey(digit)))
        {
            return;
        }

        failedHotkeys.Add($"{HotkeyDefinitions.FormatModifiers(_viewModel.DesktopHotkeyModifiers)} + {digit}");
    }

    private bool TryRegisterHotkey(int id, uint modifiers, uint key)
    {
        if (!RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            return false;
        }

        _registeredHotkeyIds.Add(id);
        return true;
    }

    private void UnregisterHotkeys()
    {
        foreach (var id in _registeredHotkeyIds)
        {
            UnregisterHotKey(_windowHandle, id);
        }

        _registeredHotkeyIds.Clear();
    }
}
