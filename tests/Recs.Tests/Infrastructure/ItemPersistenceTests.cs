using Microsoft.EntityFrameworkCore;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;
using Recs.Infrastructure.Persistence.Repositories;

namespace Recs.Tests.Infrastructure;

public class ItemPersistenceTests
{
    [Fact]
    public async Task Restaurant_RoundTrips_WithLocationTagsAndEnums()
    {
        using var database = new SqliteTestDatabase();
        var createdAt = new DateTimeOffset(2026, 3, 1, 9, 30, 0, TimeSpan.Zero);

        var restaurant = Item.CreateRestaurant(
            id: "item-peel-castle",
            name: "Peel Castle Café",
            category: "Café",
            priceTier: PriceTier.Budget,
            location: new GeoLocation(54.2369, -4.5486),
            tags: new[] { "Seafood", " outdoor-seating " },
            description: "Harbourside café by the castle.",
            createdAt: createdAt);

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            await repository.AddAsync(restaurant);
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var loaded = await new ItemRepository(readContext).GetByIdAsync("item-peel-castle");

            Assert.NotNull(loaded);
            Assert.Equal("item-peel-castle", loaded!.Id);
            Assert.Equal("Peel Castle Café", loaded.Name);
            Assert.Equal("Café", loaded.Category);
            Assert.Equal(ItemType.Restaurant, loaded.Type);
            Assert.Equal(PriceTier.Budget, loaded.PriceTier);
            Assert.Equal(new GeoLocation(54.2369, -4.5486), loaded.Location);
            Assert.Equal("Harbourside café by the castle.", loaded.Description);
            Assert.Equal(createdAt, loaded.CreatedAt);
            Assert.True(loaded.IsActive);
            Assert.Null(loaded.EventStart);
            Assert.Null(loaded.EventEnd);

            // Tags are normalised to lowercase/trimmed by the domain, and must
            // come back case-insensitive after the JSON round trip.
            Assert.Equal(new[] { "outdoor-seating", "seafood" }, loaded.Tags.OrderBy(t => t));
            Assert.Contains("SEAFOOD", loaded.Tags);
        }
    }

    [Fact]
    public async Task Event_RoundTrips_WithEventStartAndEnd()
    {
        using var database = new SqliteTestDatabase();
        var eventStart = new DateTimeOffset(2026, 7, 4, 18, 0, 0, TimeSpan.Zero);
        var eventEnd = new DateTimeOffset(2026, 7, 4, 23, 0, 0, TimeSpan.Zero);

        var item = Item.CreateEvent(
            id: "item-tynwald",
            name: "Tynwald Day",
            category: "Festival",
            priceTier: PriceTier.Free,
            location: new GeoLocation(54.1909, -4.4956),
            eventStart: eventStart,
            eventEnd: eventEnd,
            tags: new[] { "family" });


        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            await repository.AddAsync(item);
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var loaded = await new ItemRepository(readContext).GetByIdAsync("item-tynwald");

            Assert.NotNull(loaded);
            Assert.Equal(ItemType.Event, loaded!.Type);
            Assert.Equal(eventStart, loaded.EventStart);
            Assert.Equal(eventEnd, loaded.EventEnd);
            Assert.Equal(PriceTier.Free, loaded.PriceTier);
            Assert.Equal(new[] { "family" }, loaded.Tags);
        }
    }

    [Fact]
    public async Task GetByCategory_ExcludesInactiveItemsByDefault()
    {
        using var database = new SqliteTestDatabase();
        var location = new GeoLocation(54.1500, -4.4800);

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);

            await repository.AddAsync(Item.CreateRestaurant(
                "item-active", "Active Diner", "Restaurant", PriceTier.Moderate, location));
            await repository.AddAsync(Item.CreateRestaurant(
                "item-closed", "Closed Diner", "Restaurant", PriceTier.Moderate, location,
                isActive: false));
            await repository.AddAsync(Item.CreateEvent(
                "item-festival", "Summer Festival", "Festival", PriceTier.Free, location,
                eventStart: DateTimeOffset.UtcNow.AddDays(30)));

            await writeContext.SaveChangesAsync();
        }

        using var readContext = database.CreateContext();
        var repositoryUnderTest = new ItemRepository(readContext);

        var active = await repositoryUnderTest.GetByCategoryAsync("Restaurant");
        Assert.Equal(new[] { "item-active" }, active.Select(i => i.Id));

        var all = await repositoryUnderTest.GetByCategoryAsync("Restaurant", activeOnly: false);
        Assert.Equal(2, all.Count);

        var events = await repositoryUnderTest.GetByTypeAsync(ItemType.Event);
        Assert.Equal(new[] { "item-festival" }, events.Select(i => i.Id));
    }

    [Fact]
    public async Task UpdateStatus_And_Delete_Are_Persisted()
    {
        using var database = new SqliteTestDatabase();

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            await repository.AddAsync(Item.CreateRestaurant(
                "item-temp", "Temp Diner", "Restaurant", PriceTier.Budget,
                new GeoLocation(54.1500, -4.4800)));
            await writeContext.SaveChangesAsync();
        }

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            var item = await repository.GetByIdAsync("item-temp");
            item!.UpdateStatus(false);
            await repository.UpdateAsync(item);
            await writeContext.SaveChangesAsync();
        }

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            await repository.DeleteAsync("item-temp");
            await writeContext.SaveChangesAsync();
        }

        using var readContext = database.CreateContext();
        Assert.Null(await new ItemRepository(readContext).GetByIdAsync("item-temp"));
    }

    [Fact]
    public async Task Duplicate_Item_Id_Throws()
    {
        using var database = new SqliteTestDatabase();
        var location = new GeoLocation(54.1500, -4.4800);

        using (var writeContext = database.CreateContext())
        {
            var repository = new ItemRepository(writeContext);
            await repository.AddAsync(Item.CreateRestaurant(
                "item-dup", "First", "Restaurant", PriceTier.Budget, location));
            await writeContext.SaveChangesAsync();
        }

        using var context = database.CreateContext();
        var repositoryUnderTest = new ItemRepository(context);

        await repositoryUnderTest.AddAsync(Item.CreateRestaurant(
            "item-dup", "Second", "Restaurant", PriceTier.Budget, location));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
