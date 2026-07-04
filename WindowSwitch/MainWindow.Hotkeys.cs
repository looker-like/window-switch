using System.Windows.Threading;
using WindowSwitch.Services;

namespace WindowSwitch;

public partial class MainWindow
{
    private void ToggleShowHotkeyCapture()
    {
        if (_viewModel.IsCapturingShowHotkey)
        {
            EndShowHotkeyCapture("已取消监听。");
            return;
        }

        BeginShowHotkeyCapture();
    }

    private void BeginShowHotkeyCapture()
    {
        _viewModel.IsCapturingShowHotkey = true;
        _viewModel.SetHotkeyStatus("正在监听唤起快捷键。");

        var keyboardReady = EnsureKeyboardHook();
        var mouseReady = EnsureMouseHook();
        if (!keyboardReady || !mouseReady)
        {
            EndShowHotkeyCapture("监听启动失败，无法录制新的唤起快捷键。");
        }
    }

    private void EndShowHotkeyCapture(string? statusMessage = null)
    {
        _viewModel.IsCapturingShowHotkey = false;
        if (statusMessage is not null)
        {
            _viewModel.SetHotkeyStatus(statusMessage);
        }

        UpdateInputHookLifetime();
    }

    private void CompleteShowHotkeyCapture(CapturedShowHotkey hotkey)
    {
        _viewModel.IsCapturingShowHotkey = false;
        _viewModel.ApplyCapturedShowHotkey(hotkey);
        ScheduleSettingsSave();
        UpdateInputHookLifetime();
    }

    private void CaptureKeyboardHotkey(int virtualKey)
    {
        if (!_viewModel.IsCapturingShowHotkey)
        {
            return;
        }

        if (virtualKey == VkEscape)
        {
            EndShowHotkeyCapture("已取消监听。");
            return;
        }

        if (!HotkeyDefinitions.IsSupportedMainVirtualKey(virtualKey))
        {
            return;
        }

        var modifiers = GetPressedHotkeyModifiers();
        if (modifiers == 0)
        {
            _viewModel.SetHotkeyStatus("键盘唤起快捷键需要同时按住 Ctrl、Alt、Shift 或 Win。");
            return;
        }

        CompleteShowHotkeyCapture(new CapturedShowHotkey(
            ShowHotkeyKind.Keyboard,
            modifiers,
            virtualKey,
            _viewModel.ShowHotkeyMouseButton));
    }

    private void CaptureMouseHotkey(MouseHotkeyButton button)
    {
        if (!_viewModel.IsCapturingShowHotkey)
        {
            return;
        }

        CompleteShowHotkeyCapture(new CapturedShowHotkey(
            ShowHotkeyKind.MouseButton,
            _viewModel.ShowHotkeyModifiers,
            _viewModel.ShowHotkeyVirtualKey,
            button));
    }

}
