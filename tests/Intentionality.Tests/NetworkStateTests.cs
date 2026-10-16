using System.IO;
using System.Text.Json;
using Intentionality.Services;

namespace Intentionality.Tests;

public class NetworkStateTests
{
    [Fact]
    public void NetworkStateDocument_RoundTripsThroughJson()
    {
        var path = Path.Combine(Path.GetTempPath(), "network-state-" + Guid.NewGuid() + ".json");
        try
        {
            var doc = new NetworkStateDocument
            {
                SavedAt = DateTimeOffset.Parse("2026-10-15T12:00:00+02:00"),
                Adapters =
                [
                    new NetworkAdapterRecord { Name = "Ethernet", Id = "{abc}", InterfaceIndex = 12 },
                    new NetworkAdapterRecord { Name = "Wi-Fi", Id = "{def}", InterfaceIndex = 15 }
                ]
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            File.WriteAllText(path, JsonSerializer.Serialize(doc, options));

            var loaded = JsonSerializer.Deserialize<NetworkStateDocument>(File.ReadAllText(path), options);
            Assert.NotNull(loaded);
            Assert.Equal(2, loaded!.Adapters.Count);
            Assert.Equal("Ethernet", loaded.Adapters[0].Name);
            Assert.Equal(12, loaded.Adapters[0].InterfaceIndex);
            Assert.Equal("Wi-Fi", loaded.Adapters[1].Name);

            // RestoreFromFile with missing adapters should not throw
            NetworkService.RestoreFromFile(path + ".missing");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
