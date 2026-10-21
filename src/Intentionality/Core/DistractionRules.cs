namespace Intentionality.Core;

public static class DistractionRules
{
    public const string YouTube = "youtube";
    public const string Reddit = "reddit";
    public const string Spotify = "spotify";

    private static readonly HashSet<string> Browsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "iexplore", "msedgewebview2"
    };

    public static string? Match(string? processName, string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(processName))
            return null;

        var process = StripExe(processName);
        var title = windowTitle ?? "";

        if (process.Equals("Spotify", StringComparison.OrdinalIgnoreCase))
            return Spotify;

        if (!Browsers.Contains(process))
            return null;

        if (title.Contains("YouTube", StringComparison.OrdinalIgnoreCase))
            return YouTube;

        if (title.Contains("Reddit", StringComparison.OrdinalIgnoreCase)
            || title.TrimStart().StartsWith("r/", StringComparison.OrdinalIgnoreCase))
            return Reddit;

        return null;
    }

    public static string DisplayName(string ruleId) => ruleId.ToLowerInvariant() switch
    {
        YouTube => "YouTube",
        Reddit => "Reddit",
        Spotify => "Spotify",
        _ => ruleId
    };

    private static string StripExe(string processName)
    {
        if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return processName[..^4];
        return processName;
    }
}
