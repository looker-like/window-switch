using System.Runtime.InteropServices;
using System.Windows.Threading;
using WindowSwitch.Services;

namespace WindowSwitch;

public partial class MainWindow
{
    private bool EnsureKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            return true;
        }

        _keyboardHook = SetKeyboardHook(
            WhKeyboardLl,
            _keyboardHookCallback,
            GetModuleHandle(null),
            0);
        return _keyboardHook != IntPtr.Zero;
    }

    private bool EnsureMouseHook()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            return true;
        }

        _mouseHook = SetMouseHook(
            WhMouseLl,
            _mouseHookCallback,
            GetModuleHandle(null),
            0);
        return _mouseHook != IntPtr.Zero;
    }

    private void UpdateInputHookLifetime()
    {
        if (_viewModel.IsCapturingShowHotkey)
        {
            EnsureKeyboardHook();
            EnsureMouseHook();
            return;
        }

        ReleaseKeyboardHook();
        if (_viewModel.IsHotkeyEnabled && _viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton)
        {
            EnsureMouseHook();
            return;
        }

        ReleaseMouseHook();
    }

    private void ReleaseKeyboardHook()
    {
        if (_keyboardHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
    }

    private void ReleaseMouseHook()
    {
        if (_mouseHook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HcAction &&
            _viewModel.IsCapturingShowHotkey &&
            wParam.ToInt32() is WmKeyDown or WmSysKeyDown)
        {
            var data = Marshal.PtrToStructure<KeyboardHookData>(lParam);
            Dispatcher.BeginInvoke(() => CaptureKeyboardHotkey(data.VirtualKeyCode), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode != HcAction)
        {
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<MouseHookData>(lParam);

        if (_viewModel.IsCapturingShowHotkey &&
            TryGetMouseButton(message, data.MouseData, out var capturedButton, out var isCaptureDown, out _) &&
            isCaptureDown)
        {
            Dispatcher.BeginInvoke(() => CaptureMouseHotkey(capturedButton), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        if (_isMouseActivationGestureActive)
        {
            if (message == WmMouseMove)
            {
                QueueMouseGestureSelectionUpdate(data.Point);
            }
            else if (TryGetMouseButton(message, data.MouseData, out var button, out _, out var isUp) &&
                isUp &&
                button == _activeMouseActivationButton)
            {
                Dispatcher.BeginInvoke(() => CompleteMouseActivationGesture(data.Point), DispatcherPriority.Input);
                return new IntPtr(1);
            }

            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        if (_viewModel.IsHotkeyEnabled &&
            _viewModel.ShowHotkeyKind == ShowHotkeyKind.MouseButton &&
            TryGetMouseButton(message, data.MouseData, out var activationButton, out var isDown, out _) &&
            isDown &&
            activationButton == _viewModel.ShowHotkeyMouseButton)
        {
            Dispatcher.BeginInvoke(() => BeginMouseActivationGesture(activationButton, data.Point), DispatcherPriority.Input);
            return new IntPtr(1);
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private static bool TryGetMouseButton(
        int message,
        int mouseData,
        out MouseHotkeyButton button,
        out bool isDown,
        out bool isUp)
    {
        isDown = message is WmRightButtonDown or WmMiddleButtonDown or WmXButtonDown;
        isUp = message is WmRightButtonUp or WmMiddleButtonUp or WmXButtonUp;
        button = MouseHotkeyButton.Middle;

        switch (message)
        {
            case WmRightButtonDown:
            case WmRightButtonUp:
                button = MouseHotkeyButton.Right;
                return true;
            case WmMiddleButtonDown:
            case WmMiddleButtonUp:
                button = MouseHotkeyButton.Middle;
                return true;
            case WmXButtonDown:
            case WmXButtonUp:
                var xButton = (mouseData >> 16) & 0xFFFF;
                button = xButton == XButton2 ? MouseHotkeyButton.XButton2 : MouseHotkeyButton.XButton1;
                return xButton is XButton1 or XButton2;
            default:
                return false;
        }
    }

    private void HandleDesktopHotkeyDigit(int digit)
    {
        _viewModel.Refresh();
        var availableDesktopIndexes = _viewModel.Desktops.Select(desktop => desktop.Index).ToArray();
        var desktopIndex = _desktopHotkeySequence.HandleDigit(digit, availableDesktopIndexes);

        if (_desktopHotkeySequence.IsListening)
        {
            if (!_desktopHotkeySequenceTimer.IsEnabled)
            {
                _desktopHotkeySequenceTimer.Start();
            }
        }
        else
        {
            _desktopHotkeySequenceTimer.Stop();
        }

        if (desktopIndex is int targetIndex)
        {
            SwitchDesktopFromHotkey(targetIndex);
        }
    }

    private void ResetDesktopHotkeySequence()
    {
        _desktopHotkeySequence.Reset();
        _desktopHotkeySequenceTimer.Stop();
    }

    private static uint DigitToVirtualKey(int digit)
    {
        return Vk0 + (uint)digit;
    }

    private static bool AreRequiredModifiersPressed(int modifiers)
    {
        var normalized = (HotkeyModifiers)HotkeyDefinitions.NormalizeModifiers(modifiers);

        if (normalized.HasFlag(HotkeyModifiers.Control) && !IsKeyPressed(VkControl))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Alt) && !IsKeyPressed(VkMenu))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Shift) && !IsKeyPressed(VkShift))
        {
            return false;
        }

        if (normalized.HasFlag(HotkeyModifiers.Windows) &&
            !IsKeyPressed(VkLeftWindows) &&
            !IsKeyPressed(VkRightWindows))
        {
            return false;
        }

        return true;
    }

    private static int GetPressedHotkeyModifiers()
    {
        var modifiers = HotkeyModifiers.None;
        if (IsKeyPressed(VkControl))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (IsKeyPressed(VkMenu))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (IsKeyPressed(VkShift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (IsKeyPressed(VkLeftWindows) || IsKeyPressed(VkRightWindows))
        {
            modifiers |= HotkeyModifiers.Windows;
        }

        return (int)modifiers;
    }

    private static bool IsKeyPressed(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private void SwitchDesktopFromHotkey(int desktopIndex)
    {
        var desktop = _viewModel.Desktops.FirstOrDefault(item => item.Index == desktopIndex);
        if (desktop is not null)
        {
            _viewModel.SwitchDesktopCommand.Execute(desktop.Id);
        }
    }
}
