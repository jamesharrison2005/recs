using Microsoft.ML;
using Microsoft.ML.Data;
using Recs.Infrastructure.Persistence;
using Recs.ML.Models;
using Recs.Application.Common.Interfaces;
using Recs.ML.ColdStart;
using System.Collections.Generic;
using System.Linq;

namespace Recs.ML.Scoring;

/// <summary>
/// Given a user ID, produce top-N recommendations with predicted scores.
/// Uses simple scoring via ML.NET prediction engine (no PredictionEnginePool for compatibility).
/// </summary>
public sealed class RecommendationService
{
    private readonly MLContext _ml;
    private readonly ITransformer _model;
    private readonly RecsDbContext _db;
    private readonly HashSet<string> _ratedPairs;
    private readonly IRecommendationProvider _provider;

    public RecommendationService(MLContext ml, string modelPath, RecsDbContext db)
    {
        _ml = ml;
        _db = db;
        _model = ml.Model.Load(modelPath, out _);

        // Cache already-rated user-item pairs to filter from recommendations
        _ratedPairs = new HashSet<string>(
            db.Ratings.Select(r => $"{r.UserId}:{r.ItemId}"),
            System.StringComparer.Ordinal);

        // Wire cold-start provider — selects strategy based on rating density
        _provider = new CollaborativeFilteringProvider(modelPath); // base; can be extended to multi-strategy
    }

    /// <summary>
    /// Get top-N recommended item IDs and scores for a user.
    /// Filters out items the user has already rated.
    /// </summary>
    public IReadOnlyList<(string ItemId, float Score)> GetTopN(string userId, int n = 10)
    {
        // Strategy: use collaborative when user has ratings; fall back to content/popularity otherwise
        var userRatingCount = _db.Ratings.Count(r => r.UserId == userId);
        const int MinRatingsForCollaborative = 5;

        IReadOnlyList<(string ItemId, float Score)> recommendations;
        if (userRatingCount >= MinRatingsForCollaborative)
            recommendations = _provider.GetCollaborative(userId, n);
        else
        {
            recommendations = _provider.GetContentBased(userId, n);
            if (recommendations.Count == 0)
                recommendations = _provider.GetPopularityFallback(n);
        }

        return recommendations
            .Where(r => !_ratedPairs.Contains($"{userId}:{r.ItemId}"))
            .Take(n)
            .ToList();
    }
}