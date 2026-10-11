using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;
using Recs.Infrastructure.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace Recs.ML.ColdStart;

/// <summary>
/// Content-based recommendations using item features + user preferences.
/// Designed for cold-start: new users with preferences, or new items with metadata.
/// Uses item Category, Tags, PriceTier, Location and matches against UserPreferences.
/// </summary>
public sealed class ContentBasedProvider : IRecommendationProvider
{
    private readonly RecsDbContext _db;

    public ContentBasedProvider(RecsDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<(string ItemId, float Score)> GetRecommendations(string userId, int maxResults = 10)
        => GetContentBased(userId, maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetCollaborative(string userId, int maxResults = 10)
        => new List<(string, float)>();

    public IReadOnlyList<(string ItemId, float Score)> GetContentBased(string userId, int maxResults = 10)
    {
        var user = _db.Users.FirstOrDefault(u => u.Id == userId);
        if (user == null) return new List<(string, float)>();

        var items = _db.Items.Where(i => i.IsActive).ToList();
        var prefs = user.Preferences;

        var scored = items.Select(item => new
        {
            ItemId = item.Id,
            Score = ComputeMatchScore(item, prefs, user.HomeLocation)
        })
        .OrderByDescending(x => x.Score)
        .Take(maxResults)
        .Select(x => (x.ItemId, x.Score))
        .ToList();

        return scored;
    }

    /// <summary>
    /// Compute similarity score between item and user preferences.
    /// Factors: category match (+1), tag overlap (+0.5 per tag), price tier match (+0.3),
    /// location proximity (distance-based, max +0.5), preference alignment.
    /// </summary>
    private float ComputeMatchScore(Item item, Domain.ValueObjects.UserPreferences prefs, Domain.ValueObjects.GeoLocation? homeLocation)
    {
        float score = 0f;

        // Category match
        if (prefs.PreferredCategories != null && prefs.PreferredCategories.Any())
        {
            if (prefs.PreferredCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase))
                score += 1.0f;
        }

        // Tag overlap
        if (prefs.PreferredTags != null && prefs.PreferredTags.Any() && item.Tags != null)
        {
            int overlap = item.Tags.Count(t => prefs.PreferredTags.Any(p => p.Equals(t, StringComparison.OrdinalIgnoreCase)));
            score += overlap * 0.5f;
        }

        // Price tier match
        if (prefs.PreferredPriceTiers != null && prefs.PreferredPriceTiers.Any())
        {
            if (prefs.PreferredPriceTiers.Contains(item.PriceTier))
                score += 0.3f;
        }

        // Location feasibility (if user has home location)
        if (homeLocation != null && item.Location != null)
        {
            double distKm = item.Location.DistanceToKm(homeLocation.Value);
            if (prefs.MaxTravelDistanceKm.HasValue && distKm <= prefs.MaxTravelDistanceKm.Value)
            {
                // Closer = higher score; max 0.5 for within 1km, scaling down
                score += Math.Max(0f, 0.5f - (float)(distKm / 10.0));
            }
        }

        return score;
    }

    public IReadOnlyList<(string ItemId, float Score)> GetPopularityFallback(int maxResults = 10)
        => new List<(string, float)>();
}