using Intentionality.Core;

namespace Intentionality.Tests;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
}

public class SessionControllerTests
{
    static readonly TimeSpan IncompleteFailureBreak = SessionController.IncompleteFailureBreak;

    [Fact]
    public void FreshSession_FlowsToUnlock()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);

        session.BeginFreshSession(LocationInfo.Unavailable("test"));
        Assert.Equal(SessionPhase.AskingWork, session.Phase);

        session.SubmitWorkingOn("Thesis");
        Assert.Equal(SessionPhase.AskingGoal, session.Phase);

        session.SubmitGoal("Finish method section");
        Assert.Equal(SessionPhase.AskingDuration, session.Phase);

        session.SubmitDuration(TimeSpan.FromMinutes(45));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(45), session.Snapshot.DeadlineUtc);
    }

    [Fact]
    public void EmptyWorkingOn_IsRejected()
    {
        var session = new SessionController();
        session.BeginFreshSession(null);
        Assert.Throws<ArgumentException>(() => session.SubmitWorkingOn("   "));
    }

    [Fact]
    public void DurationBelowOneMinute_IsRejected()
    {
        var session = new SessionController();
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Work");
        session.SubmitGoal("A goal");
        Assert.Throws<ArgumentException>(() => session.SubmitDuration(TimeSpan.Zero));
    }

    [Fact]
    public void GoalCompleted_StartsMandatoryBreak_ThenRestarts()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        clock.UtcNow = clock.UtcNow.AddMinutes(45);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingCompletion, session.Phase);

        session.SubmitGoalCompleted(completed: true);
        Assert.Equal(SessionPhase.AskingReflection, session.Phase);
        Assert.True(session.Snapshot.LastCompleted);

        session.SubmitReflection(AmbitionLevel.AboutRight);
        Assert.Equal(SessionPhase.OnBreak, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(5), session.Snapshot.BreakEndsAtUtc);

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingWork, session.Phase);
        Assert.Null(session.Snapshot.WorkingOn);
        Assert.Null(session.Snapshot.Goal);
    }

    [Fact]
    public void GoalNotCompleted_ReflectionThenExtension()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        clock.UtcNow = clock.UtcNow.AddMinutes(45);
        session.Tick();
        session.SubmitGoalCompleted(completed: false);
        Assert.Equal(SessionPhase.AskingReflection, session.Phase);

        session.SubmitReflection(AmbitionLevel.ABitTooAmbitious);
        Assert.Equal(SessionPhase.OnBreak, session.Phase);
        Assert.Equal(IncompleteFailureBreak, session.Snapshot.ActiveBreakDuration);
        Assert.Equal(clock.UtcNow.AddMinutes(2), session.Snapshot.BreakEndsAtUtc);

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingExtension, session.Phase);
        Assert.Equal(AmbitionLevel.ABitTooAmbitious, session.Snapshot.LastAmbitionLevel);

        session.SubmitExtension(TimeSpan.FromMinutes(20));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal("Thesis", session.Snapshot.WorkingOn);
        Assert.Equal("Finish method section", session.Snapshot.Goal);
        Assert.Equal(clock.UtcNow.AddMinutes(20), session.Snapshot.DeadlineUtc);
    }

    [Fact]
    public void GoalNotCompleted_ShortTimedBlock_SkipsFailureBreak()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitGoal("Small task");
        session.SubmitDuration(TimeSpan.FromMinutes(10));

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        session.Tick();
        session.SubmitGoalCompleted(completed: false);
        Assert.False(session.Snapshot.PendingIncompleteFailureBreak);
        session.SubmitReflection(AmbitionLevel.AboutRight);

        Assert.Equal(SessionPhase.AskingExtension, session.Phase);
    }

    [Fact]
    public void GoalNotCompleted_FifteenMinuteTimedExpiry_TakesFailureBreak()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitGoal("Chapter");
        session.SubmitDuration(TimeSpan.FromMinutes(15));

        clock.UtcNow = clock.UtcNow.AddMinutes(15);
        session.Tick();
        session.SubmitGoalCompleted(completed: false);
        Assert.True(session.Snapshot.PendingIncompleteFailureBreak);

        session.SubmitReflection(AmbitionLevel.AboutRight);
        Assert.Equal(SessionPhase.OnBreak, session.Phase);
        Assert.Equal(IncompleteFailureBreak, session.Snapshot.ActiveBreakDuration);
    }

    [Fact]
    public void GoalNotCompleted_EndedEarly_SkipsFailureBreak()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        session.EndSessionForFeedback();
        session.SubmitGoalCompleted(completed: false);
        session.SubmitReflection(AmbitionLevel.AboutRight);

        Assert.Equal(SessionPhase.AskingExtension, session.Phase);
        Assert.False(session.Snapshot.ExpiredByTimer);
    }

    [Fact]
    public void EndSessionEarly_GoesToGoalCheck()
    {
        var session = new SessionController();
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitGoal("Draft the intro");
        session.SubmitDuration(TimeSpan.FromMinutes(30));

        session.EndSessionForFeedback();
        Assert.Equal(SessionPhase.AskingCompletion, session.Phase);
        Assert.Null(session.Snapshot.DeadlineUtc);
    }

    [Fact]
    public void Tick_BeforeDeadline_DoesNothing()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        clock.UtcNow = clock.UtcNow.AddMinutes(10);
        Assert.False(session.Tick());
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
    }

    [Fact]
    public void UpdateLocation_KeepsPhaseAndAnswers()
    {
        var session = new SessionController();
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");

        session.UpdateLocation(LocationInfo.Unavailable("test"));

        Assert.Equal(SessionPhase.AskingGoal, session.Phase);
        Assert.Equal("Thesis", session.Snapshot.WorkingOn);
        Assert.False(session.Snapshot.Location!.IsAvailable);
    }

    private static void StartUnlocked(SessionController session, FakeClock clock)
    {
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitGoal("Finish method section");
        session.SubmitDuration(TimeSpan.FromMinutes(45));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(45), session.Snapshot.DeadlineUtc);
    }
}
