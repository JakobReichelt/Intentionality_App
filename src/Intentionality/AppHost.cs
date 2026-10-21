using System.IO;
using System.Windows.Threading;
using Intentionality.Core;
using Intentionality.Services;
using Microsoft.Win32;
using WpfApplication = System.Windows.Application;

namespace Intentionality;

/// <summary>
/// App lifecycle: startup, clock ticks, power events and shutdown.
/// Which step is shown for which phase lives in AppHost.Flow.cs.
/// </summary>
public sealed partial class AppHost : IDisposable
{
    private readonly SessionController _session = new();
    private readonly StateStore _stateStore = new();
    private readonly MarkdownLogWriter _log = new();
    private readonly LocationService _location = new();
    private readonly LockOverlayService _lock = new();
    private readonly DistractionGuard _guard = new();
    private readonly PromptVariants _prompts = new();
    private readonly TrayService _tray;
    private readonly DispatcherTimer _tickTimer;
    private readonly EventWaitHandle _endSessionEvent = new(false, EventResetMode.AutoReset, EndSessionSignal.Name);
    private RegisteredWaitHandle? _endSessionWait;
    private SessionSnapshot? _previousSnapshot;
    private DistractionHit? _pendingDistraction;
    private DateTimeOffset _ignoreDistractionsUntil = DateTimeOffset.MinValue;
    private bool _started;

    public AppHost()
    {
        _tray = new TrayService(WpfApplication.Current.Dispatcher);
        _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _tickTimer.Tick += (_, _) => AdvanceClock();
        _session.StateChanged += PersistState;
        _tray.EndSessionRequested += EndSessionEarly;
    }

    public async Task StartAsync()
    {
        if (_started)
            return;
        _started = true;

        _tray.Show();
        ListenForEndSessionSignal();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        LogPreviousSessionAsInterrupted();
        await LockAndBeginSessionAsync();
        _tickTimer.Start();
    }

    public void LogUnhandledError(Exception exception)
    {
        try
        {
            _log.LogError("Unerwarteter Fehler", exception);
        }
        catch (IOException)
        {
            // The caller still shows the error to the user.
        }
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
    }

    private void ListenForEndSessionSignal()
    {
        _endSessionWait = ThreadPool.RegisterWaitForSingleObject(
            _endSessionEvent,
            (_, _) => WpfApplication.Current.Dispatcher.BeginInvoke(EndSessionEarly),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private void LogPreviousSessionAsInterrupted()
    {
        try
        {
            if (_stateStore.Load() is { } previous)
            {
                _previousSnapshot = previous;
                _log.LogInterrupted(previous);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            _log.LogError("Gespeicherter Zustand konnte nicht gelesen werden", ex);
        }
    }

    private async Task LockAndBeginSessionAsync()
    {
        await DesktopGate.WaitForHomeScreenAsync();

        var location = await _location.GetLocationAsync();
        _session.BeginFreshSession(location, _previousSnapshot);
        _previousSnapshot = null;
        RenderPhase();
    }

    private void AdvanceClock()
    {
        var before = _session.Phase;
        if (_session.Tick())
            OnTimerElapsed(before);
        else if (_session.Phase == SessionPhase.Unlocked)
            PollDistraction();

        UpdateTray();
    }

    private void PollDistraction()
    {
        if (DateTimeOffset.UtcNow < _ignoreDistractionsUntil)
            return;

        try
        {
            var hit = _guard.DetectForeground();
            if (hit is null)
                return;

            if (_session.ReportDistraction(hit.RuleId))
            {
                _pendingDistraction = hit;
                RenderPhase();
            }
        }
        catch (Exception ex)
        {
            _log.LogError("Ablenkungsprüfung fehlgeschlagen", ex);
        }
    }

    private void OnTimerElapsed(SessionPhase before)
    {
        if (before is SessionPhase.Unlocked or SessionPhase.CheckingDistraction)
            _log.LogTimeExpired();

        if (_session.Phase == SessionPhase.AskingWork)
            _ = RefreshLocationThenRenderAsync();
        else
            RenderPhase();
    }

    private async Task RefreshLocationThenRenderAsync()
    {
        try
        {
            _session.UpdateLocation(await _location.GetLocationAsync());
            RenderPhase();
        }
        catch (Exception ex)
        {
            _log.LogError("Neue Sitzung konnte nicht vorbereitet werden", ex);
            RenderPhase();
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
            return;

        WpfApplication.Current.Dispatcher.BeginInvoke(() =>
        {
            AdvanceClock();
            var mustBeLocked = _session.Phase is SessionPhase.AskingCompletion
                or SessionPhase.AskingResumeNote
                or SessionPhase.CheckingDistraction
                or SessionPhase.OnBreak;
            if (mustBeLocked && !_lock.IsLocked)
                RenderPhase();
        });
    }

    private void PersistState()
    {
        try
        {
            _stateStore.Save(_session.Snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogError("Zustand konnte nicht gespeichert werden", ex);
        }
    }
}
