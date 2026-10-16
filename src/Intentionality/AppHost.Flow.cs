using Intentionality.Core;
using Intentionality.Services;
using Intentionality.UI.Steps;

namespace Intentionality;

public partial class AppHost
{
    void EnterLockForExistingSession()
    {
        _lock.ShowLock(SpinnerView.Create());
        _ = Task.Run(DisableInternet);
    }

    void DisableInternet()
    {
        try
        {
            _network.DisableInternet();
        }
        catch (Exception ex)
        {
            _log.AppendRaw($"### {MarkdownLogWriter.FormatLocal(DateTimeOffset.Now)} — Netzwerk konnte nicht deaktiviert werden\n- Fehler: {ex.Message}\n");
        }
    }

    void Unlock()
    {
        try
        {
            _network.EnableInternet();
        }
        catch (Exception ex)
        {
            _log.AppendRaw($"### {MarkdownLogWriter.FormatLocal(DateTimeOffset.Now)} — Netzwerk konnte nicht wiederhergestellt werden\n- Fehler: {ex.Message}\n");
        }

        _lock.HideLock();
        _tray.SetSessionActive(true);
        _tray.UpdateRemaining(_session.GetRemaining());
    }

    void EndSessionEarly()
    {
        if (_session.Phase != SessionPhase.Unlocked)
            return;

        _log.LogEndedEarly(_session.Snapshot);
        _session.EndSessionForFeedback();
        PersistState();
        EnterLockForExistingSession();
        RenderPhase();
    }

    void RenderPhase()
    {
        var snap = _session.Snapshot;
        _tray.SetSessionActive(snap.Phase == SessionPhase.Unlocked);

        switch (snap.Phase)
        {
            case SessionPhase.AskingWork:
                ShowWork();
                break;
            case SessionPhase.AskingDuration:
                ShowDuration();
                break;
            case SessionPhase.AskingGoal:
                ShowGoal();
                break;
            case SessionPhase.Unlocked:
                if (_lock.IsLocked)
                    Unlock();
                _tray.UpdateRemaining(_session.GetRemaining());
                break;
            case SessionPhase.AskingCompletion:
                ShowCompletion(snap);
                break;
            case SessionPhase.AskingReflection:
                ShowReflection();
                break;
            case SessionPhase.AskingExtension:
                ShowExtension();
                break;
            case SessionPhase.OnBreak:
                EnsureLocked();
                _lock.UpdatePrimaryContent(BreakStep.Create(() => _session.GetRemaining()));
                _tray.UpdateRemaining(_session.GetRemaining());
                break;
        }
    }

    void ShowWork()
    {
        EnsureLocked();
        _lock.UpdatePrimaryContent(TextStep.Create("Woran arbeitest du gerade?", text =>
        {
            _session.SubmitWorkingOn(text);
            PersistState();
            RenderPhase();
        }));
    }

    void ShowDuration()
    {
        EnsureLocked();
        _lock.UpdatePrimaryContent(DurationStep.Create(duration =>
        {
            _session.SubmitDuration(duration);
            PersistState();
            RenderPhase();
        }));
    }

    void ShowGoal()
    {
        EnsureLocked();
        _lock.UpdatePrimaryContent(TextStep.Create("Was ist das Ziel dieser Sitzung?", goal =>
        {
            _session.SubmitGoal(goal);
            _log.LogSessionStarted(_session.Snapshot);
            PersistState();
            _lock.FadeOutThen(Unlock);
        }));
    }

    void ShowCompletion(SessionSnapshot snap)
    {
        EnsureLocked();
        _lock.UpdatePrimaryContent(OptionStep.Create(
            "Hast du das Ziel erreicht?",
            snap.Goal,
            ["Ja", "Nein"],
            start: 1,
            index =>
            {
                var completed = index == 0;
                _log.LogGoalCheck(_session.Snapshot, completed);
                _session.SubmitGoalCompleted(completed);
                PersistState();
                RenderPhase();
            }));
    }

    void ShowReflection()
    {
        EnsureLocked();
        var levels = Enum.GetValues<AmbitionLevel>();
        _lock.UpdatePrimaryContent(OptionStep.Create(
            "War das Ziel zu groß oder zu klein?",
            detail: null,
            levels.Select(AmbitionLevelLabels.ToDisplay).ToArray(),
            start: (int)AmbitionLevel.AboutRight,
            index =>
            {
                var level = levels[index];
                _log.LogReflection(_session.Snapshot, level);
                _session.SubmitReflection(level, distracted: false);
                if (_session.Phase == SessionPhase.OnBreak)
                    _log.LogBreakStarted(_session.Snapshot);
                PersistState();
                RenderPhase();
            }));
    }

    void ShowExtension()
    {
        EnsureLocked();
        _lock.UpdatePrimaryContent(DurationStep.Create(duration =>
        {
            _session.SubmitExtension(duration);
            _log.LogExtension(_session.Snapshot);
            PersistState();
            _lock.FadeOutThen(Unlock);
        }, "Wie viel Zeit brauchst du noch?"));
    }

    void EnsureLocked()
    {
        if (_lock.IsLocked)
            return;
        _lock.ShowLock(SpinnerView.Create());
        _ = Task.Run(DisableInternet);
    }
}
