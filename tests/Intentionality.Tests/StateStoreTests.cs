using System.IO;
using Intentionality.Core;
using Intentionality.Services;

namespace Intentionality.Tests;

public class StateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "intentionality-state-" + Guid.NewGuid());
    private string StatePath => Path.Combine(_dir, "state.json");

    [Fact]
    public void Load_WithoutFile_ReturnsNull()
    {
        Assert.Null(new StateStore(StatePath).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSnapshot()
    {
        var store = new StateStore(StatePath);
        var snapshot = new SessionSnapshot
        {
            Phase = SessionPhase.OnBreak,
            WorkingOn = "Thesis",
            Goal = "Draft intro",
            DoneCriterion = "Intro paragraph written",
            IfDistraction = "YouTube",
            ThenResponse = "Close the tab",
            ResumeNote = "Open the outline",
            PreviousResumeNote = "Finish citations",
            PreviousWorkingOn = "Literature",
            AllowedDistractions = ["spotify"],
            BreakEndsAtUtc = new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero),
            LastAmbitionLevel = AmbitionLevel.AboutRight
        };

        store.Save(snapshot);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(snapshot.SessionId, loaded!.SessionId);
        Assert.Equal(SessionPhase.OnBreak, loaded.Phase);
        Assert.Equal("Thesis", loaded.WorkingOn);
        Assert.Equal("Intro paragraph written", loaded.DoneCriterion);
        Assert.Equal("YouTube", loaded.IfDistraction);
        Assert.Equal("Close the tab", loaded.ThenResponse);
        Assert.Equal("Open the outline", loaded.ResumeNote);
        Assert.Equal("Finish citations", loaded.PreviousResumeNote);
        Assert.Equal("Literature", loaded.PreviousWorkingOn);
        Assert.Equal(["spotify"], loaded.AllowedDistractions);
        Assert.Equal(snapshot.BreakEndsAtUtc, loaded.BreakEndsAtUtc);
        Assert.Equal(AmbitionLevel.AboutRight, loaded.LastAmbitionLevel);
        Assert.False(File.Exists(StatePath + ".tmp"));
    }

    [Fact]
    public void Load_CorruptFile_MovesItAsideAndThrows()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StatePath, "{ not json");
        var store = new StateStore(StatePath);

        Assert.Throws<InvalidDataException>(() => store.Load());
        Assert.False(File.Exists(StatePath));
        Assert.Equal("{ not json", File.ReadAllText(store.CorruptBackupPath));
        Assert.Null(store.Load());
    }

    [Fact]
    public void Load_IgnoresFieldsFromOlderVersions()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(StatePath, """{ "phase": "AskingGoal", "lastDistracted": true }""");

        var loaded = new StateStore(StatePath).Load();

        Assert.Equal(SessionPhase.AskingGoal, loaded!.Phase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
