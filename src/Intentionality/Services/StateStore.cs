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

    public void Save(SessionSnapshot snapshot)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(snapshot, Options);
        File.WriteAllText(_path, json);
    }

    public SessionSnapshot? Load()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<SessionSnapshot>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
