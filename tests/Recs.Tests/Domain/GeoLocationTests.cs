using Recs.Domain.ValueObjects;

namespace Recs.Tests.Domain;

public class GeoLocationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(54.1532, -4.4815)] // Douglas, Isle of Man
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void Constructor_WithValidCoordinates_CreatesInstance(double latitude, double longitude)
    {
        var location = new GeoLocation(latitude, longitude);

        Assert.Equal(latitude, location.Latitude);
        Assert.Equal(longitude, location.Longitude);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(double.NaN, 0)]
    [InlineData(double.PositiveInfinity, 0)]
    public void Constructor_WithInvalidLatitude_ThrowsArgumentOutOfRangeException(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(latitude, longitude));
    }

    [Theory]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    [InlineData(0, double.NaN)]
    [InlineData(0, double.NegativeInfinity)]
    public void Constructor_WithInvalidLongitude_ThrowsArgumentOutOfRangeException(double latitude, double longitude)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GeoLocation(latitude, longitude));
    }

    [Fact]
    public void DistanceToKm_BetweenDouglasAndPeel_CalculatesAccurateDistance()
    {
        // Douglas Promenade (54.1533, -4.4786) to Peel Castle (54.2259, -4.7001)
        var douglas = new GeoLocation(54.1533, -4.4786);
        var peel = new GeoLocation(54.2259, -4.7001);

        double distanceKm = douglas.DistanceToKm(peel);

        // Great circle distance is ~16.5 km
        Assert.InRange(distanceKm, 16.0, 17.0);
    }

    [Fact]
    public void DistanceToKm_SamePoint_ReturnsZero()
    {
        var point = new GeoLocation(54.1533, -4.4786);

        double distance = point.DistanceToKm(point);

        Assert.Equal(0, distance, precision: 5);
    }

    [Fact]
    public void DistanceToMiles_CalculatesAccurateConversion()
    {
        var douglas = new GeoLocation(54.1533, -4.4786);
        var peel = new GeoLocation(54.2259, -4.7001);

        double distanceKm = douglas.DistanceToKm(peel);
        double distanceMiles = douglas.DistanceToMiles(peel);

        Assert.Equal(distanceKm * 0.621371192, distanceMiles, precision: 4);
    }

    [Fact]
    public void ValueEquality_IdenticalCoordinates_AreEqual()
    {
        var loc1 = new GeoLocation(54.15, -4.48);
        var loc2 = new GeoLocation(54.15, -4.48);

        Assert.Equal(loc1, loc2);
    }
}
