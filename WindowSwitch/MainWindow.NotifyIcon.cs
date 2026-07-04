using WindowSwitch.Services;
using Forms = System.Windows.Forms;

namespace WindowSwitch;

public partial class MainWindow
{
    private Forms.NotifyIcon CreateNotifyIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示", null, (_, _) => Dispatcher.Invoke(ShowFromBackground));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApplication));

        var notifyIcon = new Forms.NotifyIcon
        {
            Icon = AppIcons.CreateNotifyIcon(),
            Text = "WindowSwitch",
            Visible = true,
            ContextMenuStrip = menu,
        };

        notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromBackground);
        return notifyIcon;
    }

    private void ExitApplication()
    {
        _isExitRequested = true;
        Close();
    }

    private void TearDown()
    {
        CloseSettingsWindow();
        _refreshTimer.Stop();
        _settingsSaveTimer.Stop();
        _desktopHotkeySequenceTimer.Stop();
        LayoutUpdated -= MainWindow_LayoutUpdated;
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel.DesktopSwitchCompleted -= ViewModel_DesktopSwitchCompleted;
        _hwndSource?.RemoveHook(WndProc);
        if (_windowHandle != IntPtr.Zero)
        {
            UnregisterHotkeys();
        }

        ReleaseKeyboardHook();
        ReleaseMouseHook();
        SaveCurrentSettings();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
