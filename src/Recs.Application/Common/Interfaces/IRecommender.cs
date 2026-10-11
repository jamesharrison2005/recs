using Recs.Application.Models;

namespace Recs.Application.Common.Interfaces;

public interface IRecommender
{
    Task<IReadOnlyList<RecommendationResult>> RecommendAsync(
        string userId, int count = 10, ItemFilter? filter = null, CancellationToken ct = default);
}
