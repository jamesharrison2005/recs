using Recs.Domain.Entities;
using Recs.Domain.Enums;

namespace Recs.Application.Common.Interfaces;

public interface IItemRepository
{
    Task<Item?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByCategoryAsync(string category, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByTypeAsync(ItemType type, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Item>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Item item, CancellationToken cancellationToken = default);
    Task UpdateAsync(Item item, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
}
