namespace Intentionality.Core;

public sealed class SessionSnapshot
{
    public Guid SessionId { get; set; } = Guid.NewGuid();
    public SessionPhase Phase { get; set; } = SessionPhase.AskingWork;
    public string? WorkingOn { get; set; }
    public string? Goal { get; set; }
    public TimeSpan? RequestedDuration { get; set; }
    public DateTimeOffset? DeadlineUtc { get; set; }
    public DateTimeOffset? BreakEndsAtUtc { get; set; }
    public LocationInfo? Location { get; set; }
    public AmbitionLevel? LastAmbitionLevel { get; set; }
    public bool? LastDistracted { get; set; }
    public bool? LastCompleted { get; set; }

    public SessionSnapshot Clone() => new()
    {
        SessionId = SessionId,
        Phase = Phase,
        WorkingOn = WorkingOn,
        Goal = Goal,
        RequestedDuration = RequestedDuration,
        DeadlineUtc = DeadlineUtc,
        BreakEndsAtUtc = BreakEndsAtUtc,
        Location = Location,
        LastAmbitionLevel = LastAmbitionLevel,
        LastDistracted = LastDistracted,
        LastCompleted = LastCompleted
    };
}
