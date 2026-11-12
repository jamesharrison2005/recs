using Microsoft.ML;
using Microsoft.ML.Trainers;
using Recs.ML.Models;
using Recs.Infrastructure.Persistence;
using System.Collections.Generic;

namespace Recs.ML.Training;

/// <summary>
/// Constructs the ML pipeline: load ratings from DB → train/test split → Matrix Factorization → evaluation.
/// </summary>
public sealed class Pipeline
{
    private readonly RecsDbContext _db;
    private readonly string _modelPath; // e.g. "ml-model.zip"

    public Pipeline(RecsDbContext db, string modelPath = "ml-model.zip")
    {
        _db = db;
        _modelPath = modelPath;
    }

    /// <summary>
    /// Build the MLContext + pipeline and fit the MatrixFactorization model on the train split.
    /// Call Train() to fit the model on data and persist it to disk.
    /// </summary>
    public (ITransformer Model, IDataView TrainSet, IDataView TestSet) BuildPipeline()
    {
        var ml = new MLContext(seed: 42);

        // 1. Load ratings from SQLite
        var data = ml.Data.LoadFromEnumerable(LoadRatingsAsEnumerable());

        // 2. Train/Test split (80/20 by default)
        var trainTestSplit = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        // 3. MatrixFactorizationTrainer
        // Copy the label first so the output schema contains a prediction named "Score"
        // and the original label under "Label" — otherwise both land on "Score".
        var pipeline = ml.Transforms.CopyColumns("Label", nameof(RatingData.Score))
            .Append(ml.Transforms.Conversion.MapValueToKey(nameof(RatingData.UserId)))
            .Append(ml.Transforms.Conversion.MapValueToKey(nameof(RatingData.ItemId)))
            .Append(ml.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = nameof(RatingData.UserId),
                MatrixRowIndexColumnName = nameof(RatingData.ItemId),
                LabelColumnName = "Label",
                NumberOfIterations = 20,
                ApproximationRank = 50,
                LearningRate = 0.01f
            }));

        // 4. Train the model
        var model = pipeline.Fit(trainTestSplit.TrainSet);

        return (model, trainTestSplit.TrainSet, trainTestSplit.TestSet);
    }

    /// <summary>
    /// Materialize ratings from the database as RatingData enumerable.
    /// </summary>
    private IEnumerable<RatingData> LoadRatingsAsEnumerable()
    {
        foreach (var rating in _db.Ratings)
        {
            yield return new RatingData
            {
                UserId = rating.UserId,
                ItemId = rating.ItemId,
                Score = rating.Score
            };
        }
    }

    /// <summary>
    /// Train the model, persist it to disk and return the training artifacts so callers
    /// can run evaluation without training twice.
    /// </summary>
    public (ITransformer Model, IDataView TrainSet, IDataView TestSet) Train()
    {
        var ml = new MLContext(seed: 42);
        var (model, trainSet, testSet) = BuildPipeline();
        ml.Model.Save(model, trainSet.Schema, _modelPath);
        return (model, trainSet, testSet);
    }
}
