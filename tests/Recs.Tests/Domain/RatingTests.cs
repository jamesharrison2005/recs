using Recs.Domain.Entities;

namespace Recs.Tests.Domain;

public class RatingTests
{
    [Fact]
    public void Constructor_WithValidInputs_InitializesCorrectly()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-10);
        var rating = new Rating(
            id: "rate-1",
            userId: "user-1",
            itemId: "item-1",
            score: 4.5f,
            review: "Great atmosphere and friendly staff!",
            timestamp: timestamp);

        Assert.Equal("rate-1", rating.Id);
        Assert.Equal("user-1", rating.UserId);
        Assert.Equal("item-1", rating.ItemId);
        Assert.Equal(4.5f, rating.Score);
        Assert.Equal("Great atmosphere and friendly staff!", rating.Review);
        Assert.Equal(timestamp, rating.Timestamp);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(2.5f)]
    [InlineData(5.0f)]
    public void Constructor_WithValidScoreBoundary_Succeeds(float score)
    {
        var rating = new Rating("r1", "u1", "i1", score);
        Assert.Equal(score, rating.Score);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(0.0f)]
    [InlineData(-1.0f)]
    [InlineData(5.1f)]
    [InlineData(10.0f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Constructor_WithOutOfRangeScore_ThrowsArgumentOutOfRangeException(float score)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rating("r1", "u1", "i1", score));
    }

    [Theory]
    [InlineData(null, "u1", "i1")]
    [InlineData("", "u1", "i1")]
    [InlineData("   ", "u1", "i1")]
    public void Constructor_WithInvalidId_ThrowsArgumentException(string? invalidId, string userId, string itemId)
    {
        Assert.Throws<ArgumentException>(() => new Rating(invalidId!, userId, itemId, 4.0f));
    }

    [Theory]
    [InlineData("r1", null, "i1")]
    [InlineData("r1", "", "i1")]
    [InlineData("r1", "   ", "i1")]
    public void Constructor_WithInvalidUserId_ThrowsArgumentException(string id, string? invalidUserId, string itemId)
    {
        Assert.Throws<ArgumentException>(() => new Rating(id, invalidUserId!, itemId, 4.0f));
    }

    [Theory]
    [InlineData("r1", "u1", null)]
    [InlineData("r1", "u1", "")]
    [InlineData("r1", "u1", "   ")]
    public void Constructor_WithInvalidItemId_ThrowsArgumentException(string id, string userId, string? invalidItemId)
    {
        Assert.Throws<ArgumentException>(() => new Rating(id, userId, invalidItemId!, 4.0f));
    }
}
