using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Recs.Application.Common.Interfaces;
using Recs.Application.Models;
using Recs.Domain.Entities;

namespace Recs.Application.Services;

public sealed class PredictionService : IPredictionService
{
    private readonly IItemRepository _itemRepo;
    private readonly IRatingRepository _ratingRepo;
    private readonly IRecommendationProvider _provider;

    public PredictionService(
        IItemRepository itemRepo,
        IRatingRepository ratingRepo,
        IRecommendationProvider provider)
    {
        _itemRepo = itemRepo;
        _ratingRepo = ratingRepo;
        _provider = provider;
    }

    public async Task<IReadOnlyList<Item>> PredictAsync(string userId, int n = 10, ItemFilter? filters = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (n <= 0) n = 10;

        // 2. Load context: exclude already-rated
        var rated = await _ratingRepo.GetByUserIdAsync(userId);
        var seenIds = new HashSet<string>(rated.Select(r => r.ItemId), StringComparer.OrdinalIgnoreCase);

        // 3. Candidate generation: all active items, apply filter, exclude seen (~2000 max)
        var allItems = await _itemRepo.GetAllAsync(activeOnly: true);
        var candidates = allItems
            .Where(i => !seenIds.Contains(i.Id))
            .Where(i => filters == null || string.IsNullOrWhiteSpace(filters.ItemType) || MatchesType(i, filters.ItemType))
            .ToList();

        if (candidates.Count == 0)
            return new List<Item>();

        // 6. Unknown user -> popular items (respecting filter)
        if (rated.Count == 0)
        {
            var popularIds = _provider.GetPopularityFallback(n * 5)
                .Select(p => p.ItemId)
                .Distinct()
                .ToList();
            var popularItems = await _itemRepo.GetByIdsAsync(popularIds);
            return popularItems
                .Where(i => MatchesType(i, filters?.ItemType))
                .Take(n)
                .ToList();
        }

        // 4/5. Score all candidates via collaborative filtering (matrix factorization)
        // Delegate to provider for efficient batch scoring of user-candidate pairs.
        var scores = _provider.GetCollaborative(userId, candidates.Count);

        // Drop NaN, clamp 1.0-5.0, sort descending
        var ranked = scores
            .Where(s => !float.IsNaN(s.Score))
            .Select(s => new { s.ItemId, Score = Math.Clamp(s.Score, 1.0f, 5.0f) })
            .OrderByDescending(s => s.Score)
            .Take(n)
            .ToList();

        // 6. Return item details
        var ids = ranked.Select(r => r.ItemId).ToList();
        var resultItems = await _itemRepo.GetByIdsAsync(ids);
        var ordered = ids
            .Select(id => resultItems.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i != null)
            .Cast<Item>()
            .ToList();

        return ordered;
    }

    private static bool MatchesType(Item item, string? itemType)
    {
        if (string.IsNullOrWhiteSpace(itemType)) return true;
        // Match by enum name or category string
        var typeName = item.Type.ToString();
        return typeName.Equals(itemType, StringComparison.OrdinalIgnoreCase)
            || (item.Category?.Equals(itemType, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
