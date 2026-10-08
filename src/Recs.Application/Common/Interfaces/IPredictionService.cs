using Recs.Application.Models;
using Recs.Domain.Entities;

namespace Recs.Application.Common.Interfaces;

public interface IPredictionService
{
    /// <summary>
    /// Predict top-N recommendations for a user with optional filters.
    /// Returns item details; unknown users get popular items (with filter respected).
    /// </summary>
    Task<IReadOnlyList<Item>> PredictAsync(string userId, int n = 10, ItemFilter? filters = null);
}
