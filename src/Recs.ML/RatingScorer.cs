using Microsoft.ML;
using Microsoft.ML.Data;
using Recs.ML.Models;

namespace Recs.ML;

public sealed class RatingScorer : IRatingScorer
{
    private readonly ITransformer? _model;
    private readonly MLContext _ml = new();

    public bool IsModelLoaded => _model != null;

    public RatingScorer(string? modelPath = null)
    {
        if (!string.IsNullOrWhiteSpace(modelPath) && System.IO.File.Exists(modelPath))
        {
            _model = _ml.Model.Load(modelPath, out _);
        }
    }

    public IReadOnlyList<(string ItemId, float Score)> ScoreBatch(string userId, IEnumerable<string> itemIds)
    {
        var candidates = itemIds.ToList();
        if (candidates.Count == 0 || _model == null) return new List<(string, float)>();

        var data = new List<RatingData>();
        foreach (var id in candidates)
            data.Add(new RatingData { UserId = userId, ItemId = id, Score = 0f });

        var view = _ml.Data.LoadFromEnumerable(data);
        var transformed = _model.Transform(view);
        var predictions = _ml.Data.CreateEnumerable<Prediction>(transformed, reuseRowObject: false).ToList();

        var result = new List<(string ItemId, float Score)>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var score = predictions[i].Score;
            if (!float.IsNaN(score) && !float.IsInfinity(score))
                result.Add((candidates[i], score));
        }
        return result;
    }
}
