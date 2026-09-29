using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;
using Recs.Infrastructure.Persistence.Repositories;

namespace Recs.Tests.Infrastructure;

public class RatingPersistenceTests
{
    [Fact]
    public async Task Rating_RoundTrips_And_IsQueryableByUserAndItem()
    {
        using var database = new SqliteTestDatabase();
        await SeedUserAndItemAsync(database, "user-1", "item-1");
        await SeedUserAndItemAsync(database, "user-2", "item-1");

        var timestamp = new DateTimeOffset(2026, 5, 20, 14, 45, 0, TimeSpan.Zero);

        using (var writeContext = database.CreateContext())
        {
            var repository = new RatingRepository(writeContext);
            await repository.AddAsync(new Rating("rating-1", "user-1", "item-1", 4.5f, "Great chips.", timestamp));
            await repository.AddAsync(new Rating("rating-2", "user-2", "item-1", 2.0f, "Not for me.", timestamp));
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var repository = new RatingRepository(readContext);

            var loaded = await repository.GetByIdAsync("rating-1");
            Assert.NotNull(loaded);
            Assert.Equal("user-1", loaded!.UserId);
            Assert.Equal("item-1", loaded.ItemId);
            Assert.Equal(4.5f, loaded.Score);
            Assert.Equal("Great chips.", loaded.Review);
            Assert.Equal(timestamp, loaded.Timestamp);

            var byPair = await repository.GetByUserAndItemAsync("user-1", "item-1");
            Assert.Equal("rating-1", byPair!.Id);

            var byUser = await repository.GetByUserIdAsync("user-1");
            Assert.Equal(new[] { "rating-1" }, byUser.Select(r => r.Id));

            var byItem = await repository.GetByItemIdAsync("item-1");
            Assert.Equal(2, byItem.Count);
        }
    }

    [Fact]
    public async Task User_Cannot_Rate_The_Same_Item_Twice()
    {
        using var database = new SqliteTestDatabase();
        await SeedUserAndItemAsync(database, "user-1", "item-1");

        using (var writeContext = database.CreateContext())
        {
            var repository = new RatingRepository(writeContext);
            await repository.AddAsync(new Rating("rating-first", "user-1", "item-1", 3.0f));
            await writeContext.SaveChangesAsync();
        }

        using var context = database.CreateContext();
        var repositoryUnderTest = new RatingRepository(context);

        await repositoryUnderTest.AddAsync(new Rating("rating-second", "user-1", "item-1", 1.0f, "Changed my mind."));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Contains("UNIQUE constraint failed: Ratings.UserId, Ratings.ItemId", sqliteException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_Rating_Id_Is_Rejected_Even_For_Different_Pairs()
    {
        using var database = new SqliteTestDatabase();
        await SeedUserAndItemAsync(database, "user-1", "item-1");
        await SeedUserAndItemAsync(database, "user-2", "item-2");

        using (var writeContext = database.CreateContext())
        {
            var repository = new RatingRepository(writeContext);
            await repository.AddAsync(new Rating("rating-same-id", "user-1", "item-1", 3.0f));
            await writeContext.SaveChangesAsync();
        }

        using var context = database.CreateContext();
        var repositoryUnderTest = new RatingRepository(context);

        await repositoryUnderTest.AddAsync(new Rating("rating-same-id", "user-2", "item-2", 3.0f));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Rating_For_Unknown_Item_Is_Rejected_By_Foreign_Key()
    {
        using var database = new SqliteTestDatabase();
        await SeedUserAndItemAsync(database, "user-1", "item-1");

        using var context = database.CreateContext();
        var repository = new RatingRepository(context);

        await repository.AddAsync(new Rating("rating-orphan", "user-1", "item-does-not-exist", 3.0f));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_An_Item_Cascades_To_Its_Ratings()
    {
        using var database = new SqliteTestDatabase();
        await SeedUserAndItemAsync(database, "user-1", "item-1");

        using (var writeContext = database.CreateContext())
        {
            var repository = new RatingRepository(writeContext);
            await repository.AddAsync(new Rating("rating-1", "user-1", "item-1", 3.0f));
            await writeContext.SaveChangesAsync();
        }

        using (var writeContext = database.CreateContext())
        {
            var itemRepository = new ItemRepository(writeContext);
            await itemRepository.DeleteAsync("item-1");
            await writeContext.SaveChangesAsync();
        }

        using var readContext = database.CreateContext();
        Assert.Empty(await new RatingRepository(readContext).GetByItemIdAsync("item-1"));
    }

    private static async Task SeedUserAndItemAsync(SqliteTestDatabase database, string userId, string itemId)
    {
        using var context = database.CreateContext();

        var userRepository = new UserRepository(context);
        if (!await userRepository.ExistsAsync(userId))
        {
            await userRepository.AddAsync(new User(userId, $"name-{userId}"));
        }

        var itemRepository = new ItemRepository(context);
        if (!await itemRepository.ExistsAsync(itemId))
        {
            await itemRepository.AddAsync(Item.CreateRestaurant(
                id: itemId,
                name: $"Item {itemId}",
                category: "Restaurant",
                priceTier: PriceTier.Budget,
                location: new GeoLocation(54.1500, -4.4800)));
        }

        await context.SaveChangesAsync();
    }
}
