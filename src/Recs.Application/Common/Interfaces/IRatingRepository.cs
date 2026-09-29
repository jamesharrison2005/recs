using Recs.Domain.Entities;

namespace Recs.Application.Common.Interfaces;

public interface IRatingRepository
{
    Task<Rating?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<Rating?> GetByUserAndItemAsync(string userId, string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rating>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rating>> GetByItemIdAsync(string itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Rating>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Rating rating, CancellationToken cancellationToken = default);
    Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
