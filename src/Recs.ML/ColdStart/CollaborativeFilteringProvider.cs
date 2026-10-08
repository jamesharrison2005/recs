using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Recs.ML.ColdStart;

/// <summary>
/// Collaborative filtering provider — delegates to trained MatrixFactorization model.
/// Requires sufficient rating history; not suitable for pure cold-start users/items.
/// </summary>
public sealed class CollaborativeFilteringProvider : IRecommendationProvider
{
    private readonly string _modelPath;

    public CollaborativeFilteringProvider(string modelPath = "ml-model.zip")
    {
        _modelPath = modelPath;
    }

    public IReadOnlyList<(string ItemId, float Score)> GetRecommendations(string userId, int maxResults = 10)
        => GetCollaborative(userId, maxResults);

    public IReadOnlyList<(string ItemId, float Score)> GetCollaborative(string userId, int maxResults = 10)
    {
        // Delegate to the trained ML model; in full integration this loads
        // RecommendationService or creates a PredictionEngine from the saved model.
        // For now: return empty — integration point for Pipeline/Service.
        // Implementation detail: load _modelPath via MLContext, score all items, filter rated.
        return new List<(string, float)>();
    }

    public IReadOnlyList<(string ItemId, float Score)> GetContentBased(string userId, int maxResults = 10)
        => new List<(string, float)>();

    public IReadOnlyList<(string ItemId, float Score)> GetPopularityFallback(int maxResults = 10)
        => new List<(string, float)>();
}