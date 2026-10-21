using System.IO;
using Intentionality.Core;
using Intentionality.Services;

namespace Intentionality.Tests;

public class MarkdownLogWriterTests
{
    [Fact]
    public void WritesEachInteractionInOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), "intentionality-test-" + Guid.NewGuid() + ".md");
        try
        {
            var log = new MarkdownLogWriter(path);
            var snap = new SessionSnapshot
            {
                WorkingOn = "Thesis outline",
                Goal = "Finish method section",
                DoneCriterion = "Method drafted",
                IfDistraction = "YouTube",
                ThenResponse = "Close the tab",
                RequestedDuration = TimeSpan.FromMinutes(45),
                DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(45),
                Location = new LocationInfo
                {
                    IsAvailable = true,
                    Latitude = 52.374,
                    Longitude = 9.738,
                    AccuracyMeters = 40
                },
                LastCompleted = false
            };

            log.LogSessionStarted(snap);
            log.LogDistraction(DistractionRules.YouTube, belongsToGoal: false);
            log.LogEndedEarly();
            log.LogGoalCheck(completed: false);
            log.LogReflection(AmbitionLevel.FarTooAmbitious);
            snap.RequestedDuration = TimeSpan.FromMinutes(15);
            snap.DeadlineUtc = DateTimeOffset.UtcNow.AddMinutes(15);
            log.LogExtension(snap);

            var text = File.ReadAllText(path);
            Assert.Equal(1, CountHeadings(text, 3));
            Assert.Equal(2, CountHeadings(text, 4));
            Assert.Contains("— Sitzung", text);
            Assert.Contains("— Ablenkung", text);
            Assert.Contains("— Verlängert", text);
            Assert.DoesNotContain("— Zielprüfung", text);
            Assert.DoesNotContain("— Zielgröße", text);
            Assert.Contains("Thesis outline", text);
            Assert.Contains("Finish method section", text);
            Assert.Contains("Fertig, wenn: Method drafted", text);
            Assert.Contains("Wenn: YouTube / Dann: Close the tab", text);
            Assert.Contains("YouTube – geschlossen", text);
            Assert.Contains("52.37400", text);
            Assert.Contains("Zeit: 45 Minuten", text);
            Assert.Contains("Erreicht: Nein", text);
            Assert.Contains("Zielgröße: Zu groß", text);
            Assert.Contains("Zusätzliche Zeit: 15 Minuten", text);
            Assert.Contains("Vorzeitig beendet:", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LogsEveryExtensionAsItsOwnEntry()
    {
        var path = Path.Combine(Path.GetTempPath(), "intentionality-test-" + Guid.NewGuid() + ".md");
        try
        {
            var log = new MarkdownLogWriter(path);
            var startedUntil = new DateTimeOffset(2026, 10, 16, 8, 10, 0, TimeSpan.Zero);
            var firstUntil = new DateTimeOffset(2026, 10, 16, 8, 30, 0, TimeSpan.Zero);
            var secondUntil = new DateTimeOffset(2026, 10, 16, 8, 40, 15, TimeSpan.Zero);
            var snap = new SessionSnapshot
            {
                WorkingOn = "Photos",
                Goal = "Sort the batch",
                RequestedDuration = TimeSpan.FromMinutes(30),
                DeadlineUtc = startedUntil
            };

            log.LogSessionStarted(snap);

            log.LogGoalCheck(completed: false);
            log.LogReflection(AmbitionLevel.ABitTooAmbitious);
            snap.RequestedDuration = TimeSpan.FromMinutes(20);
            snap.DeadlineUtc = firstUntil;
            log.LogExtension(snap);

            log.LogTimeExpired();
            log.LogGoalCheck(completed: false);
            log.LogReflection(AmbitionLevel.AboutRight);
            snap.RequestedDuration = TimeSpan.FromMinutes(10);
            snap.DeadlineUtc = secondUntil;
            log.LogExtension(snap);

            var text = File.ReadAllText(path);
            Assert.Equal(1, CountHeadings(text, 3));
            Assert.Equal(2, CountHeadings(text, 4));
            Assert.Equal(2, CountOf(text, "— Verlängert"));
            Assert.Contains("Zielgröße: Zu groß", text);
            Assert.Contains("Zielgröße: Passend", text);
            Assert.Contains("Zeit: 30 Minuten", text);
            Assert.Contains("Zusätzliche Zeit: 20 Minuten", text);
            Assert.Contains("Zusätzliche Zeit: 10 Minuten", text);
            Assert.Contains(MarkdownLogWriter.FormatLocal(startedUntil), text);
            Assert.Contains(MarkdownLogWriter.FormatLocal(firstUntil), text);
            Assert.Contains(MarkdownLogWriter.FormatLocal(secondUntil), text);
            Assert.Contains("Photos", text);
            Assert.Contains("Sort the batch", text);

            var first = text.IndexOf("— Verlängert", StringComparison.Ordinal);
            var second = text.IndexOf("— Verlängert", first + 1, StringComparison.Ordinal);
            Assert.True(first < second);
            var firstBlock = text[first..second];
            var secondBlock = text[second..];
            Assert.Contains("Zielgröße: Zu groß", firstBlock);
            Assert.Contains("Zusätzliche Zeit: 20 Minuten", firstBlock);
            Assert.Contains("Zielgröße: Passend", secondBlock);
            Assert.Contains("Zusätzliche Zeit: 10 Minuten", secondBlock);
            Assert.Contains("Zeit abgelaufen:", secondBlock);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LogBreakStarted_IncludesResumeNote()
    {
        var path = Path.Combine(Path.GetTempPath(), "intentionality-test-" + Guid.NewGuid() + ".md");
        try
        {
            var log = new MarkdownLogWriter(path);
            var snap = new SessionSnapshot
            {
                WorkingOn = "Code",
                Goal = "Ship feature",
                ActiveBreakDuration = TimeSpan.FromMinutes(5),
                BreakEndsAtUtc = DateTimeOffset.UtcNow.AddMinutes(5)
            };

            log.LogGoalCheck(completed: true);
            log.LogResumeNote("Write the release notes");
            log.LogReflection(AmbitionLevel.AboutRight);
            log.LogBreakStarted(snap);

            var text = File.ReadAllText(path);
            Assert.Contains("— Pause", text);
            Assert.Contains("Nächster Schritt: Write the release notes", text);
            Assert.Contains("Erreicht: Ja", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void LogError_WritesTitleAndMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), "intentionality-test-" + Guid.NewGuid() + ".md");
        try
        {
            var log = new MarkdownLogWriter(path);

            log.LogError("Zustand konnte nicht gespeichert werden", new IOException("Datei gesperrt"));

            var text = File.ReadAllText(path);
            Assert.Contains("— Zustand konnte nicht gespeichert werden", text);
            Assert.Contains("- Fehler: Datei gesperrt", text);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static int CountHeadings(string text, int level)
    {
        var marker = new string('#', level) + " ";
        var deeper = new string('#', level + 1) + " ";
        return CountOf(text, marker) - CountOf(text, deeper);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }
}
