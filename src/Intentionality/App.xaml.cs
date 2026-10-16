using System.Threading;
using System.Windows;
using System.Windows.Shell;
using Intentionality.Services;

namespace Intentionality;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Global\\IntentionalityFocusLockSingleton";
    private Mutex? _mutex;
    private AppHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            if (e.Args.Any(arg => string.Equals(arg, EndSessionSignal.Argument, StringComparison.OrdinalIgnoreCase)))
                EndSessionSignal.Pulse();
            else
            {
                System.Windows.MessageBox.Show(
                    "Intentionality läuft bereits.",
                    "Intentionality",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                NetworkService.RestoreFromFile(AppPaths.NetworkStateFile);
            }
            catch
            {
                // ignore
            }

            System.Windows.MessageBox.Show(
                "Intentionality ist auf einen Fehler gestoßen und hat die Netzwerkadapter wieder aktiviert.\n\n" + args.Exception.Message,
                "Intentionality",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        Exit += (_, _) =>
        {
            _host?.Dispose();
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch
            {
                // ignore
            }
            _mutex?.Dispose();
        };

        JumpList.SetJumpList(this, new JumpList());

        _host = new AppHost();
        await _host.StartAsync();
    }
}
