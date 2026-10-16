using System.Globalization;

namespace Intentionality.Core;

public sealed class LocationInfo
{
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public string? CivicAddress { get; init; }
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }

    public static LocationInfo Unavailable(string reason) => new()
    {
        IsAvailable = false,
        UnavailableReason = reason
    };

    public string ToLogString()
    {
        if (!IsAvailable)
            return $"nicht verfügbar ({UnavailableReason ?? "unbekannt"})";

        var coords = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F5}, {1:F5}",
            Latitude,
            Longitude);
        if (AccuracyMeters is double acc)
            coords += string.Format(CultureInfo.InvariantCulture, " (±{0:F0} m)", acc);

        if (!string.IsNullOrWhiteSpace(CivicAddress))
            return $"{coords}; {CivicAddress}";

        return coords;
    }
}
