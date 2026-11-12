using Recs.Application.Common.Interfaces;
using System.Collections.Generic;

namespace Recs.ML.ColdStart;

/// <summary>
/// Composite cold-start provider: delegates content-based scoring to
/// <see cref="ContentBasedProvider"/> and popularity fallthrough to
/// <see cref="PopularityFallthroughProvider"/>. Registered as the single
/// <see cref="IRecommendationProvider"/> so callers get both strategies
/// without depending on concrete types.
/// </summary>
public sealed class CompositeColdStartProvider : IRecommendationProvider
{
    private readonly PopularityFallthroughProvider _popularity;
    private readonly ContentBasedProvider _contentBased;

    public CompositeColdStartProvider(PopularityFallthroughProvider popularity, ContentBasedProvider contentBased)
    {
        _popularity = popularity;
        _contentBased = contentBased;
    }

    public IReadOnlyList<(string ItemId, float Score)> GetRecommendations(string userId, int maxResults = 10)
        => _popularity.GetPopularityFallback(maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetContentBased(string userId, int maxResults = 10)
        => _contentBased.GetContentBased(userId, maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetPopularityFallback(int maxResults = 10)
        => _popularity.GetPopularityFallback(maxResults);
}
