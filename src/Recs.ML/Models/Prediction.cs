using Microsoft.ML.Data;

namespace Recs.ML.Models;

/// <summary>
/// Output from a trained recommendation model.
/// Score is the predicted rating (1-5). Top items by score are recommendations.
/// </summary>
public class Prediction
{
    [ColumnName("score")]
    public float Score { get; set; }
}