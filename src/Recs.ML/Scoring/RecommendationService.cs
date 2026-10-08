using Microsoft.ML;
using Microsoft.ML.Data;
using Recs.Infrastructure.Persistence;
using Recs.ML.Models;
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

    public RecommendationService(MLContext ml, string modelPath, RecsDbContext db)
    {
        _ml = ml;
        _db = db;
        _model = ml.Model.Load(modelPath, out _);

        // Cache already-rated user-item pairs to filter from recommendations
        _ratedPairs = new HashSet<string>(
            db.Ratings.Select(r => $"{r.UserId}:{r.ItemId}"),
            System.StringComparer.Ordinal);
    }

    /// <summary>
    /// Get top-N recommended item IDs and scores for a user.
    /// Filters out items the user has already rated.
    /// </summary>
    public IReadOnlyList<(string ItemId, float Score)> GetTopN(string userId, int n = 10)
    {
        // Get all active items from DB
        var allItems = _db.Items
            .Where(i => i.IsActive)
            .Select(i => new RatingData
            {
                UserId = userId,
                ItemId = i.Id,
                Score = 0 // Dummy label
            })
            .ToList();

        if (allItems.Count == 0)
            return new List<(string, float)>();

        // Create prediction engine for scoring
        var predEngine = _ml.Model.CreatePredictionEngine<RatingData, Prediction>(_model);

        // Score all items and filter already-rated
        var scored = allItems
            .Select(item => new
            {
                item.ItemId,
                Score = predEngine.Predict(item).Score,
                Key = $"{userId}:{item.ItemId}"
            })
            .Where(x => !_ratedPairs.Contains(x.Key))
            .OrderByDescending(x => x.Score)
            .Take(n)
            .Select(x => (x.ItemId, x.Score))
            .ToList();

        return scored;
    }
}