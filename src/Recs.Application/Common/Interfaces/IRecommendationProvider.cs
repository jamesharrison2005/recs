using Recs.Domain.Entities;

namespace Recs.Application.Common.Interfaces;

/// <summary>
/// Recommendation provider that can switch between collaborative, content-based,
/// and popularity fallthrough strategies based on available data.
/// Cold-start extension point — not wired to Pipeline/RecommendationService yet.
/// </summary>
public interface IRecommendationProvider
{
    /// <summary>
    /// Get recommendations for a user, selecting strategy based on rating density.
    /// </summary>
    /// <param name="userId">Target user ID.</param>
    /// <param name="maxResults">Maximum recommendations to return.</param>
    /// <returns>Ordered list of (ItemId, Score) pairs.</returns>
    IReadOnlyList<(string ItemId, float Score)> GetRecommendations(string userId, int maxResults = 10);

    /// <summary>
    /// Gets recommendations using only collaborative filtering (matrix factorization).
    /// Requires the user to have sufficient ratings (typically >= MinRatingsPerUser).
    /// </summary>
    IReadOnlyList<(string ItemId, float Score)> GetCollaborative(string userId, int maxResults = 10);

    /// <summary>
    /// Gets recommendations using content-based similarity.
    /// Uses item features (Category, Tags, PriceTier, Location) and user preferences
    /// (PreferredCategories, PreferredPriceTiers, PreferredTags, MaxTravelDistanceKm).
    /// Suitable for new users or items with few interactions.
    /// </summary>
    IReadOnlyList<(string ItemId, float Score)> GetContentBased(string userId, int maxResults = 10);

    /// <summary>
    /// Gets most popular items (highest average rating, most reviews) as a fallback.
    /// Always works regardless of user history — base layer for cold-start.
    /// </summary>
    IReadOnlyList<(string ItemId, float Score)> GetPopularityFallback(int maxResults = 10);
}