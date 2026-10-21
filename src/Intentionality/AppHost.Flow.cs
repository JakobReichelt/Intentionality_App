using System.Windows;
using Intentionality.Core;
using Intentionality.UI.Steps;

namespace Intentionality;

public sealed partial class AppHost
{
    private void RenderPhase()
    {
        var phase = _session.Phase;
        _tray.SetSessionActive(phase == SessionPhase.Unlocked);
        _tray.UpdateRemaining(_session.GetRemaining());

        if (phase == SessionPhase.Unlocked)
        {
            if (_lock.IsLocked)
                Unlock();
            return;
        }

        EnsureLocked();
        _lock.UpdatePrimaryContent(CreateStep(phase));
    }

    private UIElement CreateStep(SessionPhase phase)
    {
        void Accept(Action apply, Action? onApplied = null) =>
            ApplyInput(phase, apply, onApplied ?? RenderPhase);

        var goal = _session.Snapshot.Goal ?? "";

        return phase switch
        {
            SessionPhase.AskingWork => TextStep.Create(
                "Woran arbeitest du gerade?",
                text => Accept(() => _session.SubmitWorkingOn(text))),
            SessionPhase.AskingGoal => TextStep.Create(
                "Was ist das Ziel dieser Sitzung?",
                text => Accept(() => _session.SubmitGoal(text))),
            SessionPhase.AskingDuration => DurationStep.Create(
                duration => Accept(() => StartSession(duration), FadeOutAndUnlock)),
            SessionPhase.AskingCompletion => CompletionStep.Create(
                goal,
                completed => Accept(() => ConfirmCompletion(completed))),
            SessionPhase.AskingReflection => GoalSizeStep.Create(
                goal,
                level => Accept(() => ConfirmGoalSize(level))),
            SessionPhase.AskingExtension => DurationStep.Create(
                duration => Accept(() => ExtendSession(duration), FadeOutAndUnlock),
                "Wie viel Zeit brauchst du noch?"),
            SessionPhase.OnBreak => BreakStep.Create(_session.GetRemaining),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Für diese Phase gibt es keinen Schritt.")
        };
    }

    private void ApplyInput(SessionPhase stepPhase, Action apply, Action onApplied)
    {
        // A step can outlive its phase, e.g. a second Enter during the unlock fade-out.
        if (_session.Phase != stepPhase)
            return;

        try
        {
            apply();
        }
        catch (ArgumentException ex)
        {
            _log.LogError("Eingabe wurde abgelehnt", ex);
            RenderPhase();
            return;
        }

        onApplied();
    }

    private void StartSession(TimeSpan duration)
    {
        _session.SubmitDuration(duration);
        _log.LogSessionStarted(_session.Snapshot);
    }

    private void ConfirmCompletion(bool completed)
    {
        _session.SubmitGoalCompleted(completed);
        _log.LogGoalCheck(completed);
    }

    private void ConfirmGoalSize(AmbitionLevel level)
    {
        _session.SubmitReflection(level);
        _log.LogReflection(level);
        if (_session.Phase == SessionPhase.OnBreak)
            _log.LogBreakStarted(_session.Snapshot);
    }

    private void ExtendSession(TimeSpan duration)
    {
        _session.SubmitExtension(duration);
        _log.LogExtension(_session.Snapshot);
    }

    private void EndSessionEarly()
    {
        if (_session.Phase != SessionPhase.Unlocked)
            return;

        _session.EndSessionForFeedback();
        _log.LogEndedEarly();
        RenderPhase();
    }

    private void FadeOutAndUnlock() => _lock.FadeOutThen(Unlock);

    private void Unlock()
    {
        _lock.HideLock();
        _tray.SetSessionActive(true);
        _tray.UpdateRemaining(_session.GetRemaining());
    }

    private void EnsureLocked()
    {
        if (!_lock.IsLocked)
            _lock.ShowLock(SpinnerView.Create());
    }
}
