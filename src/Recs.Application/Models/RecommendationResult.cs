using Recs.Domain.Entities;

namespace Recs.Application.Models;

public enum RecommendationSource { Model, Fallback }

public sealed class RecommendationResult
{
    public Item Item { get; }
    public float Score { get; }
    public RecommendationSource Source { get; }

    public RecommendationResult(Item item, float score, RecommendationSource source)
    {
        Item = item;
        Score = score;
        Source = source;
    }
}
