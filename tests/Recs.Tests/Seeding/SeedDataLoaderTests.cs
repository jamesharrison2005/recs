using Microsoft.EntityFrameworkCore;
using Recs.Seeder;

namespace Recs.Tests.Seeding;

public class SeedDataLoaderTests
{
    private static async Task<SeedRunSummary> RunAsync(
        SqliteMigratedTestDatabase database,
        SeedOptions options)
    {
        await using var context = database.CreateContext();
        var loader = new SeedDataLoader(context, options);
        return await loader.RunAsync();
    }

    [Fact]
    public async Task Run_LoadsRestaurantsInTheConfiguredCity()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        // biz-001, 002, 003, 004 and 009 are Nashville restaurants with usable coordinates and
        // at least one review. biz-005 has no coordinates, biz-006 is out of range, biz-007 is
        // not a restaurant, and biz-008 is in Philadelphia. biz-010 is a valid restaurant but
        // no review references it, so the density filter drops it.
        Assert.Equal(5, await context.Items.CountAsync());

        var item = await context.Items.SingleAsync(i => i.Id == "yelp-biz-001");
        Assert.Equal("Hattie B's Chicken", item.Name);
        Assert.Equal("Chicken Wings", item.Category);
        Assert.Equal(36.1313, item.Location.Latitude, 4);
    }

    [Fact]
    public async Task Run_DropsItemsWithNoRatingsAtAll()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        // An item nobody rated cannot inform a recommendation, so it is not worth storing.
        Assert.Null(await context.Items.FindAsync("yelp-biz-010"));
        Assert.Equal(await context.Items.CountAsync(), summary.ItemsInDataset);
        Assert.Equal(await context.Users.CountAsync(), summary.UsersInDataset);
        Assert.Equal(await context.Ratings.CountAsync(), summary.RatingsInDataset);
        Assert.DoesNotContain(await context.Ratings.ToListAsync(), r => r.ItemId == "yelp-biz-010");
    }

    [Fact]
    public async Task Run_SkipsBusinessesWithUnusableCoordinates()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        Assert.Null(await context.Items.FindAsync("yelp-biz-005"));
        Assert.Null(await context.Items.FindAsync("yelp-biz-006"));
        Assert.Equal(1, summary.Skipped[SeedSkipReason.MissingCoordinates]);
        Assert.Equal(1, summary.Skipped[SeedSkipReason.InvalidCoordinates]);
    }

    [Fact]
    public async Task Run_ExcludesNonRestaurantsAndOtherCities()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        Assert.Null(await context.Items.FindAsync("yelp-biz-007"));
        Assert.Null(await context.Items.FindAsync("yelp-biz-008"));
    }

    [Fact]
    public async Task Run_CapsTheNumberOfBusinesses()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(
            database,
            SeedFixture.CreateOptions(o => o.MaxBusinesses = 2));

        await using var context = database.CreateContext();

        Assert.Equal(2, await context.Items.CountAsync());
        Assert.Equal(2, summary.ItemsInDataset);
    }

    [Fact]
    public async Task Run_ExcludesReviewsWithStarsOutsideTheDomainRange()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        // rev-016 (0 stars) and rev-017 (6 stars) target biz-002, which user-1 has an
        // otherwise-valid rating for.
        Assert.Equal(2, summary.Skipped[SeedSkipReason.OutOfRangeStars]);
        Assert.Equal(4.0f, (await context.Ratings.SingleAsync(r => r.ItemId == "yelp-biz-002"
            && r.UserId == "yelp-user-1")).Score);
    }

    [Fact]
    public async Task Run_ExcludesReviewsForBusinessesThatWereNotLoaded()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        Assert.DoesNotContain(await context.Ratings.ToListAsync(),
            r => r.ItemId is "yelp-biz-005" or "yelp-biz-006" or "yelp-biz-007" or "yelp-biz-008");
    }

    [Fact]
    public async Task Run_DropsUsersAndItemsBelowTheDensityThreshold()
    {
        using var database = new SqliteMigratedTestDatabase();

        // user-4 has a single rating and is dropped. biz-009's only two ratings belong to user-4
        // and user-5, so losing user-4 leaves it with one rating, below the threshold of two, and
        // it drops too. That cascades to user-5, who has no other ratings.
        var summary = await RunAsync(
            database,
            SeedFixture.CreateOptions(o =>
            {
                o.MinRatingsPerUser = 2;
                o.MinRatingsPerItem = 2;
            }));

        await using var context = database.CreateContext();

        Assert.Null(await context.Users.FindAsync("yelp-user-4"));
        Assert.Null(await context.Items.FindAsync("yelp-biz-009"));

        var ratings = await context.Ratings.ToListAsync();
        Assert.DoesNotContain(ratings, r => r.UserId == "yelp-user-4");
        Assert.DoesNotContain(ratings, r => r.ItemId == "yelp-biz-009");

        // Three entities dropped: user-4, then biz-009, then user-5.
        Assert.Equal(3, summary.Skipped[SeedSkipReason.DensityFiltered]);
    }

    [Fact]
    public async Task Run_DropsItemsBelowTheDensityThreshold()
    {
        using var database = new SqliteMigratedTestDatabase();

        // biz-009 has only two ratings, so a threshold of three drops it and its ratings.
        await RunAsync(
            database,
            SeedFixture.CreateOptions(o =>
            {
                o.MinRatingsPerUser = 1;
                o.MinRatingsPerItem = 3;
            }));

        await using var context = database.CreateContext();

        Assert.Null(await context.Items.FindAsync("yelp-biz-009"));
        Assert.DoesNotContain(await context.Ratings.ToListAsync(), r => r.ItemId == "yelp-biz-009");
    }

    [Fact]
    public async Task Run_InsertsOneRatingPerUserAndItemPair()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        // rev-010 and rev-011 are a second review by user-1 of biz-001 and biz-002 respectively.
        var ratings = await context.Ratings.Where(r => r.UserId == "yelp-user-1").ToListAsync();

        Assert.Equal(3, ratings.Count);
        Assert.Equal(ratings.Count, ratings.Select(r => r.ItemId).Distinct().Count());
        Assert.DoesNotContain(ratings, r => r.Score == 2.0f);
    }

    [Fact]
    public async Task Run_ReportsARepeatedUserItemPairAsASkipNotAsAnExistingRow()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        // rev-011 is user-1 reviewing biz-001 a second time. Nothing was in the database to
        // collide with, so it is a duplicate within the source file and must not be reported as a
        // row that was already there: doing so overstates the count by one per repeated pair.
        Assert.Equal(1, summary.Skipped[SeedSkipReason.DuplicateInFile]);
        Assert.Equal(0, summary.AlreadyInDatabase);
    }

    [Fact]
    public async Task Run_CreatesUsersWithIdDerivedUsernames()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        var user = await context.Users.SingleAsync(u => u.Id == "yelp-user-1");

        Assert.Equal("yelp-user-1", user.Username);
    }

    [Fact]
    public async Task Run_TruncatesOverlongTextToColumnLimits()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        Assert.All(await context.Items.ToListAsync(),
            i => Assert.True(i.Description!.Length <= 4000));
        Assert.All(await context.Ratings.ToListAsync(),
            r => Assert.True(r.Review is null or { Length: <= 4000 }));
    }

    [Fact]
    public async Task Run_ReportsAReadConsistentSummary()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        await using var context = database.CreateContext();

        Assert.Equal(10, summary.BusinessesRead);
        Assert.Equal(18, summary.ReviewsRead);
        Assert.Equal(await context.Items.CountAsync(), summary.ItemsInserted);
        Assert.Equal(await context.Users.CountAsync(), summary.UsersInserted);
        Assert.Equal(await context.Ratings.CountAsync(), summary.RatingsInserted);
    }

    [Fact]
    public async Task Run_AverageRatingsPerUserReflectsTheLoadedDataset()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(database, SeedFixture.CreateOptions());

        Assert.True(summary.AverageRatingsPerUser > 0);
        Assert.Equal(
            (double)summary.RatingsInDataset / summary.UsersInDataset,
            summary.AverageRatingsPerUser,
            6);
    }

    [Fact]
    public async Task Run_SecondTime_InsertsNothingAndIsIdempotent()
    {
        using var database = new SqliteMigratedTestDatabase();
        var options = SeedFixture.CreateOptions();

        var first = await RunAsync(database, options);

        await using (var context = database.CreateContext())
        {
            Assert.Equal(5, await context.Items.CountAsync());
        }

        var second = await RunAsync(database, options);

        await using var verifyContext = database.CreateContext();

        Assert.Equal(0, second.ItemsInserted);
        Assert.Equal(0, second.UsersInserted);
        Assert.Equal(0, second.RatingsInserted);

        // Row counts are unchanged and match the first run exactly.
        Assert.Equal(5, await verifyContext.Items.CountAsync());
        Assert.Equal(first.UsersInserted, await verifyContext.Users.CountAsync());
        Assert.Equal(first.RatingsInserted, await verifyContext.Ratings.CountAsync());

        // Nothing was skipped as malformed or density filtered on a re-run over unchanged input. The
        // two out-of-range-star rows are the same rows the first run skipped, because the tally
        // pass reads the whole file both times. rev-011 is not a repeat this time: the rating it
        // resolves to is in the database, so it is recognised as already present instead.
        Assert.Equal(0, second.Skipped[SeedSkipReason.DensityFiltered]);
        Assert.Equal(2, second.Skipped[SeedSkipReason.OutOfRangeStars]);
        Assert.Equal(0, second.Skipped[SeedSkipReason.DuplicateInFile]);

        // Every user, item and rating the second run offered was recognised as already present.
        Assert.Equal(5 + 5 + 12, second.AlreadyInDatabase);

        // The dataset is reported in full on a run that inserted nothing, so the summary still
        // describes what is actually loaded.
        Assert.Equal(first.ItemsInDataset, second.ItemsInDataset);
        Assert.Equal(first.UsersInDataset, second.UsersInDataset);
        Assert.Equal(first.RatingsInDataset, second.RatingsInDataset);
        Assert.Equal(first.AverageRatingsPerUser, second.AverageRatingsPerUser, 6);
    }

    [Fact]
    public async Task Run_SecondTimeWithLargerCap_AddsOnlyTheNewRows()
    {
        using var database = new SqliteMigratedTestDatabase();

        var narrow = SeedFixture.CreateOptions(o =>
        {
            o.MaxBusinesses = 3;
            o.MinRatingsPerUser = 1;
            o.MinRatingsPerItem = 1;
        });

        var first = await RunAsync(database, narrow);

        var wider = SeedFixture.CreateOptions(o =>
        {
            o.MaxBusinesses = 6;
            o.MinRatingsPerUser = 1;
            o.MinRatingsPerItem = 1;
        });

        var second = await RunAsync(database, wider);

        await using var context = database.CreateContext();

        Assert.Equal(3, first.ItemsInserted);

        // Raising the cap re-reads the reviews, so the wider run reaches biz-004 and biz-009.
        // It must not re-insert the three items the first run already wrote.
        Assert.Equal(2, second.ItemsInserted);
        Assert.Equal(5, await context.Items.CountAsync());

        // The rows the first run wrote are recognised as already present rather than re-inserted.
        // The counter spans items, users and ratings together, so it is only asserted to be
        // non-zero here; Run_SecondTime_InsertsNothingAndIsIdempotent pins down its exact value
        // on a run where every row is a duplicate.
        Assert.True(second.AlreadyInDatabase > 0);
    }

    [Fact]
    public async Task Run_CapsTheNumberOfReviews()
    {
        using var database = new SqliteMigratedTestDatabase();

        var summary = await RunAsync(
            database,
            SeedFixture.CreateOptions(o => o.MaxReviews = 4));

        await using var context = database.CreateContext();

        Assert.Equal(4, await context.Ratings.CountAsync());
        Assert.Equal(4, summary.RatingsInserted);
    }

    [Fact]
    public async Task Run_CapsTheNumberOfUsers()
    {
        using var database = new SqliteMigratedTestDatabase();

        await RunAsync(database, SeedFixture.CreateOptions(o => o.MaxUsers = 2));

        await using var context = database.CreateContext();

        Assert.Equal(2, await context.Users.CountAsync());

        // No rating may reference a user that was not inserted, or the foreign key fails.
        var userIds = await context.Users.Select(u => u.Id).ToListAsync();
        var ratingUserIds = await context.Ratings.Select(r => r.UserId).Distinct().ToListAsync();

        Assert.All(ratingUserIds, id => Assert.Contains(id, userIds));
    }
}
