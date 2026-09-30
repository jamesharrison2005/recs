using Recs.Domain.Enums;
using Recs.Seeder;
using Recs.Seeder.Yelp;

namespace Recs.Tests.Seeding;

public class BusinessMapperTests
{
    private static YelpBusiness Business(
        string? categories = "Restaurants, Italian, Pizza",
        double? latitude = 36.13,
        double? longitude = -86.77) => new()
    {
        BusinessId = "biz-1",
        Name = "A Restaurant",
        City = "Nashville",
        State = "TN",
        Latitude = latitude,
        Longitude = longitude,
        IsOpen = 1,
        Categories = categories
    };

    [Theory]
    [InlineData("Restaurants, Italian, Pizza", "Pizza")]
    [InlineData("Restaurants, Southern, Chicken Wings", "Chicken Wings")]
    [InlineData("Restaurants", "Restaurants")]
    [InlineData("Restaurants, Fine Dining", "Fine Dining")]
    [InlineData(null, "Restaurants")]
    [InlineData("", "Restaurants")]
    public void ResolveCategory_PicksMostSpecificCategory(string? categories, string expected)
    {
        Assert.Equal(expected, BusinessMapper.ResolveCategory(categories));
    }

    [Theory]
    [InlineData("$", PriceTier.Budget)]
    [InlineData("$$", PriceTier.Moderate)]
    [InlineData("$$$", PriceTier.Expensive)]
    [InlineData("$$$$", PriceTier.Luxury)]
    [InlineData(null, PriceTier.Free)]
    [InlineData("", PriceTier.Free)]
    [InlineData("   ", PriceTier.Free)]
    [InlineData("$$$$$", PriceTier.Free)]
    public void MapPriceTier_MapsYelpPriceString(string? price, PriceTier expected)
    {
        Assert.Equal(expected, BusinessMapper.MapPriceTier(price));
    }

    [Theory]
    [InlineData("Restaurants, Italian", true)]
    [InlineData("restaurants, italian", true)]
    [InlineData("Shopping, Clothing", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRestaurant_RequiresRestaurantsCategory(string? categories, bool expected)
    {
        Assert.Equal(expected, BusinessMapper.IsRestaurant(Business(categories)));
    }

    [Fact]
    public void MatchesLocation_IgnoresCaseAndSurroundingSpace()
    {
        var business = Business();
        business.City = "  nashville ";

        Assert.True(BusinessMapper.MatchesLocation(business, "Nashville", null));
        Assert.True(BusinessMapper.MatchesLocation(business, "Nashville", "tn"));
        Assert.False(BusinessMapper.MatchesLocation(business, "Nashville", "PA"));
        Assert.False(BusinessMapper.MatchesLocation(business, "Philadelphia", null));
    }

    [Fact]
    public void Map_WithUsableCoordinates_ProducesRestaurantItem()
    {
        var result = BusinessMapper.Map(Business());

        Assert.Null(result.SkipReason);
        var item = Assert.IsType<Recs.Domain.Entities.Item>(result.Item);
        Assert.Equal("yelp-biz-1", item.Id);
        Assert.Equal(ItemType.Restaurant, item.Type);
        Assert.Equal("Pizza", item.Category);
        Assert.Equal(36.13, item.Location.Latitude, 6);
        Assert.Equal(-86.77, item.Location.Longitude, 6);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Map_ExposesEveryCategoryAsATag()
    {
        var result = BusinessMapper.Map(Business("Restaurants, Italian, Pizza"));

        var tags = result.Item!.Tags;
        Assert.Equal(3, tags.Count);
        Assert.Contains("restaurants", tags);
        Assert.Contains("italian", tags);
        Assert.Contains("pizza", tags);
    }

    [Fact]
    public void Map_WithNullCoordinates_IsSkippedRatherThanDefaultingToZero()
    {
        var result = BusinessMapper.Map(Business(latitude: null, longitude: null));

        Assert.Null(result.Item);
        Assert.Equal(SeedSkipReason.MissingCoordinates, result.SkipReason);
    }

    [Theory]
    [InlineData(145.0, -86.77)]
    [InlineData(36.13, 200.0)]
    [InlineData(double.NaN, -86.77)]
    public void Map_WithOutOfRangeCoordinates_IsSkipped(double latitude, double longitude)
    {
        var result = BusinessMapper.Map(Business(latitude: latitude, longitude: longitude));

        Assert.Null(result.Item);
        Assert.Equal(SeedSkipReason.InvalidCoordinates, result.SkipReason);
    }

    [Fact]
    public void Map_WithClosedBusiness_IsInactive()
    {
        var business = Business();
        business.IsOpen = 0;

        Assert.False(BusinessMapper.Map(business).Item!.IsActive);
    }

    [Fact]
    public void Map_WithUnknownOpenState_IsTreatedAsActive()
    {
        var business = Business();
        business.IsOpen = null;

        Assert.True(BusinessMapper.Map(business).Item!.IsActive);
    }

    [Fact]
    public void Map_TruncatesOverlongNameToColumnLimit()
    {
        var business = Business();
        business.Name = new string('x', 400);

        var item = BusinessMapper.Map(business).Item!;

        Assert.Equal(256, item.Name.Length);
    }

    [Fact]
    public void ToItemId_IsDeterministic()
    {
        Assert.Equal("yelp-biz-1", BusinessMapper.ToItemId("biz-1"));
        Assert.Equal(BusinessMapper.ToItemId("biz-1"), BusinessMapper.ToItemId("biz-1"));
    }
}
