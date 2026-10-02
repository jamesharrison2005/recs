using Microsoft.EntityFrameworkCore;
using Recs.Domain.Entities;
using Recs.Infrastructure.Persistence;
using Recs.Seeder.Yelp;

namespace Recs.Seeder;

/// <summary>
/// Loads a Yelp subset into the database as <see cref="User"/>, <see cref="Item"/> and
/// <see cref="Rating"/> rows.
/// <para>
/// The review file is too large to filter in a single streaming pass: keeping only users and items
/// with enough ratings depends on counts that are not known until the file has been read. Rather
/// than buffer millions of candidate ratings, the loader makes two streaming passes over the review
/// file and tallies only per-user and per-item counts in between. Those dictionaries are bounded by
/// the size of the subset, not the size of the file, so memory stays flat.
/// </para>
/// </summary>
public sealed class SeedDataLoader
{
    /// <summary>
    /// Rows handled between progress reports. A full pass over the review file is minutes of
    /// silent work, so the passes report progress rather than looking hung.
    /// </summary>
    private const long ProgressInterval = 500_000;

    private readonly RecsDbContext _context;
    private readonly SeedOptions _options;
    private readonly SeedRunSummary _summary = new();
    private readonly Action<string> _log;

    /// <summary>Row count at the last progress report, so reports fire on the interval alone.</summary>
    private long _lastProgressAt;

    public SeedDataLoader(RecsDbContext context, SeedOptions options, Action<string>? log = null)
    {
        _context = context;
        _options = options;
        _log = log ?? (_ => { });
    }

    public SeedRunSummary Summary => _summary;

    public async Task<SeedRunSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        // The API does not migrate on startup, so the seeder owns bringing the file up to date.
        // Migrate is safe to call against an already-migrated database.
        await _context.Database.MigrateAsync(cancellationToken);

        // Pass 1 over the businesses produces candidate items. They are held in memory but not
        // yet inserted: the density filter may still drop some of them, and an item that ends up
        // with no ratings should not reach the database. The allow-list is a separate set because
        // the filter removes from candidates in place.
        var candidates = await LoadItemCandidatesAsync(cancellationToken);
        var candidateIds = candidates.Keys.ToHashSet(StringComparer.Ordinal);

        // Two streaming passes over the reviews: one to tally counts, one to insert.
        var counts = await TallyRatingsAsync(candidateIds, cancellationToken);
        var survivingUsers = ApplyDensityFilter(counts, candidates);

        // Insert order is items, then users, then ratings, because ratings carry foreign keys
        // to both. Items are written first because they are already in hand. The review pass reads
        // the survivors, so a density-dropped item is excluded without a second check.
        await InsertItemsAsync(candidates, cancellationToken);
        await InsertUsersAndRatingsAsync(
            candidates.Keys.ToHashSet(StringComparer.Ordinal),
            survivingUsers,
            cancellationToken);

        // The density filter fixed the shape of the dataset before either insert ran, but which
        // rows reach the database is only settled once inserts are done: a user pushed past
        // MaxUsers is dropped here, and its ratings are dropped with it. Taking the counts off
        // the database is what keeps a second run from reporting an empty dataset for rows it
        // deliberately left alone.
        await PopulateDatasetCountsAsync(cancellationToken);

        return _summary;
    }

    /// <summary>
    /// Records what the loaded dataset now contains, straight from the database. The alternative,
    /// counting rows as they are offered for insert, reports the candidate set rather than the
    /// loaded one, and reports nothing at all on a run where everything was already present.
    /// </summary>
    private async Task PopulateDatasetCountsAsync(CancellationToken cancellationToken)
    {
        _summary.UsersInDataset = await _context.Users.CountAsync(cancellationToken);
        _summary.ItemsInDataset = await _context.Items.CountAsync(cancellationToken);
        _summary.RatingsInDataset = await _context.Ratings.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Streams the business file once, keeping restaurants in the configured city that have usable
    /// coordinates. Candidates are buffered in memory because they are capped by
    /// <see cref="SeedOptions.MaxBusinesses"/>, which is a small number by design.
    /// </summary>
    private async Task<Dictionary<string, Item>> LoadItemCandidatesAsync(CancellationToken cancellationToken)
    {
        var candidates = new Dictionary<string, Item>(_options.MaxBusinesses, StringComparer.Ordinal);

        var malformed = await YelpJsonStreamReader.ForEachAsync<YelpBusiness>(
            _options.BusinessFilePath,
            (business, ct) =>
            {
                _summary.BusinessesRead++;
                Progress();

                if (!BusinessMapper.IsRestaurant(business))
                    return Task.CompletedTask;

                if (!BusinessMapper.MatchesLocation(business, _options.City, _options.State))
                    return Task.CompletedTask;

                // The cap is checked before mapping so the count stays bounded while the rest of
                // the file is still streamed for its skip reasons.
                if (candidates.Count >= _options.MaxBusinesses)
                    return Task.CompletedTask;

                var result = BusinessMapper.Map(business);
                if (result.SkipReason is { } reason)
                {
                    _summary.Skipped.Count(reason);
                    return Task.CompletedTask;
                }

                candidates[result.ItemId] = result.Item!;
                return Task.CompletedTask;
            },
            cancellationToken);

        if (malformed > 0)
            _summary.Skipped.Count(SeedSkipReason.Malformed, malformed);

        _log($"Filtered to {candidates.Count} restaurants in \"{_options.City}\".");

        return candidates;
    }

    /// <summary>
    /// Inserts the candidate items that survived the density filter, skipping any already present.
    /// </summary>
    private async Task InsertItemsAsync(
        Dictionary<string, Item> candidates,
        CancellationToken cancellationToken)
    {
        var existingItemIds = await _context.Items
            .Select(i => i.Id)
            .ToHashSetAsync(cancellationToken);

        var buffer = new List<Item>(_options.BatchSize);
        foreach (var item in candidates.Values)
        {
            if (existingItemIds.Contains(item.Id))
            {
                _summary.CountAlreadyInDatabase();
                continue;
            }

            buffer.Add(item);
            if (buffer.Count >= _options.BatchSize)
                _summary.ItemsInserted += await FlushAsync(_context.Items, buffer, cancellationToken);
        }

        _summary.ItemsInserted += await FlushAsync(_context.Items, buffer, cancellationToken);
        _log(Reported("items", _summary.ItemsInserted));
    }

    /// <summary>
    /// First review pass. Tallies ratings per user and per item, ignoring any review for a
    /// business that was not kept.
    /// </summary>
    private async Task<RatingCounts> TallyRatingsAsync(
        IReadOnlySet<string> itemIds,
        CancellationToken cancellationToken)
    {
        var counts = new RatingCounts();

        var malformed = await YelpJsonStreamReader.ForEachAsync<YelpReview>(
            _options.ReviewFilePath,
            (review, ct) =>
            {
                _summary.ReviewsRead++;
                Progress();

                if (string.IsNullOrWhiteSpace(review.UserId) || string.IsNullOrWhiteSpace(review.BusinessId))
                {
                    _summary.Skipped.Count(SeedSkipReason.Malformed);
                    return Task.CompletedTask;
                }

                if (review.Stars is null ||
                    review.Stars < Rating.MinScore ||
                    review.Stars > Rating.MaxScore)
                {
                    _summary.Skipped.Count(SeedSkipReason.OutOfRangeStars);
                    return Task.CompletedTask;
                }

                var itemId = BusinessMapper.ToItemId(review.BusinessId.Trim());
                if (!itemIds.Contains(itemId))
                    return Task.CompletedTask;

                var userId = ReviewMapper.ToUserId(review.UserId.Trim());

                if (!counts.Users.TryGetValue(userId, out var seen))
                {
                    seen = new UserTally();
                    counts.Users[userId] = seen;
                }

                // One user/item pair yields exactly one Rating, so a repeat of the same business
                // is recorded as seen rather than counted twice.
                if (!seen.Items.Add(itemId))
                    return Task.CompletedTask;

                if (!counts.Items.TryGetValue(itemId, out var itemTally))
                {
                    itemTally = new ItemTally();
                    counts.Items[itemId] = itemTally;
                }

                itemTally.Users.Add(userId);
                counts.Total++;

                return Task.CompletedTask;
            },
            cancellationToken);

        if (malformed > 0)
            _summary.Skipped.Count(SeedSkipReason.Malformed, malformed);

        _log($"Found {counts.Total} ratings across {counts.Users.Count} users and {counts.Items.Count} items.");

        return counts;
    }

    /// <summary>
    /// Drops users and items below the minimum rating count, returning the surviving user keys and
    /// removing dropped items from <paramref name="candidates"/> so they are never inserted.
    /// Ratings involving a dropped user or item cannot be kept: they would violate a foreign key,
    /// and their presence is exactly the sparsity the filter exists to remove.
    /// </summary>
    private HashSet<string> ApplyDensityFilter(
        RatingCounts counts,
        Dictionary<string, Item> candidates)
    {
        var users = counts.Users.Keys.ToHashSet(StringComparer.Ordinal);
        var items = counts.Items.Keys.ToHashSet(StringComparer.Ordinal);

        // The two thresholds constrain each other: dropping a user removes one of an item's
        // ratings, which can push that item below MinRatingsPerItem, which in turn removes more
        // ratings and can drop further users. So iterate until neither set changes. In practice
        // this settles in two or three rounds; in the worst case a round drops nothing and the
        // loop ends.
        //
        // Every check counts members of the current surviving sets rather than the raw tallies.
        // That is what makes it correct when the two thresholds disagree: an item whose raters
        // are themselves all on the boundary must fall, even though its raw count met the bar.
        while (true)
        {
            var previousUserCount = users.Count;
            var previousItemCount = items.Count;

            var thinUsers = users
                .Where(userId => counts.Users[userId].Items.Count(id => items.Contains(id))
                                 < _options.MinRatingsPerUser)
                .ToList();

            foreach (var userId in thinUsers)
            {
                users.Remove(userId);
                items.RemoveWhere(id => counts.Items[id].Users.All(u => !users.Contains(u)));
            }

            // Recomputed from the users that survived the pass above: dropping a user can leave
            // an item with too few raters, and that has to be visible in this same round.
            var thinItems = items
                .Where(itemId => counts.Items[itemId].Users.Count(users.Contains) < _options.MinRatingsPerItem)
                .ToList();

            foreach (var itemId in thinItems)
            {
                items.Remove(itemId);
                users.RemoveWhere(userId => counts.Users[userId].Items.Count(items.Contains) == 0);
            }

            if (users.Count == previousUserCount && items.Count == previousItemCount)
                break;
        }

        // A drop is an entity that the raw tallies knew about but the survivors do not.
        var dropped = (counts.Users.Count - users.Count) + (counts.Items.Count - items.Count);
        _summary.Skipped.Count(SeedSkipReason.DensityFiltered, dropped);

        // An item that never reached the density threshold must not be inserted, and must not
        // be counted in the dataset. Removing it here means the insert pass cannot see it.
        foreach (var itemId in candidates.Keys.Where(id => !items.Contains(id)).ToList())
            candidates.Remove(itemId);

        _log($"Density filter (min {_options.MinRatingsPerUser}/user, {_options.MinRatingsPerItem}/item) " +
             $"kept {users.Count} users and {candidates.Count} items.");

        return users;
    }

    /// <summary>
    /// Second review pass. Inserts users before the ratings that reference them, streaming from
    /// the review file again so the pass never materialises the whole review set.
    /// </summary>
    private async Task InsertUsersAndRatingsAsync(
        IReadOnlySet<string> itemIds,
        HashSet<string> survivingUsers,
        CancellationToken cancellationToken)
    {
        var existingUserIds = await _context.Users
            .Select(u => u.Id)
            .ToHashSetAsync(cancellationToken);

        var existingRatingIds = await _context.Ratings
            .Select(r => r.Id)
            .ToHashSetAsync(cancellationToken);

        var userBuffer = new List<User>(_options.BatchSize);
        var ratingBuffer = new List<Rating>(_options.BatchSize);
        var seenUsers = new HashSet<string>(StringComparer.Ordinal);
        var seenRatings = new HashSet<string>(StringComparer.Ordinal);

        var malformed = await YelpJsonStreamReader.ForEachAsync<YelpReview>(
            _options.ReviewFilePath,
            async (review, ct) =>
            {
                // This pass does not accumulate ReviewsRead: the tally pass already counted every
                // line, and counting again would double the figure the summary reports.
                Progress();

                if (string.IsNullOrWhiteSpace(review.UserId) || string.IsNullOrWhiteSpace(review.BusinessId))
                    return;

                // itemIds is the post-density-filter set, so an item dropped earlier is
                // excluded here without needing a second check.
                var itemId = BusinessMapper.ToItemId(review.BusinessId.Trim());
                if (!itemIds.Contains(itemId))
                    return;

                var userId = ReviewMapper.ToUserId(review.UserId.Trim());

                // survivingUsers is read here and written on the other side of this removal, so
                // a user pushed past MaxUsers has their remaining reviews skipped too.
                if (!survivingUsers.Contains(userId))
                    return;

                // Register the user on first sight. Username mirrors the Yelp user id rather
                // than the display name, because display names are not unique and the column
                // carries a unique index.
                if (seenUsers.Add(userId))
                {
                    if (existingUserIds.Contains(userId))
                    {
                        _summary.CountAlreadyInDatabase();
                    }
                    else if (seenUsers.Count > _options.MaxUsers)
                    {
                        // Over the cap: this user and all their ratings are left out.
                        survivingUsers.Remove(userId);
                        _summary.Skipped.Count(SeedSkipReason.DensityFiltered);
                    }
                    else
                    {
                        userBuffer.Add(new User(userId, userId));
                    }
                }

                if (seenUsers.Count > _options.MaxUsers)
                    return;

                // Skip reasons are counted during the tally pass only. Counting them again here
                // would report each bad row twice, since both passes read the same rows.
                if (ReviewMapper.Map(review, itemId).Rating is not { } rating)
                    return;

                if (existingRatingIds.Contains(rating.Id))
                {
                    // Removed from the in-run set as well, because it is already persisted and so
                    // was never staged. Without this, a second review by the same user of the same
                    // business later in the file would be rejected by seenRatings and miscounted as
                    // "already in database" a second time, overstating it by one per repeated pair.
                    seenRatings.Remove(rating.Id);
                    _summary.CountAlreadyInDatabase();
                    return;
                }

                if (!seenRatings.Add(rating.Id))
                {
                    // A repeat of the same business within this run. Distinct from a row already in
                    // the database: nothing was skipped for the database's sake, and reporting it
                    // as AlreadyInDatabase would imply a lookup that never happened.
                    _summary.Skipped.Count(SeedSkipReason.DuplicateInFile);
                    return;
                }

                if (_summary.RatingsInserted + ratingBuffer.Count >= _options.MaxReviews)
                    return;

                ratingBuffer.Add(rating);
                if (ratingBuffer.Count >= _options.BatchSize)
                {
                    // Users must be committed before the ratings that reference them, so the user
                    // buffer is always flushed first at a shared batch boundary.
                    _summary.UsersInserted += await FlushAsync(_context.Users, userBuffer, ct);
                    _summary.RatingsInserted += await FlushAsync(_context.Ratings, ratingBuffer, ct);
                }
            },
            cancellationToken);

        if (malformed > 0)
            _summary.Skipped.Count(SeedSkipReason.Malformed, malformed);

        // Same ordering rule for the final partial batches.
        _summary.UsersInserted += await FlushAsync(_context.Users, userBuffer, cancellationToken);
        _summary.RatingsInserted += await FlushAsync(_context.Ratings, ratingBuffer, cancellationToken);

        _log($"{Reported("users", _summary.UsersInserted)}, {Reported("ratings", _summary.RatingsInserted)}.");
    }

    /// <summary>
    /// Phrases an insert count so a step that found nothing new still reports a result. "Inserted
    /// 0 items." reads like the run stopped there, which is precisely the wrong impression when
    /// every candidate was already present.
    /// </summary>
    private static string Reported(string entity, int inserted)
        => inserted == 0
            ? $"inserted 0 {entity}, all already in database"
            : $"inserted {inserted} {entity}";

    /// <summary>
    /// Logs the number of rows handled so far, every <see cref="ProgressInterval"/> rows. Tracking
    /// both business and review rows under one counter keeps the reporter to a single field; the
    /// phase name in the message says which pass is running.
    /// </summary>
    private void Progress(string phase = "rows")
    {
        var seen = _summary.BusinessesRead + _summary.ReviewsRead;

        if (seen < _lastProgressAt + ProgressInterval)
            return;

        _lastProgressAt = seen;
        _log($"  ... {seen:N0} {phase} processed");
    }

    /// <summary>
    /// Adds a batch and saves it, then detaches everything so the change tracker does not grow
    /// without bound across a long run.
    /// </summary>
    private async Task<int> FlushAsync<TEntity>(
        DbSet<TEntity> set,
        List<TEntity> buffer,
        CancellationToken cancellationToken) where TEntity : class
    {
        if (buffer.Count == 0)
            return 0;

        var count = buffer.Count;
        set.AddRange(buffer);
        await _context.SaveChangesAsync(cancellationToken);

        _context.ChangeTracker.Clear();
        buffer.Clear();

        return count;
    }

    /// <summary>Per-user and per-item rating tallies, held between the two review passes.</summary>
    private sealed class RatingCounts
    {
        public Dictionary<string, UserTally> Users { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ItemTally> Items { get; } = new(StringComparer.Ordinal);

        public int Total { get; set; }
    }

    /// <summary>One user's distinct rated items, kept so both count and members are available.</summary>
    private sealed class UserTally
    {
        public HashSet<string> Items { get; } = new(StringComparer.Ordinal);

        public int Count => Items.Count;
    }

    /// <summary>One item's distinct raters, kept so both count and members are available.</summary>
    private sealed class ItemTally
    {
        public HashSet<string> Users { get; } = new(StringComparer.Ordinal);

        public int Count => Users.Count;
    }
}
