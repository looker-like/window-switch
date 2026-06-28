using System.Windows;
using WindowSwitch.ViewModels;

namespace WindowSwitch;

public partial class SettingsWindow : Window
{
    public SettingsWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public event EventHandler? RecordShowHotkeyRequested;

    private void RecordShowHotkeyButton_Click(object sender, RoutedEventArgs e)
    {
        RecordShowHotkeyRequested?.Invoke(this, EventArgs.Empty);
    }
}
