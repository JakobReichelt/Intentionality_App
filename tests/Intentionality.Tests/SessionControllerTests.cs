using Intentionality.Core;

namespace Intentionality.Tests;

public class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
}

public class SessionControllerTests
{
    [Fact]
    public void FreshSession_FlowsToUnlock()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);

        session.BeginFreshSession(LocationInfo.Unavailable("test"));
        Assert.Equal(SessionPhase.AskingWork, session.Phase);

        session.SubmitWorkingOn("Thesis");
        Assert.Equal(SessionPhase.AskingDuration, session.Phase);

        session.SubmitDuration(TimeSpan.FromMinutes(45));
        Assert.Equal(SessionPhase.AskingGoal, session.Phase);

        session.SubmitGoal("Finish method section");
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

        session.SubmitReflection(AmbitionLevel.AboutRight, distracted: false);
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

        session.SubmitReflection(AmbitionLevel.ABitTooAmbitious, distracted: true);
        Assert.Equal(SessionPhase.AskingExtension, session.Phase);
        Assert.Equal(AmbitionLevel.ABitTooAmbitious, session.Snapshot.LastAmbitionLevel);
        Assert.True(session.Snapshot.LastDistracted);

        session.SubmitExtension(TimeSpan.FromMinutes(20));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal("Thesis", session.Snapshot.WorkingOn);
        Assert.Equal("Finish method section", session.Snapshot.Goal);
        Assert.Equal(clock.UtcNow.AddMinutes(20), session.Snapshot.DeadlineUtc);
    }

    [Fact]
    public void EndSessionEarly_GoesToGoalCheck()
    {
        var session = new SessionController();
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitDuration(TimeSpan.FromMinutes(30));
        session.SubmitGoal("Draft the intro");

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

    private static void StartUnlocked(SessionController session, FakeClock clock)
    {
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitDuration(TimeSpan.FromMinutes(45));
        session.SubmitGoal("Finish method section");
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(45), session.Snapshot.DeadlineUtc);
    }
}
