using System.IO;
using System.Text;
using Intentionality.Core;

namespace Intentionality.Services;

public sealed class MarkdownLogWriter
{
    private readonly string _path;
    private readonly object _gate = new();
    private CycleNotes _cycle = new();

    public MarkdownLogWriter(string? path = null)
    {
        _path = path ?? AppPaths.ResolveLogFile();
        EnsureHeader();
    }

    public string Path => _path;

    public void LogSessionStarted(SessionSnapshot snapshot)
    {
        ResetCycle();
        AppendEvent(
            3,
            "Sitzung",
            $"- Ort: {snapshot.Location?.ToLogString() ?? "nicht verfügbar"}",
            WorkLine(snapshot),
            $"- Zeit: {FormatDuration(snapshot.RequestedDuration)}",
            GoalLine(snapshot),
            UntilLine(snapshot.DeadlineUtc));
    }

    public void LogInterrupted(SessionSnapshot? previous)
    {
        ResetCycle();
        AppendEvent(
            3,
            "Sitzung unterbrochen (neue Anmeldung)",
            previous is null ? null : $"- Vorherige Phase: {FormatPhase(previous.Phase)}",
            WorkLine(previous, "Vorher woran"),
            GoalLine(previous, "Vorheriges Ziel"),
            UntilLine(previous?.DeadlineUtc, "Vorherige Frist"));
    }

    public void LogEndedEarly(SessionSnapshot snapshot)
    {
        _cycle.EndedEarlyAt = DateTimeOffset.Now;
    }

    public void LogTimeExpired(SessionSnapshot snapshot)
    {
        _cycle.TimeExpiredAt = DateTimeOffset.Now;
    }

    public void LogGoalCheck(SessionSnapshot snapshot, bool completed)
    {
        _cycle.Completed = completed;
    }

    public void LogReflection(SessionSnapshot snapshot, AmbitionLevel level)
    {
        _cycle.GoalSize = AmbitionLevelLabels.ToDisplay(level);
    }

    public void LogExtension(SessionSnapshot snapshot)
    {
        AppendEvent(
            4,
            "Verlängert",
            WorkLine(snapshot),
            GoalLine(snapshot),
            _cycle.Completed is bool completed ? $"- Erreicht: {(completed ? "Ja" : "Nein")}" : null,
            _cycle.GoalSize is not null ? $"- Zielgröße: {_cycle.GoalSize}" : null,
            _cycle.EndedEarlyAt is DateTimeOffset ended ? $"- Vorzeitig beendet: {FormatLocal(ended)}" : null,
            _cycle.TimeExpiredAt is DateTimeOffset expired ? $"- Zeit abgelaufen: {FormatLocal(expired)}" : null,
            $"- Zusätzliche Zeit: {FormatDuration(snapshot.RequestedDuration)}",
            UntilLine(snapshot.DeadlineUtc));
        ResetCycle();
    }

    public void LogBreakStarted(SessionSnapshot snapshot)
    {
        AppendEvent(
            4,
            "Pause",
            WorkLine(snapshot),
            GoalLine(snapshot),
            _cycle.Completed is bool completed ? $"- Erreicht: {(completed ? "Ja" : "Nein")}" : null,
            _cycle.GoalSize is not null ? $"- Zielgröße: {_cycle.GoalSize}" : null,
            $"- Dauer: {FormatDuration(SessionController.MandatoryBreak)}",
            UntilLine(snapshot.BreakEndsAtUtc, "Pause bis"));
        ResetCycle();
    }

    public void LogBreakEnded(SessionSnapshot snapshot)
    {
    }

    public void LogLocationSample(LocationInfo location)
    {
        AppendEvent(3, "Ort erfasst", $"- Ort: {location.ToLogString()}");
    }

    public void AppendRaw(string markdownBlock)
    {
        Append(markdownBlock.TrimEnd() + Environment.NewLine);
    }

    private void ResetCycle() => _cycle = new CycleNotes();

    private sealed class CycleNotes
    {
        public DateTimeOffset? EndedEarlyAt { get; set; }
        public DateTimeOffset? TimeExpiredAt { get; set; }
        public bool? Completed { get; set; }
        public string? GoalSize { get; set; }
    }

    private void AppendEvent(int level, string title, params string?[] lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{new string('#', level)} {FormatLocal(DateTimeOffset.Now)} — {title}");
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
                sb.AppendLine(line);
        }

        Append(sb.ToString());
    }

    private static string? WorkLine(SessionSnapshot? snapshot, string label = "Woran")
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.WorkingOn))
            return null;
        return $"- {label}: {snapshot.WorkingOn}";
    }

    private static string? GoalLine(SessionSnapshot? snapshot, string label = "Ziel")
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Goal))
            return null;
        return $"- {label}: {snapshot.Goal}";
    }

    private static string? UntilLine(DateTimeOffset? until, string label = "Frei bis")
    {
        if (until is not DateTimeOffset value)
            return null;
        return $"- {label}: {FormatLocal(value)}";
    }

    private static string FormatPhase(SessionPhase phase) => phase switch
    {
        SessionPhase.AskingWork => "Frage nach der Arbeit",
        SessionPhase.AskingDuration => "Frage nach der Zeit",
        SessionPhase.AskingGoal => "Frage nach dem Ziel",
        SessionPhase.Unlocked => "Freigeschaltet",
        SessionPhase.AskingCompletion => "Frage, ob das Ziel erreicht ist",
        SessionPhase.AskingReflection => "Frage nach der Zielgröße",
        SessionPhase.AskingExtension => "Frage nach der Verlängerung",
        SessionPhase.OnBreak => "Pause",
        _ => phase.ToString()
    };

    private void EnsureHeader()
    {
        lock (_gate)
        {
            var dir = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(_path) || new FileInfo(_path).Length == 0)
            {
                File.WriteAllText(_path,
                    "# Intentionality-Protokoll" + Environment.NewLine + Environment.NewLine
                    + "Fortlaufendes Sitzungsprotokoll." + Environment.NewLine + Environment.NewLine);
            }
        }
    }

    private void Append(string block)
    {
        lock (_gate)
        {
            EnsureHeader();
            File.AppendAllText(_path, block.TrimEnd() + Environment.NewLine + Environment.NewLine);
        }
    }

    public static string FormatLocal(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        var tz = TimeZoneInfo.Local;
        var abbr = tz.IsDaylightSavingTime(local) ? GetDstAbbreviation(tz) : GetStandardAbbreviation(tz);
        return $"{local:yyyy-MM-dd HH:mm:ss} {abbr}";
    }

    private static string GetDstAbbreviation(TimeZoneInfo tz)
    {
        if (tz.Id.Contains("W. Europe", StringComparison.OrdinalIgnoreCase)
            || tz.Id.Contains("Romance", StringComparison.OrdinalIgnoreCase)
            || tz.StandardName.Contains("Mitteleurop", StringComparison.OrdinalIgnoreCase))
            return "CEST";
        return "DST";
    }

    private static string GetStandardAbbreviation(TimeZoneInfo tz)
    {
        if (tz.Id.Contains("W. Europe", StringComparison.OrdinalIgnoreCase)
            || tz.Id.Contains("Romance", StringComparison.OrdinalIgnoreCase)
            || tz.StandardName.Contains("Mitteleurop", StringComparison.OrdinalIgnoreCase))
            return "CET";
        return tz.GetUtcOffset(DateTimeOffset.Now) is var offset
            ? $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:00}:{Math.Abs(offset.Minutes):00}"
            : "local";
    }

    public static string FormatDuration(TimeSpan? duration)
    {
        if (duration is null)
            return "n/a";

        var d = duration.Value;
        if (d.TotalHours >= 1)
        {
            var hours = (int)d.TotalHours;
            var minutes = d.Minutes;
            if (minutes == 0)
                return hours == 1 ? "1 Stunde" : $"{hours} Stunden";
            return $"{hours} Std. {minutes} Min.";
        }

        var mins = (int)Math.Round(d.TotalMinutes);
        return mins == 1 ? "1 Minute" : $"{mins} Minuten";
    }
}
