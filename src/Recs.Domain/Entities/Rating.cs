namespace Recs.Domain.Entities;

public class Rating
{
    public const float MinScore = 1.0f;
    public const float MaxScore = 5.0f;

    public string Id { get; private set; }
    public string UserId { get; private set; }
    public string ItemId { get; private set; }
    public float Score { get; private set; }
    public string? Review { get; private set; }
    public DateTimeOffset Timestamp { get; private set; }

    /// <summary>
    /// Used by EF Core to materialise a <see cref="Rating"/> from the database,
    /// bypassing the validating constructor. Not for application use.
    /// </summary>
    private Rating()
    {
    }

    public Rating(
        string id,
        string userId,
        string itemId,
        float score,
        string? review = null,
        DateTimeOffset? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Rating ID cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User ID cannot be null or whitespace.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(itemId));
        }

        if (float.IsNaN(score) || float.IsInfinity(score) || score is < MinScore or > MaxScore)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, $"Rating score must be between {MinScore} and {MaxScore}.");
        }

        Id = id.Trim();
        UserId = userId.Trim();
        ItemId = itemId.Trim();
        Score = score;
        Review = review?.Trim();
        Timestamp = timestamp ?? DateTimeOffset.UtcNow;
    }
}
