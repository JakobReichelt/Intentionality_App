using System.Windows;
using System.Windows.Threading;
using Intentionality.Core;
using Intentionality.Services;
using Intentionality.UI.Steps;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;

namespace Intentionality;

public sealed partial class AppHost : IDisposable
{
    private readonly SessionController _session = new();
    private readonly StateStore _stateStore = new();
    private readonly MarkdownLogWriter _log = new();
    private readonly NetworkService _network = new();
    private readonly LocationService _location = new();
    private readonly LockOverlayService _lock = new();
    private readonly TrayService _tray;
    private readonly DispatcherTimer _tickTimer;
    private readonly EventWaitHandle _endSessionEvent = new(false, EventResetMode.AutoReset, EndSessionSignal.Name);
    private RegisteredWaitHandle? _endSessionWait;
    private bool _started;

    public AppHost()
    {
        _tray = new TrayService(WpfApplication.Current.Dispatcher);
        _tickTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _tickTimer.Tick += (_, _) => OnTick();
        _session.StateChanged += PersistState;
        _tray.EndSessionRequested += EndSessionEarly;
    }

    public string LogPath => _log.Path;

    public async Task StartAsync()
    {
        if (_started)
            return;
        _started = true;

        _tray.Show();
        _endSessionWait = ThreadPool.RegisterWaitForSingleObject(
            _endSessionEvent,
            (_, _) => WpfApplication.Current.Dispatcher.BeginInvoke(EndSessionEarly),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        var previous = _stateStore.Load();
        if (previous is not null
            && previous.Phase is SessionPhase.Unlocked or SessionPhase.OnBreak
                or SessionPhase.AskingCompletion or SessionPhase.AskingReflection
                or SessionPhase.AskingExtension or SessionPhase.AskingWork
                or SessionPhase.AskingDuration or SessionPhase.AskingGoal)
        {
            _log.LogInterrupted(previous);
        }

        await EnterLockedQuestionnaireAsync();
        _tickTimer.Start();
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _tickTimer.Stop();
        _session.StateChanged -= PersistState;
        _tray.EndSessionRequested -= EndSessionEarly;
        _endSessionWait?.Unregister(null);
        _endSessionEvent.Dispose();
        _lock.Dispose();
        _tray.Dispose();

        // Best-effort: if we exit while network is down, restore it.
        try
        {
            if (_network.IsDisabled)
                _network.EnableInternet();
        }
        catch
        {
            // emergency script remains available
        }
    }

    private async Task EnterLockedQuestionnaireAsync()
    {
        await DesktopGate.WaitForHomeScreenAsync();
        _lock.ShowLock(SpinnerView.Create());

        var locationTask = _location.GetLocationAsync();
        var networkTask = Task.Run(DisableInternet);
        var location = await locationTask;
        await networkTask;
        _log.LogLocationSample(location);

        _session.BeginFreshSession(location);
        PersistState();
        RenderPhase();
    }

    private void OnTick()
    {
        var before = _session.Snapshot;
        var changed = _session.Tick();
        _tray.UpdateRemaining(_session.GetRemaining());

        if (!changed)
            return;

        PersistState();
        LogPhaseTransition(before);

        if (before.Phase == SessionPhase.Unlocked && _session.Phase == SessionPhase.AskingCompletion)
        {
            EnterLockForExistingSession();
            RenderPhase();
            return;
        }

        if (before.Phase == SessionPhase.OnBreak && _session.Phase == SessionPhase.AskingWork)
        {
            // Still locked; ask for the next session. Refresh location optionally.
            _ = RefreshLocationThenRenderAsync();
            return;
        }

        RenderPhase();
    }

    private void LogPhaseTransition(SessionSnapshot before)
    {
        if (before.Phase == SessionPhase.Unlocked && _session.Phase == SessionPhase.AskingCompletion)
            _log.LogTimeExpired(before);
        else if (before.Phase == SessionPhase.OnBreak && _session.Phase == SessionPhase.AskingWork)
            _log.LogBreakEnded(before);
    }

    private async Task RefreshLocationThenRenderAsync()
    {
        var location = await _location.GetLocationAsync();
        _log.LogLocationSample(location);
        var snap = _session.Snapshot;
        snap.Location = location;
        _session.Restore(snap);
        PersistState();
        RenderPhase();
    }

    private void PersistState()
    {
        _stateStore.Save(_session.Snapshot);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
            return;

        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            var before = _session.Snapshot;
            var changed = _session.Tick();
            PersistState();
            if (changed)
                LogPhaseTransition(before);
            if (_session.Phase == SessionPhase.AskingCompletion && !_lock.IsLocked)
            {
                EnterLockForExistingSession();
                RenderPhase();
            }
            else if (_session.Phase == SessionPhase.OnBreak && !_lock.IsLocked)
            {
                EnsureLocked();
                RenderPhase();
            }
            else
            {
                _tray.UpdateRemaining(_session.GetRemaining());
            }
        });
    }
}
