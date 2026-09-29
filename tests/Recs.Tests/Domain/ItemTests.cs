using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;

namespace Recs.Tests.Domain;

public class ItemTests
{
    private readonly GeoLocation _douglas = new(54.1533, -4.4786);

    [Fact]
    public void CreateRestaurant_WithValidInputs_InitializesCorrectly()
    {
        var item = Item.CreateRestaurant(
            id: "rest-1",
            name: "Little Fish Cafe",
            category: "Seafood",
            priceTier: PriceTier.Moderate,
            location: _douglas,
            tags: new[] { "Seafood", "Casual", "Outdoor-Seating" },
            description: "Fresh local seafood by the quay.");

        Assert.Equal("rest-1", item.Id);
        Assert.Equal("Little Fish Cafe", item.Name);
        Assert.Equal(ItemType.Restaurant, item.Type);
        Assert.Equal("Seafood", item.Category);
        Assert.Equal(PriceTier.Moderate, item.PriceTier);
        Assert.Equal(_douglas, item.Location);
        Assert.Equal(3, item.Tags.Count);
        Assert.Contains("seafood", item.Tags);
        Assert.Contains("casual", item.Tags);
        Assert.Contains("outdoor-seating", item.Tags);
        Assert.Equal("Fresh local seafood by the quay.", item.Description);
        Assert.True(item.IsActive);
        Assert.Null(item.EventStart);
        Assert.Null(item.EventEnd);
    }

    [Fact]
    public void CreateEvent_WithValidInputs_InitializesCorrectly()
    {
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end = start.AddHours(4);

        var item = Item.CreateEvent(
            id: "event-1",
            name: "Isle of Man Food Festival",
            category: "Food & Drink",
            priceTier: PriceTier.Budget,
            location: _douglas,
            eventStart: start,
            eventEnd: end,
            tags: new[] { "Local Produce", "Family" },
            description: "Annual food and drink festival.");

        Assert.Equal("event-1", item.Id);
        Assert.Equal("Isle of Man Food Festival", item.Name);
        Assert.Equal(ItemType.Event, item.Type);
        Assert.Equal("Food & Drink", item.Category);
        Assert.Equal(PriceTier.Budget, item.PriceTier);
        Assert.Equal(_douglas, item.Location);
        Assert.Equal(start, item.EventStart);
        Assert.Equal(end, item.EventEnd);
        Assert.True(item.IsActive);
        Assert.Contains("local produce", item.Tags);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidId_ThrowsArgumentException(string? invalidId)
    {
        Assert.Throws<ArgumentException>(() => new Item(
            id: invalidId!,
            name: "Test",
            type: ItemType.Restaurant,
            category: "Food",
            priceTier: PriceTier.Moderate,
            location: _douglas));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidName_ThrowsArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => new Item(
            id: "item-1",
            name: invalidName!,
            type: ItemType.Restaurant,
            category: "Food",
            priceTier: PriceTier.Moderate,
            location: _douglas));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidCategory_ThrowsArgumentException(string? invalidCategory)
    {
        Assert.Throws<ArgumentException>(() => new Item(
            id: "item-1",
            name: "Test",
            type: ItemType.Restaurant,
            category: invalidCategory!,
            priceTier: PriceTier.Moderate,
            location: _douglas));
    }

    [Fact]
    public void Constructor_WithEventEndBeforeEventStart_ThrowsArgumentException()
    {
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var end = start.AddHours(-1);

        Assert.Throws<ArgumentException>(() => Item.CreateEvent(
            id: "event-bad",
            name: "Invalid Event",
            category: "Music",
            priceTier: PriceTier.Free,
            location: _douglas,
            eventStart: start,
            eventEnd: end));
    }

    [Fact]
    public void Tags_AreNormalizedAndCaseInsensitive()
    {
        var item = Item.CreateRestaurant(
            id: "r1",
            name: "Bar",
            category: "Pub",
            priceTier: PriceTier.Moderate,
            location: _douglas,
            tags: new[] { "Cocktails", "cocktails", " LIVE MUSIC ", "   " });

        Assert.Equal(2, item.Tags.Count);
        Assert.Contains("cocktails", item.Tags);
        Assert.Contains("live music", item.Tags);
    }

    [Fact]
    public void MatchesTags_WithMatchingTag_ReturnsTrue()
    {
        var item = Item.CreateRestaurant(
            id: "r1",
            name: "Cafe",
            category: "Coffee",
            priceTier: PriceTier.Budget,
            location: _douglas,
            tags: new[] { "wifi", "vegan" });

        Assert.True(item.MatchesTags(new[] { "VEGAN", "outdoor" }));
        Assert.False(item.MatchesTags(new[] { "outdoor", "parking" }));
    }

    [Fact]
    public void DistanceToKm_DelegatesToGeoLocation()
    {
        var item = Item.CreateRestaurant(
            id: "r1",
            name: "Douglas Place",
            category: "Dining",
            priceTier: PriceTier.Moderate,
            location: new GeoLocation(54.1533, -4.4786));

        var peel = new GeoLocation(54.2259, -4.7001);

        double dist = item.DistanceToKm(peel);
        Assert.InRange(dist, 16.0, 17.0);
    }

    [Fact]
    public void UpdateStatus_ChangesIsActive()
    {
        var item = Item.CreateRestaurant(
            id: "r1",
            name: "Place",
            category: "Dining",
            priceTier: PriceTier.Moderate,
            location: _douglas);

        item.UpdateStatus(false);
        Assert.False(item.IsActive);
    }
}
