using System.Windows;
using Intentionality.Core;
using Intentionality.UI.Steps;

namespace Intentionality;

public sealed partial class AppHost
{
    private void RenderPhase()
    {
        var phase = _session.Phase;
        _tray.SetSessionActive(phase is SessionPhase.Unlocked or SessionPhase.CheckingDistraction);
        UpdateTray();

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
        var doneCriterion = _session.Snapshot.DoneCriterion;

        return phase switch
        {
            SessionPhase.AskingWork => TextStep.Create(
                _prompts.Pick(PromptKind.Work),
                text => Accept(() => _session.SubmitWorkingOn(text)),
                WorkHint()),
            SessionPhase.AskingGoal => TextStep.Create(
                _prompts.Pick(PromptKind.Goal),
                text => Accept(() => _session.SubmitGoal(text))),
            SessionPhase.AskingDoneCriterion => TextStep.Create(
                _prompts.Pick(PromptKind.DoneCriterion),
                text => Accept(() => _session.SubmitDoneCriterion(text))),
            SessionPhase.AskingDuration => DurationStep.Create(
                duration => Accept(() => _session.SubmitDuration(duration)),
                _prompts.Pick(PromptKind.Duration)),
            SessionPhase.AskingIfThen => IfThenStep.Create(
                _prompts.Pick(PromptKind.IfThen),
                (distraction, response) => Accept(
                    () => StartSession(distraction, response),
                    FadeOutAndUnlock)),
            SessionPhase.AskingCompletion => CompletionStep.Create(
                _prompts.Pick(PromptKind.CompletionLead),
                goal,
                completed => Accept(() => ConfirmCompletion(completed)),
                doneCriterion),
            SessionPhase.AskingResumeNote => TextStep.Create(
                _prompts.Pick(PromptKind.ResumeNote),
                text => Accept(() => ConfirmResumeNote(text))),
            SessionPhase.AskingReflection => GoalSizeStep.Create(
                goal,
                level => Accept(() => ConfirmGoalSize(level))),
            SessionPhase.AskingExtension => DurationStep.Create(
                duration => Accept(() => ExtendSession(duration), FadeOutAndUnlock),
                _prompts.Pick(PromptKind.Extension)),
            SessionPhase.CheckingDistraction => DistractionStep.Create(
                _prompts.Pick(PromptKind.DistractionLead),
                goal,
                DistractionRules.DisplayName(_session.Snapshot.PendingDistraction ?? ""),
                belongs => Accept(() => ResolveDistraction(belongs), FadeOutAndUnlock)),
            SessionPhase.OnBreak => BreakStep.Create(_session.GetRemaining),
            _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Für diese Phase gibt es keinen Schritt.")
        };
    }

    private string? WorkHint()
    {
        var snap = _session.Snapshot;
        if (!string.IsNullOrWhiteSpace(snap.PreviousResumeNote))
            return $"Nächster Schritt von letztem Mal: {snap.PreviousResumeNote}";
        if (!string.IsNullOrWhiteSpace(snap.PreviousWorkingOn))
            return $"Zuletzt: {snap.PreviousWorkingOn}";
        return null;
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

    private void StartSession(string distraction, string response)
    {
        _session.SubmitIfThen(distraction, response);
        _log.LogSessionStarted(_session.Snapshot);
    }

    private void ConfirmCompletion(bool completed)
    {
        _session.SubmitGoalCompleted(completed);
        _log.LogGoalCheck(completed);
    }

    private void ConfirmResumeNote(string text)
    {
        _session.SubmitResumeNote(text);
        _log.LogResumeNote(text);
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

    private void ResolveDistraction(bool belongsToGoal)
    {
        var ruleId = _session.Snapshot.PendingDistraction ?? "";
        if (!belongsToGoal && !string.IsNullOrWhiteSpace(ruleId))
            _guard.CloseOffending(ruleId);

        _session.ResolveDistraction(belongsToGoal);
        _log.LogDistraction(ruleId, belongsToGoal);
    }

    private void EndSessionEarly()
    {
        if (_session.Phase is not (SessionPhase.Unlocked or SessionPhase.CheckingDistraction))
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
        UpdateTray();
    }

    private void EnsureLocked()
    {
        if (!_lock.IsLocked)
            _lock.ShowLock(SpinnerView.Create());
    }

    private void UpdateTray()
    {
        var goal = _session.Phase is SessionPhase.Unlocked or SessionPhase.CheckingDistraction
            ? _session.Snapshot.Goal
            : null;
        _tray.UpdateRemaining(_session.GetRemaining(), goal);
    }
}
