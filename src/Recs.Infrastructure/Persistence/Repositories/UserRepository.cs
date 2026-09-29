using Microsoft.EntityFrameworkCore;
using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;

namespace Recs.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly RecsDbContext _context;

    public UserRepository(RecsDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        _context.Users.FirstOrDefaultAsync(u => u.Username == username.Trim(), cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Users.ToListAsync(cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default) =>
        await _context.Users.AddAsync(user, cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is not null)
        {
            _context.Users.Remove(user);
        }
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default) =>
        _context.Users.AnyAsync(u => u.Id == id, cancellationToken);
}
