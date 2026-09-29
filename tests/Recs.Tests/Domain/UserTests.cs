using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;

namespace Recs.Tests.Domain;

public class UserTests
{
    private readonly GeoLocation _douglas = new(54.1533, -4.4786);

    [Fact]
    public void Constructor_WithMinimalInputs_InitializesWithDefaults()
    {
        var user = new User("user-1", "alice");

        Assert.Equal("user-1", user.Id);
        Assert.Equal("alice", user.Username);
        Assert.Null(user.HomeLocation);
        Assert.NotNull(user.Preferences);
        Assert.Empty(user.Preferences.PreferredCategories);
        Assert.Empty(user.Preferences.PreferredPriceTiers);
        Assert.Empty(user.Preferences.PreferredTags);
        Assert.Null(user.Preferences.MaxTravelDistanceKm);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidId_ThrowsArgumentException(string? invalidId)
    {
        Assert.Throws<ArgumentException>(() => new User(invalidId!, "alice"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithInvalidUsername_ThrowsArgumentException(string? invalidUsername)
    {
        Assert.Throws<ArgumentException>(() => new User("user-1", invalidUsername!));
    }

    [Fact]
    public void UpdatePreferences_SetsNewPreferences()
    {
        var user = new User("user-1", "alice");
        var newPrefs = new UserPreferences(
            preferredCategories: new[] { "Italian", "Pub" },
            preferredPriceTiers: new[] { PriceTier.Moderate, PriceTier.Budget },
            preferredTags: new[] { "outdoor-seating" },
            maxTravelDistanceKm: 25.0);

        user.UpdatePreferences(newPrefs);

        Assert.Equal(2, user.Preferences.PreferredCategories.Count);
        Assert.Contains("Italian", user.Preferences.PreferredCategories);
        Assert.Contains(PriceTier.Moderate, user.Preferences.PreferredPriceTiers);
        Assert.Contains("outdoor-seating", user.Preferences.PreferredTags);
        Assert.Equal(25.0, user.Preferences.MaxTravelDistanceKm);
    }

    [Fact]
    public void UpdatePreferences_WithNull_ThrowsArgumentNullException()
    {
        var user = new User("user-1", "alice");
        Assert.Throws<ArgumentNullException>(() => user.UpdatePreferences(null!));
    }

    [Fact]
    public void UpdateLocation_UpdatesAndClearsLocation()
    {
        var user = new User("user-1", "alice");
        Assert.Null(user.HomeLocation);

        user.UpdateLocation(_douglas);
        Assert.Equal(_douglas, user.HomeLocation);

        user.UpdateLocation(null);
        Assert.Null(user.HomeLocation);
    }

    [Fact]
    public void UserPreferences_WithNegativeDistance_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new UserPreferences(maxTravelDistanceKm: -5.0));
    }
}
