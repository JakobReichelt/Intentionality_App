using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Intentionality.Services;

public sealed class NetworkAdapterRecord
{
    public string Name { get; set; } = "";
    public string Id { get; set; } = "";
    public int InterfaceIndex { get; set; }
}

public sealed class NetworkStateDocument
{
    public DateTimeOffset SavedAt { get; set; }
    public List<NetworkAdapterRecord> Adapters { get; set; } = new();
}

public sealed class NetworkService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _statePath;

    public NetworkService(string? statePath = null)
    {
        _statePath = statePath ?? AppPaths.NetworkStateFile;
    }

    public string StatePath => _statePath;

    public bool IsDisabled { get; private set; }

    public void DisableInternet()
    {
        if (IsDisabled)
            return;

        var enabled = NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni =>
                ni.OperationalStatus == OperationalStatus.Up
                && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                && ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
            .Select(ni => new NetworkAdapterRecord
            {
                Name = ni.Name,
                Id = ni.Id
            })
            .ToList();

        // Enrich with interface indexes via PowerShell for reliable restore
        EnrichIndexes(enabled);

        var doc = new NetworkStateDocument
        {
            SavedAt = DateTimeOffset.Now,
            Adapters = enabled
        };

        var dir = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(_statePath, JsonSerializer.Serialize(doc, Options));

        foreach (var adapter in enabled)
        {
            DisableAdapter(adapter);
        }

        IsDisabled = true;
    }

    public void EnableInternet()
    {
        RestoreFromFile(_statePath);
        IsDisabled = false;
    }

    public static void RestoreFromFile(string statePath)
    {
        if (!File.Exists(statePath))
            return;

        NetworkStateDocument? doc;
        try
        {
            doc = JsonSerializer.Deserialize<NetworkStateDocument>(File.ReadAllText(statePath), Options);
        }
        catch
        {
            return;
        }

        if (doc?.Adapters is null)
            return;

        foreach (var adapter in doc.Adapters)
        {
            EnableAdapter(adapter);
        }
    }

    private static void EnrichIndexes(List<NetworkAdapterRecord> adapters)
    {
        try
        {
            var output = RunPowerShell(
                "Get-NetAdapter | Select-Object Name, InterfaceIndex, InterfaceGuid | ConvertTo-Json -Compress");
            if (string.IsNullOrWhiteSpace(output))
                return;

            using var doc = JsonDocument.Parse(NormalizeJsonArray(output));
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                var name = element.GetProperty("Name").GetString();
                var index = element.GetProperty("InterfaceIndex").GetInt32();
                var guid = element.TryGetProperty("InterfaceGuid", out var g) ? g.GetString() : null;

                var match = adapters.FirstOrDefault(a =>
                    string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(guid)
                        && string.Equals(a.Id, guid, StringComparison.OrdinalIgnoreCase)));
                if (match is not null)
                    match.InterfaceIndex = index;
            }
        }
        catch
        {
            // Indexes are optional; name-based disable still works.
        }
    }

    private static string NormalizeJsonArray(string output)
    {
        var trimmed = output.Trim();
        if (trimmed.StartsWith('['))
            return trimmed;
        return "[" + trimmed + "]";
    }

    private static void DisableAdapter(NetworkAdapterRecord adapter)
    {
        if (adapter.InterfaceIndex > 0)
        {
            RunPowerShell($"Disable-NetAdapter -InterfaceIndex {adapter.InterfaceIndex} -Confirm:$false");
            return;
        }

        var escaped = adapter.Name.Replace("'", "''");
        RunPowerShell($"Disable-NetAdapter -Name '{escaped}' -Confirm:$false");
    }

    private static void EnableAdapter(NetworkAdapterRecord adapter)
    {
        if (adapter.InterfaceIndex > 0)
        {
            RunPowerShell($"Enable-NetAdapter -InterfaceIndex {adapter.InterfaceIndex} -Confirm:$false");
            return;
        }

        var escaped = adapter.Name.Replace("'", "''");
        RunPowerShell($"Enable-NetAdapter -Name '{escaped}' -Confirm:$false");
    }

    private static string RunPowerShell(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -ExecutionPolicy Bypass -Command " + Quote(command),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null)
            return "";

        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(60_000);
        return stdout;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\"", "\\\"") + "\"";
}
