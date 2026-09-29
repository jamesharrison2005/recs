using Microsoft.EntityFrameworkCore;
using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;
using Recs.Domain.Enums;

namespace Recs.Infrastructure.Persistence.Repositories;

public class ItemRepository : IItemRepository
{
    private readonly RecsDbContext _context;

    public ItemRepository(RecsDbContext context)
    {
        _context = context;
    }

    public Task<Item?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Item>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        IQueryable<Item> query = _context.Items;
        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> GetByCategoryAsync(string category, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var normalized = category.Trim();

        IQueryable<Item> query = _context.Items
            .Where(i => i.Category == normalized);

        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> GetByTypeAsync(ItemType type, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        IQueryable<Item> query = _context.Items
            .Where(i => i.Type == type);

        if (activeOnly)
        {
            query = query.Where(i => i.IsActive);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Item>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await _context.Items
            .Where(i => idList.Contains(i.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Item item, CancellationToken cancellationToken = default) =>
        await _context.Items.AddAsync(item, cancellationToken);

    public Task UpdateAsync(Item item, CancellationToken cancellationToken = default)
    {
        _context.Items.Update(item);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is not null)
        {
            _context.Items.Remove(item);
        }
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Items.AnyAsync(i => i.Id == id, cancellationToken);
}
