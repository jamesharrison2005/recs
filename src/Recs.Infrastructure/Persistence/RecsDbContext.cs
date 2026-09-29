using Microsoft.EntityFrameworkCore;
using Recs.Application.Common.Interfaces;
using Recs.Domain.Entities;

namespace Recs.Infrastructure.Persistence;

public class RecsDbContext : DbContext, IUnitOfWork
{
    public DbSet<Item> Items => Set<Item>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Rating> Ratings => Set<Rating>();

    public RecsDbContext(DbContextOptions<RecsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RecsDbContext).Assembly);
    }
}
