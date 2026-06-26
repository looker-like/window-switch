using System.Windows;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using WindowSwitch.Services;
using WindowSwitch.ViewModels;

namespace WindowSwitch;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\WindowSwitch.SingleInstance";
    private const string ActivationPipeName = "WindowSwitch.Activate";

    private MainWindowViewModel? _mainWindowViewModel;
    private Mutex? _singleInstanceMutex;
    private CancellationTokenSource? _activationListenerCancellation;
    private bool _ownsSingleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out _ownsSingleInstance);
        if (!_ownsSingleInstance)
        {
            NotifyExistingInstance();
            Shutdown();
            return;
        }

        var settingsStore = new WindowSettingsStore();
        var settings = settingsStore.Load() ?? new WindowSettings();
        var desktopService = new WindowsVirtualDesktopService();
        _mainWindowViewModel = new MainWindowViewModel(desktopService, settings, DispatchToUi);

        var window = new MainWindow(_mainWindowViewModel, settingsStore, settings);
        MainWindow = window;
        StartActivationListener(window);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationListenerCancellation?.Cancel();
        _mainWindowViewModel?.Dispose();
        if (_ownsSingleInstance)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private void DispatchToUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.Invoke(action);
    }

    private static void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", ActivationPipeName, PipeDirection.Out);
            client.Connect(500);
        }
        catch
        {
            // If activation fails, still prevent a second window from opening.
        }
    }

    private void StartActivationListener(MainWindow window)
    {
        _activationListenerCancellation = new CancellationTokenSource();
        var token = _activationListenerCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        ActivationPipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token).ConfigureAwait(false);
                    DispatchToUi(window.ShowFromBackground);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException)
                {
                }
            }
        }, token);
    }
}

