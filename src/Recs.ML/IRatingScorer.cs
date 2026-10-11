using Recs.ML.Models;

namespace Recs.ML;

public interface IRatingScorer
{
    bool IsModelLoaded { get; }
    IReadOnlyList<(string ItemId, float Score)> ScoreBatch(string userId, IEnumerable<string> itemIds);
}
