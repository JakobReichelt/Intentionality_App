using Intentionality.Core;
using Windows.Devices.Geolocation;
using Windows.Services.Maps;

namespace Intentionality.Services;

public sealed class LocationService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(8);

    public async Task<LocationInfo> GetLocationAsync(CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(DefaultTimeout);

        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
            {
                return LocationInfo.Unavailable(access switch
                {
                    GeolocationAccessStatus.Denied => "permission denied",
                    GeolocationAccessStatus.Unspecified => "permission unspecified",
                    _ => access.ToString()
                });
            }

            var locator = new Geolocator
            {
                DesiredAccuracy = PositionAccuracy.Default,
                DesiredAccuracyInMeters = 100
            };

            var position = await locator.GetGeopositionAsync()
                .AsTask(timeoutCts.Token);

            var coord = position.Coordinate.Point.Position;
            string? civic = null;

            try
            {
                var result = await MapLocationFinder.FindLocationsAtAsync(position.Coordinate.Point)
                    .AsTask(timeoutCts.Token);
                if (result.Status == MapLocationFinderStatus.Success && result.Locations.Count > 0)
                {
                    var addr = result.Locations[0].Address;
                    var parts = new[] { addr.Street, addr.Town, addr.Region, addr.Country }
                        .Where(p => !string.IsNullOrWhiteSpace(p));
                    civic = string.Join(", ", parts);
                    if (string.IsNullOrWhiteSpace(civic))
                        civic = null;
                }
            }
            catch
            {
                // Civic address is optional.
            }

            return new LocationInfo
            {
                IsAvailable = true,
                Latitude = coord.Latitude,
                Longitude = coord.Longitude,
                AccuracyMeters = position.Coordinate.Accuracy,
                CivicAddress = civic
            };
        }
        catch (OperationCanceledException)
        {
            return LocationInfo.Unavailable("timed out");
        }
        catch (Exception ex)
        {
            return LocationInfo.Unavailable(ex.GetType().Name + ": " + ex.Message);
        }
    }
}
