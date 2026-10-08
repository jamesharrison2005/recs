using Microsoft.ML.Data;

namespace Recs.ML.Models;

/// <summary>
/// Input example for ML.NET training: a single rating from a user to an item.
/// Maps from the domain Rating entity.
/// </summary>
public class RatingData
{
    [LoadColumn(0), KeyType(1000)]
    public string UserId { get; set; } = string.Empty;

    [LoadColumn(1), KeyType(1000)]
    public string ItemId { get; set; } = string.Empty;

    [LoadColumn(2)]
    public float Score { get; set; }
}