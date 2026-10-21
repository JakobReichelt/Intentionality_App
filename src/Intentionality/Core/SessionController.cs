namespace Intentionality.Core;

public sealed class SessionController
{
    public static readonly TimeSpan MandatoryBreak = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan IncompleteFailureBreak = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan LongSessionThreshold = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);

    private readonly IClock _clock;
    private SessionSnapshot _state = new();

    public SessionController(IClock? clock = null)
    {
        _clock = clock ?? new SystemClock();
    }

    public SessionSnapshot Snapshot => _state.Clone();
    public SessionPhase Phase => _state.Phase;

    public event Action? StateChanged;

    public void BeginFreshSession(LocationInfo? location)
    {
        _state = NewSession(location);
        RaiseChanged();
    }

    public void UpdateLocation(LocationInfo? location)
    {
        _state.Location = location;
        RaiseChanged();
    }

    public void SubmitWorkingOn(string text)
    {
        EnsurePhase(SessionPhase.AskingWork);
        _state.WorkingOn = RequireText(text, "Woran du arbeitest");
        _state.Phase = SessionPhase.AskingGoal;
        RaiseChanged();
    }

    public void SubmitGoal(string goal)
    {
        EnsurePhase(SessionPhase.AskingGoal);
        _state.Goal = RequireText(goal, "Sitzungsziel");
        _state.Phase = SessionPhase.AskingDuration;
        RaiseChanged();
    }

    public void SubmitDuration(TimeSpan duration)
    {
        EnsurePhase(SessionPhase.AskingDuration);
        StartTimer(duration);
        RaiseChanged();
    }

    public void SubmitGoalCompleted(bool completed)
    {
        EnsurePhase(SessionPhase.AskingCompletion);
        _state.LastCompleted = completed;
        _state.PendingIncompleteFailureBreak = !completed && RequiresIncompleteFailureBreak(_state);
        _state.Phase = SessionPhase.AskingReflection;
        RaiseChanged();
    }

    public void EndSessionForFeedback()
    {
        EnsurePhase(SessionPhase.Unlocked);
        _state.DeadlineUtc = null;
        _state.ExpiredByTimer = false;
        _state.Phase = SessionPhase.AskingCompletion;
        RaiseChanged();
    }

    public void SubmitReflection(AmbitionLevel level)
    {
        EnsurePhase(SessionPhase.AskingReflection);
        _state.LastAmbitionLevel = level;

        if (_state.LastCompleted == true)
        {
            StartBreak(MandatoryBreak, phaseAfterBreak: null);
        }
        else if (_state.PendingIncompleteFailureBreak)
        {
            _state.PendingIncompleteFailureBreak = false;
            StartBreak(IncompleteFailureBreak, SessionPhase.AskingExtension);
        }
        else
        {
            _state.Phase = SessionPhase.AskingExtension;
        }

        RaiseChanged();
    }

    public void SubmitExtension(TimeSpan duration)
    {
        EnsurePhase(SessionPhase.AskingExtension);
        StartTimer(duration);
        RaiseChanged();
    }

    /// <summary>
    /// Advances timers. Returns true if a phase transition occurred.
    /// </summary>
    public bool Tick()
    {
        var now = _clock.UtcNow;
        if (_state.Phase == SessionPhase.Unlocked && _state.DeadlineUtc is { } deadline && now >= deadline)
        {
            _state.ExpiredByTimer = true;
            _state.Phase = SessionPhase.AskingCompletion;
        }
        else if (_state.Phase == SessionPhase.OnBreak && _state.BreakEndsAtUtc is { } breakEnd && now >= breakEnd)
        {
            EndBreak();
        }
        else
        {
            return false;
        }

        RaiseChanged();
        return true;
    }

    public TimeSpan? GetRemaining()
    {
        DateTimeOffset? end = _state.Phase switch
        {
            SessionPhase.Unlocked => _state.DeadlineUtc,
            SessionPhase.OnBreak => _state.BreakEndsAtUtc,
            _ => null
        };
        if (end is not { } endsAt)
            return null;

        var remaining = endsAt - _clock.UtcNow;
        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    internal static bool RequiresIncompleteFailureBreak(SessionSnapshot state) =>
        state.LastCompleted == false
        && state.ExpiredByTimer
        && state.RequestedDuration is TimeSpan duration
        && duration >= LongSessionThreshold;

    private static SessionSnapshot NewSession(LocationInfo? location) => new()
    {
        SessionId = Guid.NewGuid(),
        Phase = SessionPhase.AskingWork,
        Location = location
    };

    private void StartTimer(TimeSpan duration)
    {
        RequireDuration(duration);
        _state.RequestedDuration = duration;
        _state.DeadlineUtc = _clock.UtcNow.Add(duration);
        _state.Phase = SessionPhase.Unlocked;
    }

    private void StartBreak(TimeSpan duration, SessionPhase? phaseAfterBreak)
    {
        _state.BreakEndsAtUtc = _clock.UtcNow.Add(duration);
        _state.ActiveBreakDuration = duration;
        _state.PhaseAfterBreak = phaseAfterBreak;
        _state.DeadlineUtc = null;
        _state.Phase = SessionPhase.OnBreak;
    }

    private void EndBreak()
    {
        if (_state.PhaseAfterBreak != SessionPhase.AskingExtension)
        {
            _state = NewSession(_state.Location);
            return;
        }

        _state.Phase = SessionPhase.AskingExtension;
        _state.BreakEndsAtUtc = null;
        _state.ActiveBreakDuration = null;
        _state.PhaseAfterBreak = null;
        _state.ExpiredByTimer = false;
    }

    private void EnsurePhase(SessionPhase expected)
    {
        if (_state.Phase != expected)
            throw new InvalidOperationException($"Expected phase {expected}, but was {_state.Phase}.");
    }

    private static string RequireText(string text, string label)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException($"{label} darf nicht leer sein.");
        return text.Trim();
    }

    private static void RequireDuration(TimeSpan duration)
    {
        if (duration < MinimumDuration)
            throw new ArgumentException($"Die Zeit muss mindestens {MinimumDuration.TotalMinutes:0} Minute betragen.");
    }

    private void RaiseChanged() => StateChanged?.Invoke();
}
