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
    private readonly IRatingScorer _scorer;
    private readonly IUserRepository _userRepo;

    public PredictionService(
        IItemRepository itemRepo,
        IRatingRepository ratingRepo,
        IRecommendationProvider provider,
        IRatingScorer scorer,
        IUserRepository userRepo)
    {
        _itemRepo = itemRepo;
        _ratingRepo = ratingRepo;
        _provider = provider;
        _scorer = scorer;
        _userRepo = userRepo;
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

        var candidateIds = new HashSet<string>(candidates.Select(i => i.Id), StringComparer.OrdinalIgnoreCase);

        // Decide scoring strategy:
        // 1. User has ratings + model loaded -> collaborative (Matrix Factorization) batch scores.
        // 2. Cold start (no ratings / no model / zero scored rows): content-based if the user
        //    has preferences, otherwise popularity fallthrough.
        if (rated.Count > 0 && _scorer.IsModelLoaded)
        {
            var scores = _scorer.ScoreBatch(userId, candidates.Select(i => i.Id));
            var ranked = scores
                .Where(s => candidateIds.Contains(s.ItemId))
                .OrderByDescending(s => s.Score)
                .Take(n)
                .ToList();

            if (ranked.Count > 0)
                return await OrderByIdsAsync(ranked.Select(r => r.ItemId));
        }

        // Cold-start path
        var user = await _userRepo.GetByIdAsync(userId);
        var prefs = user?.Preferences;
        var hasPrefs = prefs != null &&
            ((prefs.PreferredCategories?.Any() ?? false) || (prefs.PreferredTags?.Any() ?? false));

        IReadOnlyList<(string ItemId, float Score)> fallback;
        if (hasPrefs)
        {
            fallback = _provider.GetContentBased(userId, n * 5)
                .Where(p => candidateIds.Contains(p.ItemId))
                .OrderByDescending(p => p.Score)
                .ToList();
            if (fallback.Count < n)
            {
                var popularity = _provider.GetPopularityFallback(n * 5)
                    .Where(p => candidateIds.Contains(p.ItemId) && !fallback.Any(f => f.ItemId.Equals(p.ItemId, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(p => p.Score);
                fallback = fallback.Concat(popularity).ToList();
            }
        }
        else
        {
            fallback = _provider.GetPopularityFallback(n * 5)
                .Where(p => candidateIds.Contains(p.ItemId))
                .OrderByDescending(p => p.Score)
                .ToList();
        }

        return await OrderByIdsAsync(fallback.Select(p => p.ItemId).Distinct(StringComparer.OrdinalIgnoreCase).Take(n));
    }

    private async Task<IReadOnlyList<Item>> OrderByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return new List<Item>();

        var items = await _itemRepo.GetByIdsAsync(idList);
        return idList
            .Select(id => items.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase)))
            .Where(i => i != null)
            .Cast<Item>()
            .ToList();
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
