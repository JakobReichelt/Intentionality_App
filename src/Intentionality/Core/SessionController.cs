namespace Intentionality.Core;

public sealed class SessionController
{
    public static readonly TimeSpan MandatoryBreak = TimeSpan.FromMinutes(5);
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
        _state = new SessionSnapshot
        {
            SessionId = Guid.NewGuid(),
            Phase = SessionPhase.AskingWork,
            Location = location
        };
        RaiseChanged();
    }

    public void Restore(SessionSnapshot snapshot)
    {
        _state = snapshot.Clone();
        RaiseChanged();
    }

    public void SubmitWorkingOn(string text)
    {
        EnsurePhase(SessionPhase.AskingWork);
        var trimmed = RequireText(text, "Woran du arbeitest");
        _state.WorkingOn = trimmed;
        _state.Phase = SessionPhase.AskingDuration;
        RaiseChanged();
    }

    public void SubmitDuration(TimeSpan duration)
    {
        EnsurePhase(SessionPhase.AskingDuration);
        RequireDuration(duration);
        _state.RequestedDuration = duration;
        _state.Phase = SessionPhase.AskingGoal;
        RaiseChanged();
    }

    public void SubmitGoal(string goal)
    {
        EnsurePhase(SessionPhase.AskingGoal);
        var trimmed = RequireText(goal, "Sitzungsziel");
        _state.Goal = trimmed;
        _state.DeadlineUtc = _clock.UtcNow.Add(_state.RequestedDuration!.Value);
        _state.Phase = SessionPhase.Unlocked;
        RaiseChanged();
    }

    public void SubmitGoalCompleted(bool completed)
    {
        EnsurePhase(SessionPhase.AskingCompletion);
        _state.LastCompleted = completed;
        _state.Phase = SessionPhase.AskingReflection;
        RaiseChanged();
    }

    public void EndSessionForFeedback()
    {
        EnsurePhase(SessionPhase.Unlocked);
        _state.DeadlineUtc = null;
        _state.Phase = SessionPhase.AskingCompletion;
        RaiseChanged();
    }

    public void SubmitReflection(AmbitionLevel level, bool distracted)
    {
        EnsurePhase(SessionPhase.AskingReflection);
        _state.LastAmbitionLevel = level;
        _state.LastDistracted = distracted;
        if (_state.LastCompleted == true)
        {
            _state.BreakEndsAtUtc = _clock.UtcNow.Add(MandatoryBreak);
            _state.DeadlineUtc = null;
            _state.Phase = SessionPhase.OnBreak;
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
        RequireDuration(duration);
        _state.RequestedDuration = duration;
        _state.DeadlineUtc = _clock.UtcNow.Add(duration);
        _state.Phase = SessionPhase.Unlocked;
        RaiseChanged();
    }

    /// <summary>
    /// Advances timers. Returns true if a phase transition occurred.
    /// </summary>
    public bool Tick()
    {
        var now = _clock.UtcNow;
        if (_state.Phase == SessionPhase.Unlocked
            && _state.DeadlineUtc is DateTimeOffset deadline
            && now >= deadline)
        {
            _state.Phase = SessionPhase.AskingCompletion;
            RaiseChanged();
            return true;
        }

        if (_state.Phase == SessionPhase.OnBreak
            && _state.BreakEndsAtUtc is DateTimeOffset breakEnd
            && now >= breakEnd)
        {
            var location = _state.Location;
            _state = new SessionSnapshot
            {
                SessionId = Guid.NewGuid(),
                Phase = SessionPhase.AskingWork,
                Location = location
            };
            RaiseChanged();
            return true;
        }

        return false;
    }

    public TimeSpan? GetRemaining()
    {
        var now = _clock.UtcNow;
        if (_state.Phase == SessionPhase.Unlocked && _state.DeadlineUtc is DateTimeOffset deadline)
        {
            var remaining = deadline - now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        if (_state.Phase == SessionPhase.OnBreak && _state.BreakEndsAtUtc is DateTimeOffset breakEnd)
        {
            var remaining = breakEnd - now;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        return null;
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
