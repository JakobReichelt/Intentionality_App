using System.Threading;
using System.Windows;
using System.Windows.Shell;
using System.Windows.Threading;
using Intentionality.Services;

namespace Intentionality;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Global\\IntentionalityFocusLockSingleton";
    private const string Caption = "Intentionality";

    private Mutex? _mutex;
    private AppHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryClaimSingleInstance())
        {
            ForwardToRunningInstance(e.Args);
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        Exit += (_, _) => ReleaseResources();
        JumpList.SetJumpList(this, new JumpList());

        try
        {
            _host = new AppHost();
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            _host?.LogUnhandledError(ex);
            ShowError("Intentionality konnte nicht gestartet werden.", ex);
            Shutdown(1);
        }
    }

    private bool TryClaimSingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
            return true;

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    private static void ForwardToRunningInstance(string[] args)
    {
        var endSession = args.Any(arg => string.Equals(arg, EndSessionSignal.Argument, StringComparison.OrdinalIgnoreCase));
        if (endSession)
            EndSessionSignal.Pulse();
        else
            System.Windows.MessageBox.Show("Intentionality läuft bereits.", Caption, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        _host?.LogUnhandledError(args.Exception);
        ShowError("Intentionality ist auf einen Fehler gestoßen.", args.Exception);
        args.Handled = true;
    }

    private void ReleaseResources()
    {
        _host?.Dispose();
        if (_mutex is null)
            return;

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not owned by this thread; disposing still frees the handle.
        }
        _mutex.Dispose();
    }

    private static void ShowError(string message, Exception exception) =>
        System.Windows.MessageBox.Show(
            message + "\n\n" + exception.Message,
            Caption,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
}
