using Microsoft.ML;
using Microsoft.ML.Data;
using Recs.ML.Models;
using System.Linq;

namespace Recs.ML.Training;

/// <summary>
/// Computes recommendation-quality metrics on a trained model + test data.
/// </summary>
public sealed class Evaluator
{
    public double Rmse { get; }
    public double R2 { get; }
    public double PrecisionAtK { get; }
    public double RecallAtK { get; }

    public Evaluator(ITransformer model, IDataView testSet)
    {
        var ml = new MLContext(seed: 42);
        var predictions = model.Transform(testSet);

        // Regression metrics (RMSE, R^2) against the actual label
        var metrics = ml.Regression.Evaluate(predictions, "label");

        Rmse = metrics.RootMeanSquaredError;
        R2 = metrics.RSquared;

        // For Precision@K and Recall@K, we need to compute top-K recommendations per user
        // and compare with actual high-rated items (Score >= 4.0)
        var predictionData = ml.Data.CreateEnumerable<RatingPrediction>(predictions, reuseRowObject: false).ToList();
        var testData = ml.Data.CreateEnumerable<RatingData>(testSet, reuseRowObject: false).ToList();

        PrecisionAtK = ComputePrecisionAtK(predictionData, testData, k: 5);
        RecallAtK = ComputeRecallAtK(predictionData, testData, k: 5);
    }

    private double ComputePrecisionAtK(List<RatingPrediction> predictions, List<RatingData> testData, int k)
    {
        // Group predictions by user
        var predictionsByUser = predictions.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var actualRatingsByUser = testData.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.ToList());

        double totalPrecision = 0;
        int userCount = 0;

        foreach (var userId in predictionsByUser.Keys.Where(actualRatingsByUser.ContainsKey))
        {
            var userPredictions = predictionsByUser[userId]
                .OrderByDescending(p => p.Score)
                .Take(k)
                .ToList();

            var actualHighRatings = actualRatingsByUser[userId]
                .Where(r => r.Score >= 4.0) // Consider 4.0+ as relevant
                .Select(r => r.ItemId)
                .ToHashSet();

            if (actualHighRatings.Count == 0)
                continue;

            int hits = userPredictions.Count(p => actualHighRatings.Contains(p.ItemId));
            double precision = (double)hits / k;
            totalPrecision += precision;
            userCount++;
        }

        return userCount > 0 ? totalPrecision / userCount : 0.0;
    }

    private double ComputeRecallAtK(List<RatingPrediction> predictions, List<RatingData> testData, int k)
    {
        // Group predictions by user
        var predictionsByUser = predictions.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => g.ToList());
        var actualRatingsByUser = testData.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.ToList());

        double totalRecall = 0;
        int userCount = 0;

        foreach (var userId in predictionsByUser.Keys.Where(actualRatingsByUser.ContainsKey))
        {
            var userPredictions = predictionsByUser[userId]
                .OrderByDescending(p => p.Score)
                .Take(k)
                .Select(p => p.ItemId)
                .ToHashSet();

            var actualHighRatings = actualRatingsByUser[userId]
                .Where(r => r.Score >= 4.0) // Consider 4.0+ as relevant
                .Select(r => r.ItemId)
                .ToList();

            if (actualHighRatings.Count == 0)
                continue;

            int hits = userPredictions.Intersect(actualHighRatings).Count();
            double recall = (double)hits / actualHighRatings.Count;
            totalRecall += recall;
            userCount++;
        }

        return userCount > 0 ? totalRecall / userCount : 0.0;
    }
}

// Helper class for prediction output
public class RatingPrediction
{
    [LoadColumn(0)] public string UserId { get; set; } = string.Empty;
    [LoadColumn(1)] public string ItemId { get; set; } = string.Empty;
    [LoadColumn(2)] public float Score { get; set; }
}