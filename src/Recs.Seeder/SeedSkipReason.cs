namespace Recs.Seeder;

/// <summary>
/// Why a source row was not turned into a domain entity. Reported per-reason in the run summary
/// so a thin result set is explainable rather than mysterious.
/// </summary>
public enum SeedSkipReason
{
    /// <summary>The line was not valid JSON for the expected shape.</summary>
    Malformed,

    /// <summary>Latitude or longitude was absent. Cannot build a <c>GeoLocation</c>.</summary>
    MissingCoordinates,

    /// <summary>Coordinates were present but outside the valid range, or NaN.</summary>
    InvalidCoordinates,

    /// <summary>Stars fell outside the 1.0-5.0 range the <c>Rating</c> domain accepts.</summary>
    OutOfRangeStars,

    /// <summary>The user or item did not meet the minimum-ratings density filter.</summary>
    DensityFiltered,

    /// <summary>The rating referenced a user or item that was never loaded.</summary>
    UnknownUserOrItem,

    /// <summary>A record with this id was already in the database.</summary>
    DuplicateSkipped
}

/// <summary>Accumulates skip counts by reason.</summary>
public sealed class SkipCounter
{
    private readonly Dictionary<SeedSkipReason, long> _counts = new();

    public void Count(SeedSkipReason reason, long amount = 1)
        => _counts[reason] = _counts.GetValueOrDefault(reason) + amount;

    public long this[SeedSkipReason reason] => _counts.GetValueOrDefault(reason);

    public IReadOnlyDictionary<SeedSkipReason, long> Counts => _counts;

    /// <summary>Total rows skipped across all reasons.</summary>
    public long Total => _counts.Values.Sum();
}
