using Microsoft.EntityFrameworkCore;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;
using Recs.Infrastructure.Persistence.Repositories;

namespace Recs.Tests.Infrastructure;

public class UserPersistenceTests
{
    [Fact]
    public async Task User_RoundTrips_WithHomeLocationAndPreferences()
    {
        using var database = new SqliteTestDatabase();
        var createdAt = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var user = new User(
            id: "user-james",
            username: "james",
            homeLocation: new GeoLocation(54.1509, -4.4821),
            preferences: new UserPreferences(
                preferredCategories: new[] { "Restaurant", "Café" },
                preferredPriceTiers: new[] { PriceTier.Budget, PriceTier.Moderate },
                preferredTags: new[] { "Seafood" },
                maxTravelDistanceKm: 25.5),
            createdAt: createdAt);

        using (var writeContext = database.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            await repository.AddAsync(user);
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var loaded = await new UserRepository(readContext).GetByIdAsync("user-james");

            Assert.NotNull(loaded);
            Assert.Equal("user-james", loaded!.Id);
            Assert.Equal("james", loaded.Username);
            Assert.Equal(createdAt, loaded.CreatedAt);
            Assert.Equal(new GeoLocation(54.1509, -4.4821), loaded.HomeLocation);

            var preferences = loaded.Preferences;
            Assert.Equal(new[] { "Café", "Restaurant" }, preferences.PreferredCategories.OrderBy(c => c));
            Assert.Equal(
                new[] { PriceTier.Budget, PriceTier.Moderate },
                preferences.PreferredPriceTiers.OrderBy(t => t));
            Assert.Equal(new[] { "seafood" }, preferences.PreferredTags);
            Assert.Equal(25.5, preferences.MaxTravelDistanceKm);

            // Case-insensitivity of the domain sets must survive the JSON round trip.
            Assert.Contains("SEAFOOD", preferences.PreferredTags);
        }
    }

    [Fact]
    public async Task User_RoundTrips_WithNullHomeLocationAndEmptyPreferences()
    {
        using var database = new SqliteTestDatabase();

        using (var writeContext = database.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            await repository.AddAsync(new User("user-anon", "anon"));
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var loaded = await new UserRepository(readContext).GetByIdAsync("user-anon");

            Assert.NotNull(loaded);
            Assert.Null(loaded!.HomeLocation);
            Assert.Empty(loaded.Preferences.PreferredCategories);
            Assert.Empty(loaded.Preferences.PreferredPriceTiers);
            Assert.Empty(loaded.Preferences.PreferredTags);
            Assert.Null(loaded.Preferences.MaxTravelDistanceKm);
        }
    }

    [Fact]
    public async Task UpdatePreferences_And_UpdateLocation_Are_Persisted()
    {
        using var database = new SqliteTestDatabase();

        using (var writeContext = database.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            await repository.AddAsync(new User("user-may", "may"));
            await writeContext.SaveChangesAsync();
        }

        using (var writeContext = database.CreateContext())
        {
            var repository = new UserRepository(writeContext);
            var user = await repository.GetByIdAsync("user-may");

            user!.UpdateLocation(new GeoLocation(54.2369, -4.5486));
            user.UpdatePreferences(new UserPreferences(
                preferredTags: new[] { "walkable" },
                maxTravelDistanceKm: 5));

            await repository.UpdateAsync(user);
            await writeContext.SaveChangesAsync();
        }

        using (var readContext = database.CreateContext())
        {
            var loaded = await new UserRepository(readContext).GetByUsernameAsync("may");

            Assert.NotNull(loaded);
            Assert.Equal(new GeoLocation(54.2369, -4.5486), loaded!.HomeLocation);
            Assert.Equal(new[] { "walkable" }, loaded.Preferences.PreferredTags);
            Assert.Equal(5, loaded.Preferences.MaxTravelDistanceKm);
        }
    }

    [Fact]
    public async Task Duplicate_Username_Throws()
    {
        using var database = new SqliteTestDatabase();

        using var context = database.CreateContext();
        var repository = new UserRepository(context);

        await repository.AddAsync(new User("user-1", "same-name"));
        await context.SaveChangesAsync();

        await repository.AddAsync(new User("user-2", "same-name"));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }
}
