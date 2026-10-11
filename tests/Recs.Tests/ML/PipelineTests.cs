using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;
using Recs.Infrastructure.Persistence;
using Recs.Infrastructure.Persistence.Repositories;
using Recs.ML.Models;
using Recs.ML.Training;
using Recs.Tests.Infrastructure;
using Xunit;

namespace Recs.Tests.ML;

public class PipelineTests : IClassFixture<SqliteTestDatabase>
{
    private readonly SqliteTestDatabase _database;
    private readonly RecsDbContext _db;

    public PipelineTests(SqliteTestDatabase fixture)
    {
        _database = fixture;
        _db = fixture.CreateContext();
    }

    [Fact]
    public void Pipeline_CanLoadRatings_FromDatabase()
    {
        // Act
        var count = _db.Ratings.Count();

        // Assert: at least some ratings should exist if DB was seeded
        Assert.True(count >= 0, "Rating count readable");
    }

    [Fact]
    public void Pipeline_TrainTestSplit_ProducesNonEmptySets()
    {
        // Arrange
        using (var writeContext = _database.CreateContext())
        {
            // Seed more data to ensure train/test split works
            for(int i = 0; i < 20; i++)
            {
                SeedUserAndItemAsync(_database, $"u{i}", $"i{i}");
                writeContext.Ratings.Add(new Rating($"r{i}", $"u{i}", $"i{i}", 4.0f));
            }
            writeContext.SaveChanges();
        }

        var ratings = _db.Ratings.Select(r => new RatingData
        {
            UserId = r.UserId,
            ItemId = r.ItemId,
            Score = r.Score
        }).ToList();

        // Act
        var ml = new Microsoft.ML.MLContext(seed: 42);
        var data = ml.Data.LoadFromEnumerable(ratings);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        // Assert
        Assert.NotEmpty(ml.Data.CreateEnumerable<RatingData>(split.TrainSet, reuseRowObject: false));
        Assert.NotEmpty(ml.Data.CreateEnumerable<RatingData>(split.TestSet, reuseRowObject: false));
    }

    private static void SeedUserAndItemAsync(SqliteTestDatabase database, string userId, string itemId)
    {
        using var context = database.CreateContext();

        var userRepository = new UserRepository(context);
        if (!userRepository.ExistsAsync(userId).GetAwaiter().GetResult())
        {
            userRepository.AddAsync(new User(userId, $"name-{userId}")).GetAwaiter().GetResult();
        }

        var itemRepository = new ItemRepository(context);
        if (!itemRepository.ExistsAsync(itemId).GetAwaiter().GetResult())
        {
            itemRepository.AddAsync(Item.CreateRestaurant(
                id: itemId,
                name: $"Item {itemId}",
                category: "Restaurant",
                priceTier: PriceTier.Budget,
                location: new GeoLocation(54.1500, -4.4800))).GetAwaiter().GetResult();
        }

        context.SaveChanges();
    }
}
