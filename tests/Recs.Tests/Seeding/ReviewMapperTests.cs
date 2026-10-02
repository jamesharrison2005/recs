using Recs.Domain.Entities;
using Recs.Seeder;
using Recs.Seeder.Yelp;

namespace Recs.Tests.Seeding;

public class ReviewMapperTests
{
    private const string ItemId = "yelp-biz-1";

    private static YelpReview Review(double? stars = 4.0f) => new()
    {
        UserId = "user-1",
        BusinessId = "biz-1",
        Stars = stars,
        Text = "Delicious.",
        Date = "2019-03-15 21:07:26"
    };

    [Fact]
    public void Map_WithUsableReview_ProducesRatingWithDeterministicId()
    {
        var result = ReviewMapper.Map(Review(), ItemId);

        Assert.Null(result.SkipReason);
        var rating = result.Rating!;
        Assert.Equal("yelp-user-1:yelp-biz-1", rating.Id);
        Assert.Equal("yelp-user-1", rating.UserId);
        Assert.Equal(ItemId, rating.ItemId);
        Assert.Equal(4.0f, rating.Score);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(5.5)]
    [InlineData(6.0)]
    [InlineData(null)]
    public void Map_WithStarsOutsideDomainRange_IsSkippedRatherThanThrowing(double? stars)
    {
        var result = ReviewMapper.Map(Review(stars), ItemId);

        Assert.Null(result.Rating);
        Assert.Equal(SeedSkipReason.OutOfRangeStars, result.SkipReason);
    }

    [Theory]
    [InlineData(1.0f)]
    [InlineData(2.5f)]
    [InlineData(3.0f)]
    [InlineData(4.5f)]
    [InlineData(5.0f)]
    public void Map_AcceptsYelpHalfStepStars(float stars)
    {
        var result = ReviewMapper.Map(Review(stars), ItemId);

        Assert.Null(result.SkipReason);
        Assert.Equal(stars, result.Rating!.Score);
    }

    [Theory]
    [InlineData(null, "biz-1")]
    [InlineData("", "biz-1")]
    [InlineData("user-1", null)]
    [InlineData("user-1", "")]
    public void Map_WithMissingUserOrBusiness_IsSkippedAsMalformed(string? userId, string? businessId)
    {
        var review = Review();
        review.UserId = userId;
        review.BusinessId = businessId;

        var result = ReviewMapper.Map(review, ItemId);

        Assert.Null(result.Rating);
        Assert.Equal(SeedSkipReason.Malformed, result.SkipReason);
    }

    [Fact]
    public void Map_TruncatesOverlongReviewTextToColumnLimit()
    {
        var review = Review();
        review.Text = new string('x', 5000);

        var rating = ReviewMapper.Map(review, ItemId).Rating!;

        Assert.Equal(ReviewMapper.ReviewMaxLength, rating.Review!.Length);
    }

    [Fact]
    public void Map_ParsesHumanReadableDate()
    {
        var rating = ReviewMapper.Map(Review(), ItemId).Rating!;

        Assert.Equal(2019, rating.Timestamp.Year);
        Assert.Equal(3, rating.Timestamp.Month);
        Assert.Equal(15, rating.Timestamp.Day);
    }

    [Fact]
    public void Map_FallsBackToTimestampWhenDateIsAbsent()
    {
        var review = Review();
        review.Date = null;
        review.Timestamp = "2018-01-02 03:04:05";

        var rating = ReviewMapper.Map(review, ItemId).Rating!;

        Assert.Equal(2018, rating.Timestamp.Year);
    }

    [Fact]
    public void Map_WithNoTimestampAtAll_StillProducesARating()
    {
        var review = Review();
        review.Date = null;
        review.Timestamp = null;

        var result = ReviewMapper.Map(review, ItemId);

        Assert.Null(result.SkipReason);
        Assert.NotNull(result.Rating);
    }

    [Fact]
    public void ToRatingId_IsDeterministicPerUserAndItem()
    {
        Assert.Equal("yelp-user-1:yelp-biz-1", ReviewMapper.ToRatingId("yelp-user-1", "yelp-biz-1"));
        Assert.NotEqual(
            ReviewMapper.ToRatingId("yelp-user-1", "yelp-biz-1"),
            ReviewMapper.ToRatingId("yelp-user-1", "yelp-biz-2"));
    }
}
