using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;
using Recs.Infrastructure.Persistence;

namespace Recs.Tests.Infrastructure;

/// <summary>
/// In-memory SQLite database seeded with a deterministic popularity order:
/// item-a (avg 4.67, 3 ratings) &gt; item-c (avg 4.0, 2 ratings) &gt;
/// item-b (avg 3.0, 3 ratings) &gt; item-d (avg 2.0, 1 rating). item-e is unrated.
/// user-1 has rated item-a and item-b. item-e is unrated, so the popularity
/// fallback never returns it (no aggregate to rank by).
/// </summary>
public sealed class SeededApiTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public SeededApiTestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = CreateOptions();
        using var context = new RecsDbContext(options);
        context.Database.EnsureCreated();

        Seed(context);
    }

    public DbContextOptions<RecsDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<RecsDbContext>()
            .UseSqlite(_connection)
            .Options;

    private static void Seed(RecsDbContext context)
    {
        var geo = new GeoLocation(54.15, -4.48);

        context.Items.AddRange(
            Item.CreateRestaurant("item-a", "Item A", "Cafe", PriceTier.Moderate, geo),
            Item.CreateRestaurant("item-b", "Item B", "Cafe", PriceTier.Moderate, geo),
            Item.CreateRestaurant("item-c", "Item C", "Pub", PriceTier.Budget, geo),
            Item.CreateRestaurant("item-d", "Item D", "Pub", PriceTier.Budget, geo),
            Item.CreateRestaurant("item-e", "Item E", "Restaurant", PriceTier.Expensive, geo));

        context.Users.AddRange(
            new User("user-1", "alice"),
            new User("user-2", "bob"),
            new User("user-3", "carol"));

        context.Ratings.AddRange(
            new Rating("r-1", "user-2", "item-a", 5.0f),
            new Rating("r-2", "user-3", "item-a", 5.0f),
            new Rating("r-3", "user-1", "item-a", 4.0f),
            new Rating("r-4", "user-2", "item-b", 3.0f),
            new Rating("r-5", "user-3", "item-b", 3.0f),
            new Rating("r-6", "user-1", "item-b", 3.0f),
            new Rating("r-7", "user-2", "item-c", 4.0f),
            new Rating("r-8", "user-3", "item-c", 4.0f),
            new Rating("r-9", "user-2", "item-d", 2.0f));

        context.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();
}
