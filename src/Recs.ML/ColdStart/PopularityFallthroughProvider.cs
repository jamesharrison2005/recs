using Recs.Application.Common.Interfaces;
using Recs.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace Recs.ML.ColdStart;

/// <summary>
/// Popularity fallthrough: always works regardless of user/item interaction count.
/// Ranks by average rating score and review count — base layer for cold-start users/items.
/// </summary>
public sealed class PopularityFallthroughProvider : IRecommendationProvider
{
    private readonly RecsDbContext _db;

    public PopularityFallthroughProvider(RecsDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<(string ItemId, float Score)> GetRecommendations(string userId, int maxResults = 10)
        => GetPopularityFallback(maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetCollaborative(string userId, int maxResults = 10)
        => GetPopularityFallback(maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetContentBased(string userId, int maxResults = 10)
        => GetPopularityFallback(maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetPopularityFallback(int maxResults = 10)
    {
        // Aggregate ratings per item: average score * log(review count) for popularity weighting
        var popular = _db.Ratings
            .GroupBy(r => r.ItemId)
            .Select(g => new
            {
                ItemId = g.Key,
                AvgScore = g.Average(r => r.Score),
                ReviewCount = g.Count()
            })
            .OrderByDescending(x => x.AvgScore * System.Math.Log(1 + x.ReviewCount))
            .Take(maxResults)
            .Select(x => new { x.ItemId, Score = (float)x.AvgScore })
            .ToList()
            .Select(x => (x.ItemId, x.Score))
            .ToList();

        return popular;
    }
}