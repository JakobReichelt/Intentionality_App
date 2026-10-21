namespace Intentionality.Core;

public sealed class SessionSnapshot
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public SessionPhase Phase { get; set; } = SessionPhase.AskingWork;
    public string? WorkingOn { get; set; }
    public string? Goal { get; set; }
    public string? DoneCriterion { get; set; }
    public string? IfDistraction { get; set; }
    public string? ThenResponse { get; set; }
    public string? ResumeNote { get; set; }
    public string? PreviousResumeNote { get; set; }
    public string? PreviousWorkingOn { get; set; }
    public string? PendingDistraction { get; set; }
    public List<string> AllowedDistractions { get; set; } = new();
    public TimeSpan? RequestedDuration { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public DateTimeOffset? BreakEndsAtUtc { get; set; }
    public TimeSpan? ActiveBreakDuration { get; set; }
    public SessionPhase? PhaseAfterBreak { get; set; }
    public bool ExpiredByTimer { get; set; }
    public bool PendingIncompleteFailureBreak { get; set; }
    public LocationInfo? Location { get; set; }
    public AmbitionLevel? LastAmbitionLevel { get; set; }
    public bool? LastCompleted { get; set; }

    public SessionSnapshot Clone() => new()
    {
        SessionId = SessionId,
        Phase = Phase,
        WorkingOn = WorkingOn,
        Goal = Goal,
        DoneCriterion = DoneCriterion,
        IfDistraction = IfDistraction,
        ThenResponse = ThenResponse,
        ResumeNote = ResumeNote,
        PreviousResumeNote = PreviousResumeNote,
        PreviousWorkingOn = PreviousWorkingOn,
        PendingDistraction = PendingDistraction,
        AllowedDistractions = new List<string>(AllowedDistractions),
        RequestedDuration = RequestedDuration,
        DeadlineUtc = DeadlineUtc,
        BreakEndsAtUtc = BreakEndsAtUtc,
        ActiveBreakDuration = ActiveBreakDuration,
        PhaseAfterBreak = PhaseAfterBreak,
        ExpiredByTimer = ExpiredByTimer,
        PendingIncompleteFailureBreak = PendingIncompleteFailureBreak,
        Location = Location,
        LastAmbitionLevel = LastAmbitionLevel,
        LastCompleted = LastCompleted
    };
}
