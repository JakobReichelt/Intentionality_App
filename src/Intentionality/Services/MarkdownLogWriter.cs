using System.IO;
using System.Text;
using Intentionality.Core;

namespace Intentionality.Services;

public sealed class MarkdownLogWriter
{
    private const int SessionHeading = 3;
    private const int CycleHeading = 4;

    private readonly string _path;
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
            SessionHeading,
            "Sitzung",
            $"- Ort: {snapshot.Location?.ToLogString() ?? "nicht verfügbar"}",
            WorkLine(snapshot),
            $"- Zeit: {FormatDuration(snapshot.RequestedDuration)}",
            GoalLine(snapshot),
            UntilLine(snapshot.DeadlineUtc));
    }

    public void LogInterrupted(SessionSnapshot previous)
    {
        ResetCycle();
        AppendEvent(
            SessionHeading,
            "Sitzung unterbrochen (neue Anmeldung)",
            $"- Vorherige Phase: {FormatPhase(previous.Phase)}",
            WorkLine(previous, "Vorher woran"),
            GoalLine(previous, "Vorheriges Ziel"),
            UntilLine(previous.DeadlineUtc, "Vorherige Frist"));
    }

    public void LogEndedEarly() => _cycle.EndedEarlyAt = DateTimeOffset.Now;

    public void LogTimeExpired() => _cycle.TimeExpiredAt = DateTimeOffset.Now;

    public void LogGoalCheck(bool completed) => _cycle.Completed = completed;

    public void LogReflection(AmbitionLevel level) => _cycle.GoalSize = AmbitionLevelLabels.ToDisplay(level);

    public void LogExtension(SessionSnapshot snapshot)
    {
        AppendEvent(
            CycleHeading,
            "Verlängert",
            WorkLine(snapshot),
            GoalLine(snapshot),
            CompletedLine(),
            GoalSizeLine(),
            _cycle.EndedEarlyAt is { } ended ? $"- Vorzeitig beendet: {FormatLocal(ended)}" : null,
            _cycle.TimeExpiredAt is { } expired ? $"- Zeit abgelaufen: {FormatLocal(expired)}" : null,
            $"- Zusätzliche Zeit: {FormatDuration(snapshot.RequestedDuration)}",
            UntilLine(snapshot.DeadlineUtc));
        ResetCycle();
    }

    public void LogBreakStarted(SessionSnapshot snapshot)
    {
        AppendEvent(
            CycleHeading,
            "Pause",
            WorkLine(snapshot),
            GoalLine(snapshot),
            CompletedLine(),
            GoalSizeLine(),
            $"- Dauer: {FormatDuration(snapshot.ActiveBreakDuration ?? SessionController.MandatoryBreak)}",
            UntilLine(snapshot.BreakEndsAtUtc, "Pause bis"));
        ResetCycle();
    }

    public void LogError(string title, Exception exception)
    {
        AppendEvent(SessionHeading, title, $"- Fehler: {exception.Message}");
    }

    public static string FormatLocal(DateTimeOffset value)
    {
        var local = value.ToLocalTime();
        return $"{local:yyyy-MM-dd HH:mm:ss} {ZoneAbbreviation(local)}";
    }

    public static string FormatDuration(TimeSpan? duration)
    {
        if (duration is not { } value)
            return "n/a";

        if (value.TotalHours >= 1)
        {
            var hours = (int)value.TotalHours;
            if (value.Minutes == 0)
                return hours == 1 ? "1 Stunde" : $"{hours} Stunden";
            return $"{hours} Std. {value.Minutes} Min.";
        }

        var minutes = (int)Math.Round(value.TotalMinutes);
        return minutes == 1 ? "1 Minute" : $"{minutes} Minuten";
    }

    private void ResetCycle() => _cycle = new CycleNotes();

    private string? CompletedLine() =>
        _cycle.Completed is { } completed ? $"- Erreicht: {(completed ? "Ja" : "Nein")}" : null;

    private string? GoalSizeLine() =>
        _cycle.GoalSize is { } goalSize ? $"- Zielgröße: {goalSize}" : null;

    private static string? WorkLine(SessionSnapshot snapshot, string label = "Woran") =>
        string.IsNullOrWhiteSpace(snapshot.WorkingOn) ? null : $"- {label}: {snapshot.WorkingOn}";

    private static string? GoalLine(SessionSnapshot snapshot, string label = "Ziel") =>
        string.IsNullOrWhiteSpace(snapshot.Goal) ? null : $"- {label}: {snapshot.Goal}";

    private static string? UntilLine(DateTimeOffset? until, string label = "Frei bis") =>
        until is { } value ? $"- {label}: {FormatLocal(value)}" : null;

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

    private static string ZoneAbbreviation(DateTimeOffset local)
    {
        var zone = TimeZoneInfo.Local;
        var isDaylight = zone.IsDaylightSavingTime(local);
        if (IsCentralEurope(zone))
            return isDaylight ? "CEST" : "CET";
        if (isDaylight)
            return "DST";

        var offset = local.Offset;
        return $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:00}:{Math.Abs(offset.Minutes):00}";
    }

    private static bool IsCentralEurope(TimeZoneInfo zone) =>
        zone.Id.Contains("W. Europe", StringComparison.OrdinalIgnoreCase)
        || zone.Id.Contains("Romance", StringComparison.OrdinalIgnoreCase)
        || zone.StandardName.Contains("Mitteleurop", StringComparison.OrdinalIgnoreCase);

    private void AppendEvent(int headingLevel, string title, params string?[] lines)
    {
        var block = new StringBuilder();
        block.AppendLine($"{new string('#', headingLevel)} {FormatLocal(DateTimeOffset.Now)} — {title}");
        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
            block.AppendLine(line);

        EnsureHeader();
        File.AppendAllText(_path, block.ToString().TrimEnd() + Environment.NewLine + Environment.NewLine);
    }

    private void EnsureHeader()
    {
        var dir = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (File.Exists(_path) && new FileInfo(_path).Length > 0)
            return;

        File.WriteAllText(_path,
            "# Intentionality-Protokoll" + Environment.NewLine + Environment.NewLine
            + "Fortlaufendes Sitzungsprotokoll." + Environment.NewLine + Environment.NewLine);
    }

    private sealed class CycleNotes
    {
        public DateTimeOffset? EndedEarlyAt { get; set; }
        public DateTimeOffset? TimeExpiredAt { get; set; }
        public bool? Completed { get; set; }
        public string? GoalSize { get; set; }
    }
}
