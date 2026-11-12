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
    public double Mae { get; }

    public Evaluator(ITransformer model, IDataView testSet)
    {
        var ml = new MLContext(seed: 42);
        var predictions = model.Transform(testSet);

        // Regression metrics (RMSE, R^2, MAE) against the original label.
        // The training pipeline copies the label to "Label" so the transformed schema carries
        // both the prediction ("Score") and the ground truth ("Label").
        var metrics = ml.Regression.Evaluate(predictions, "Label", scoreColumnName: "Score");

        Rmse = metrics.RootMeanSquaredError;
        R2 = metrics.RSquared;
        Mae = metrics.MeanAbsoluteError;

        // TODO: Precision@K / Recall@K are not computed here: the transformed schema maps
        // UserId and ItemId to Key-typed values (via MapValueToKey), which CreateEnumerable cannot
        // decode back to string ids. Computing them needs a key-to-value mapping (e.g. caching the
        // MapValueToKey model from the training pipeline) or a separate evaluation data view
        // carrying raw ids alongside the predictions.
    }
}
