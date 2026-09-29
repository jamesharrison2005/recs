namespace Recs.Domain.ValueObjects;

public readonly record struct GeoLocation
{
    private const double EarthRadiusKm = 6371.0;
    private const double KmToMiles = 0.621371192;

    public double Latitude { get; }
    public double Longitude { get; }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude is < -90.0 or > 90.0 || double.IsNaN(latitude) || double.IsInfinity(latitude))
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90 degrees.");
        }

        if (longitude is < -180.0 or > 180.0 || double.IsNaN(longitude) || double.IsInfinity(longitude))
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180 degrees.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Calculates the great-circle distance to another coordinate in kilometers using the Haversine formula.
    /// </summary>
    public double DistanceToKm(GeoLocation other)
    {
        double dLat = DegreesToRadians(other.Latitude - Latitude);
        double dLon = DegreesToRadians(other.Longitude - Longitude);

        double lat1Rad = DegreesToRadians(Latitude);
        double lat2Rad = DegreesToRadians(other.Latitude);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return EarthRadiusKm * c;
    }

    /// <summary>
    /// Calculates the great-circle distance to another coordinate in miles.
    /// </summary>
    public double DistanceToMiles(GeoLocation other) => DistanceToKm(other) * KmToMiles;

    private static double DegreesToRadians(double degrees) => degrees * (Math.PI / 180.0);
}
