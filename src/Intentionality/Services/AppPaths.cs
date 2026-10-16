using System.IO;

namespace Intentionality.Services;

public static class AppPaths
{
    public static string LocalAppDataDir
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Intentionality");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static string StateFile => Path.Combine(LocalAppDataDir, "state.json");
    public static string NetworkStateFile => Path.Combine(LocalAppDataDir, "network-state.json");

    /// <summary>
    /// Resolves the markdown log path under the project logs folder when running
    /// from a publish or bin output next to the repo, otherwise under LocalAppData.
    /// </summary>
    public static string ResolveLogFile()
    {
        var candidates = new List<string>();

        var baseDir = AppContext.BaseDirectory;
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "logs")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "..", "logs")));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "logs")));

        // publish\ sits next to logs\ in the repo root
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "..", "logs")));

        foreach (var logsDir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var probe = Path.GetDirectoryName(logsDir.TrimEnd(Path.DirectorySeparatorChar));
                if (probe is not null
                    && (File.Exists(Path.Combine(probe, "Intentionality.sln"))
                        || Directory.Exists(Path.Combine(probe, "src"))))
                {
                    Directory.CreateDirectory(logsDir);
                    return Path.Combine(logsDir, "intentionality.md");
                }
            }
            catch
            {
                // try next candidate
            }
        }

        var fallback = Path.Combine(LocalAppDataDir, "logs");
        Directory.CreateDirectory(fallback);
        return Path.Combine(fallback, "intentionality.md");
    }
}
