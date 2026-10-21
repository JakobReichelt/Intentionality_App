using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Intentionality.Core;

namespace Intentionality.Services;

public sealed class StateStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public StateStore(string? path = null)
    {
        _path = path ?? AppPaths.StateFile;
    }

    public string CorruptBackupPath => Path.ChangeExtension(_path, ".corrupt.json");

    public void Save(SessionSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Write to a temp file first so a crash mid-write never leaves a half-written state file.
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, Options));
        File.Move(tempPath, _path, overwrite: true);
    }

    /// <summary>
    /// Returns the last saved snapshot, or null if none exists.
    /// A corrupt file is moved to <see cref="CorruptBackupPath"/> and reported as <see cref="InvalidDataException"/>.
    /// </summary>
    public SessionSnapshot? Load()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionSnapshot>(File.ReadAllText(_path), Options);
        }
        catch (JsonException ex)
        {
            File.Move(_path, CorruptBackupPath, overwrite: true);
            throw new InvalidDataException(
                $"Der gespeicherte Zustand war beschädigt und wurde nach {CorruptBackupPath} verschoben.", ex);
        }
    }
}
