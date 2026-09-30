using System.Globalization;
using Recs.Domain.Entities;

namespace Recs.Seeder.Yelp;

/// <summary>
/// Outcome of mapping one Yelp review line: either a <see cref="Rating"/> or the reason it was
/// dropped.
/// </summary>
public readonly record struct ReviewMapResult(Rating? Rating, SeedSkipReason? SkipReason)
{
    public static ReviewMapResult Success(Rating rating) => new(rating, null);

    public static ReviewMapResult Skipped(SeedSkipReason reason) => new(null, reason);
}

/// <summary>
/// Maps a Yelp review to a domain <see cref="Rating"/>.
/// </summary>
public static class ReviewMapper
{
    /// <summary>Matches the <c>Review</c> column's max length.</summary>
    public const int ReviewMaxLength = 4000;

    /// <summary>
    /// Builds the deterministic id for a rating from the user and item it pairs. Yelp allows a
    /// user to review a business more than once, and the database enforces one rating per
    /// (user, item) pair, so this key is also what makes a second run a no-op.
    /// </summary>
    public static string ToRatingId(string userId, string itemId) => $"{userId}:{itemId}";

    /// <summary>Builds the deterministic id for a user.</summary>
    public static string ToUserId(string yelpUserId) => $"yelp-{yelpUserId}";

    /// <summary>
    /// Maps a review to a <see cref="Rating"/>, or reports why it could not be.
    /// </summary>
    public static ReviewMapResult Map(YelpReview review, string itemId)
    {
        if (string.IsNullOrWhiteSpace(review.UserId) || string.IsNullOrWhiteSpace(review.BusinessId))
            return ReviewMapResult.Skipped(SeedSkipReason.Malformed);

        // The Rating constructor rejects anything outside this range, so an unfiltered zero or
        // null would throw and abort the run rather than skipping one row.
        if (review.Stars is null || review.Stars < Rating.MinScore || review.Stars > Rating.MaxScore)
            return ReviewMapResult.Skipped(SeedSkipReason.OutOfRangeStars);

        Rating rating;
        try
        {
            rating = new Rating(
                id: ToRatingId(ToUserId(review.UserId.Trim()), itemId),
                userId: ToUserId(review.UserId.Trim()),
                itemId: itemId,
                score: (float)review.Stars.Value,
                review: Truncate(review.Text, ReviewMaxLength),
                timestamp: ParseTimestamp(review.Date) ?? ParseTimestamp(review.Timestamp));
        }
        catch (ArgumentException)
        {
            // The entity id exceeded the column's max length, or a required part was blank.
            return ReviewMapResult.Skipped(SeedSkipReason.Malformed);
        }

        return ReviewMapResult.Success(rating);
    }

    /// <summary>
    /// Parses a Yelp timestamp. The dataset carries both a human-readable <c>date</c>
    /// ("2019-03-15 21:07:26") and an ISO <c>timestamp</c>; either may be absent, in which case
    /// the domain stamps the current time.
    /// </summary>
    public static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
