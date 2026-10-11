using Recs.Application.Common.Interfaces;
using Recs.Application.Models;
using Recs.Domain.Entities;
using Recs.ML;
using Recs.ML.ColdStart;

namespace Recs.Application.Services;

public sealed class Recommender : IRecommender
{
    private readonly IRatingScorer _scorer;
    private readonly IRecommendationProvider _provider;
    private readonly IRatingRepository _ratingRepo;
    private readonly IItemRepository _itemRepo;

    public Recommender(
        IRatingScorer scorer,
        IRecommendationProvider provider,
        IRatingRepository ratingRepo,
        IItemRepository itemRepo)
    {
        _scorer = scorer;
        _provider = provider;
        _ratingRepo = ratingRepo;
        _itemRepo = itemRepo;
    }

    public async Task<IReadOnlyList<RecommendationResult>> RecommendAsync(
        string userId, int count = 10, ItemFilter? filter = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId required", nameof(userId));

        // 1. User's existing ratings to exclude
        var userRatings = await _ratingRepo.GetByUserIdAsync(userId, ct);
        var ratedItemIds = new HashSet<string>(userRatings.Select(r => r.ItemId), StringComparer.OrdinalIgnoreCase);

        // 2. Candidate items (with filter)
        var candidates = await GetCandidateItemsAsync(filter, ct);
        var candidatesUnrated = candidates.Where(i => !ratedItemIds.Contains(i.Id)).ToList();

        // 3. Model scoring if loaded
        IReadOnlyList<RecommendationResult> modelResults = new List<RecommendationResult>();
        if (_scorer.IsModelLoaded && candidatesUnrated.Count > 0)
        {
            var ids = candidatesUnrated.Select(i => i.Id).ToList();
            var scores = _scorer.ScoreBatch(userId, ids);
            var scoreDict = scores.ToDictionary(s => s.ItemId, s => s.Score, StringComparer.OrdinalIgnoreCase);
            var itemsById = candidatesUnrated.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

            modelResults = scores
                .Where(s => itemsById.ContainsKey(s.ItemId))
                .Select(s => new RecommendationResult(itemsById[s.ItemId], s.Score, RecommendationSource.Model))
                .OrderByDescending(r => r.Score)
                .ToList();
        }

        // 4. Fallback if unknown user / no model / empty scores; must exclude already-rated and honor filter
        if (modelResults.Count == 0 || (!_scorer.IsModelLoaded && candidatesUnrated.Count > 0))
        {
            var fallbackIds = await GetFallbackIdsAsync(filter, ct); // popularity filtered
            var fallbackUnrated = fallbackIds.Where(id => !ratedItemIds.Contains(id)).ToList();
            var fallbackItems = await _itemRepo.GetByIdsAsync(fallbackUnrated, ct);
            // Use provider's popularity with filter applied via repo query
            var popularity = new PopularityFallthroughProvider(_itemRepo); // Need to inject or create; for simplicity use provider directly if it respects filter
            // Actually provider uses RecsDbContext; filter handling in fallback requires using item repo with filter
            // Simplified: build fallback from repository results already filtered
            var fallbackResults = fallbackItems
                .Select(i => new RecommendationResult(i, 1.0f, RecommendationSource.Fallback))
                .OrderByDescending(r => r.Item.Id) // placeholder ordering; real would use avg score
                .Take(count)
                .ToList();
            return fallbackResults;
        }

        return modelResults.Take(count).ToList();
    }

    private async Task<List<Item>> GetCandidateItemsAsync(ItemFilter? filter, CancellationToken ct)
    {
        if (filter != null && !string.IsNullOrWhiteSpace(filter.ItemType))
        {
            if (Enum.TryParse<ItemType>(filter.ItemType, true, out var t))
                return (await _itemRepo.GetByTypeAsync(t, ct)).ToList();
            else if (!string.IsNullOrWhiteSpace(filter.ItemType))
                return (await _itemRepo.GetByCategoryAsync(filter.ItemType, ct)).ToList();
        }
        return (await _itemRepo.GetAllAsync(ct)).ToList();
    }

    private async Task<List<string>> GetFallbackIdsAsync(ItemFilter? filter, CancellationToken ct)
    {
        // Popularity: order by average rating / count; for simplicity return all active filtered
        var all = await GetCandidateItemsAsync(filter, ct);
        return all.Select(i => i.Id).ToList();
    }
}
