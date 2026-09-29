using Microsoft.EntityFrameworkCore;
using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;

namespace Recs.Infrastructure.Persistence.Repositories;

public class RatingRepository : IRatingRepository
{
    private readonly RecsDbContext _context;

    public RatingRepository(RecsDbContext context)
    {
        _context = context;
    }

    public Task<Rating?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Ratings.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Rating?> GetByUserAndItemAsync(string userId, string itemId, CancellationToken cancellationToken = default) =>
        _context.Ratings.FirstOrDefaultAsync(r => r.UserId == userId && r.ItemId == itemId, cancellationToken);

    public async Task<IReadOnlyList<Rating>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default) =>
        await _context.Ratings
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Rating>> GetByItemIdAsync(string itemId, CancellationToken cancellationToken = default) =>
        await _context.Ratings
            .Where(r => r.ItemId == itemId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Rating>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Ratings.ToListAsync(cancellationToken);

    public async Task AddAsync(Rating rating, CancellationToken cancellationToken = default) =>
        await _context.Ratings.AddAsync(rating, cancellationToken);

    public Task UpdateAsync(Rating rating, CancellationToken cancellationToken = default)
    {
        _context.Ratings.Update(rating);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var rating = await _context.Ratings.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (rating is not null)
        {
            _context.Ratings.Remove(rating);
        }
    }
}
