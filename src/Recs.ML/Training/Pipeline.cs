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
    /// Build the MLContext + pipeline, but do NOT train yet.
    /// Call Train() to fit the model on data.
    /// </summary>
    public (ITransformer Model, IDataView TrainSet, IDataView TestSet) BuildPipeline()
    {
        var ml = new MLContext(seed: 42);

        // 1. Load ratings from SQLite
        var data = ml.Data.LoadFromEnumerable(LoadRatingsAsEnumerable());

        // 2. Train/Test split (80/20 by default)
        var trainTestSplit = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        // 3. MatrixFactorizationTrainer
        // Mapping string IDs to keys is required for MatrixFactorization
        var pipeline = ml.Transforms.Conversion.MapValueToKey(nameof(RatingData.UserId))
            .Append(ml.Transforms.Conversion.MapValueToKey(nameof(RatingData.ItemId)))
            .Append(ml.Recommendation().Trainers.MatrixFactorization(new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = nameof(RatingData.UserId),
                MatrixRowIndexColumnName = nameof(RatingData.ItemId),
                LabelColumnName = nameof(RatingData.Score),
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
    /// Train the model and persist to disk.
    /// </summary>
    public void Train()
    {
        var ml = new MLContext(seed: 42);
        var (model, trainSet, _) = BuildPipeline();
        ml.Model.Save(model, trainSet.Schema, _modelPath);
    }
}
