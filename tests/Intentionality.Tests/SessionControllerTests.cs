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
        Assert.Equal(SessionPhase.AskingDoneCriterion, session.Phase);

        session.SubmitDoneCriterion("Method section drafted");
        Assert.Equal(SessionPhase.AskingDuration, session.Phase);

        session.SubmitDuration(TimeSpan.FromMinutes(45));
        Assert.Equal(SessionPhase.AskingIfThen, session.Phase);
        Assert.Equal(TimeSpan.FromMinutes(45), session.Snapshot.RequestedDuration);
        Assert.Null(session.Snapshot.DeadlineUtc);

        session.SubmitIfThen("YouTube", "Close the tab and stretch");
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(45), session.Snapshot.DeadlineUtc);
        Assert.Equal("YouTube", session.Snapshot.IfDistraction);
        Assert.Equal("Close the tab and stretch", session.Snapshot.ThenResponse);
        Assert.Equal("Method section drafted", session.Snapshot.DoneCriterion);
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
        session.SubmitDoneCriterion("Done when written");
        Assert.Throws<ArgumentException>(() => session.SubmitDuration(TimeSpan.Zero));
    }

    [Fact]
    public void GoalCompleted_AsksResumeNote_ThenBreak_ThenCarriesNote()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        clock.UtcNow = clock.UtcNow.AddMinutes(45);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingCompletion, session.Phase);

        session.SubmitGoalCompleted(completed: true);
        Assert.Equal(SessionPhase.AskingResumeNote, session.Phase);
        Assert.True(session.Snapshot.LastCompleted);

        session.SubmitResumeNote("Open the results table");
        Assert.Equal(SessionPhase.AskingReflection, session.Phase);
        Assert.Equal("Open the results table", session.Snapshot.ResumeNote);

        session.SubmitReflection(AmbitionLevel.AboutRight);
        Assert.Equal(SessionPhase.OnBreak, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(5), session.Snapshot.BreakEndsAtUtc);

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingWork, session.Phase);
        Assert.Null(session.Snapshot.WorkingOn);
        Assert.Null(session.Snapshot.Goal);
        Assert.Equal("Open the results table", session.Snapshot.PreviousResumeNote);
        Assert.Equal("Thesis", session.Snapshot.PreviousWorkingOn);
    }

    [Fact]
    public void BeginFreshSession_CarriesResumeFromPreviousSnapshot()
    {
        var previous = new SessionSnapshot
        {
            WorkingOn = "Old work",
            ResumeNote = "Start with the intro"
        };
        var session = new SessionController();
        session.BeginFreshSession(null, previous);

        Assert.Equal("Start with the intro", session.Snapshot.PreviousResumeNote);
        Assert.Equal("Old work", session.Snapshot.PreviousWorkingOn);
    }

    [Fact]
    public void GoalNotCompleted_SkipsResumeNote_ReflectionThenExtension()
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
    public void Extension_SkipsIfThenStep()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        session.EndSessionForFeedback();
        session.SubmitGoalCompleted(completed: false);
        session.SubmitReflection(AmbitionLevel.AboutRight);
        Assert.Equal(SessionPhase.AskingExtension, session.Phase);

        session.SubmitExtension(TimeSpan.FromMinutes(10));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
    }

    [Fact]
    public void GoalNotCompleted_ShortTimedBlock_SkipsFailureBreak()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        session.BeginFreshSession(null);
        session.SubmitWorkingOn("Thesis");
        session.SubmitGoal("Small task");
        session.SubmitDoneCriterion("Task done");
        session.SubmitDuration(TimeSpan.FromMinutes(10));
        session.SubmitIfThen("Reddit", "Close tab");

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
        session.SubmitDoneCriterion("Chapter drafted");
        session.SubmitDuration(TimeSpan.FromMinutes(15));
        session.SubmitIfThen("Spotify", "Pause music");

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
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        session.EndSessionForFeedback();
        Assert.Equal(SessionPhase.AskingCompletion, session.Phase);
        Assert.Null(session.Snapshot.DeadlineUtc);
    }

    [Fact]
    public void ReportDistraction_Prompts_AndAllowRemembers()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        Assert.True(session.ReportDistraction(DistractionRules.YouTube));
        Assert.Equal(SessionPhase.CheckingDistraction, session.Phase);
        Assert.Equal(DistractionRules.YouTube, session.Snapshot.PendingDistraction);

        session.ResolveDistraction(belongsToGoal: true);
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Contains(DistractionRules.YouTube, session.Snapshot.AllowedDistractions);
        Assert.Null(session.Snapshot.PendingDistraction);

        Assert.False(session.ReportDistraction(DistractionRules.YouTube));
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
    }

    [Fact]
    public void ReportDistraction_Reject_ReturnsToUnlockedWithoutAllowing()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        session.ReportDistraction(DistractionRules.Reddit);
        session.ResolveDistraction(belongsToGoal: false);

        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Empty(session.Snapshot.AllowedDistractions);
        Assert.True(session.ReportDistraction(DistractionRules.Reddit));
    }

    [Fact]
    public void Tick_DuringDistractionCheck_ExpiresToCompletion()
    {
        var clock = new FakeClock();
        var session = new SessionController(clock);
        StartUnlocked(session, clock);

        session.ReportDistraction(DistractionRules.Spotify);
        Assert.Equal(SessionPhase.CheckingDistraction, session.Phase);

        clock.UtcNow = clock.UtcNow.AddMinutes(45);
        Assert.True(session.Tick());
        Assert.Equal(SessionPhase.AskingCompletion, session.Phase);
        Assert.Null(session.Snapshot.PendingDistraction);
        Assert.True(session.Snapshot.ExpiredByTimer);
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
        session.SubmitDoneCriterion("Method section drafted");
        session.SubmitDuration(TimeSpan.FromMinutes(45));
        session.SubmitIfThen("YouTube", "Close the tab");
        Assert.Equal(SessionPhase.Unlocked, session.Phase);
        Assert.Equal(clock.UtcNow.AddMinutes(45), session.Snapshot.DeadlineUtc);
    }
}
