using Microsoft.ML.Data;

namespace Recs.ML.Models;

public class RatingData
{
    [LoadColumn(0)]
    public string UserId { get; set; } = string.Empty;

    [LoadColumn(1)]
    public string ItemId { get; set; } = string.Empty;

    [LoadColumn(2)]
    public float Score { get; set; }
}
