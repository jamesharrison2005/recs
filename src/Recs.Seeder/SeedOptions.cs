using Microsoft.Extensions.Configuration;

namespace Recs.Seeder;

/// <summary>
/// Settings for a seed run, read from the <c>Seed</c> configuration section and overridden by
/// command line flags, which are the higher priority source.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Path to the Yelp business JSON file (line-delimited).</summary>
    public string BusinessFilePath { get; set; } = string.Empty;

    /// <summary>Path to the Yelp review JSON file (line-delimited).</summary>
    public string ReviewFilePath { get; set; } = string.Empty;

    /// <summary>
    /// Only keep businesses whose Yelp <c>city</c> matches, case-insensitively. Yelp stores the
    /// city name alone, so this is a bare name like "Nashville", not "Nashville, TN".
    /// </summary>
    public string City { get; set; } = "Nashville";

    /// <summary>
    /// Optional second filter. When set, businesses must match both city and state. Yelp's
    /// <c>state</c> column holds the abbreviation, so this is "TN".
    /// </summary>
    public string? State { get; set; } = "TN";

    public int MaxBusinesses { get; set; } = 5_000;

    public int MaxUsers { get; set; } = 20_000;

    public int MaxReviews { get; set; } = 200_000;

    /// <summary>Users with fewer ratings than this are dropped, along with their ratings.</summary>
    public int MinRatingsPerUser { get; set; } = 5;

    /// <summary>Items with fewer ratings than this are dropped, along with their ratings.</summary>
    public int MinRatingsPerItem { get; set; } = 5;

    public int BatchSize { get; set; } = 1_000;

    /// <summary>
    /// Reads settings from the <c>Seed</c> section using the indexer, matching how
    /// <c>DependencyInjection.AddInfrastructure</c> reads its connection string. This avoids a
    /// dependency on <c>Microsoft.Extensions.Configuration.Binder</c>.
    /// </summary>
    public static SeedOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        return new SeedOptions
        {
            BusinessFilePath = Read(section, nameof(BusinessFilePath), string.Empty),
            ReviewFilePath = Read(section, nameof(ReviewFilePath), string.Empty),
            City = Read(section, nameof(City), "Nashville"),
            State = Read(section, nameof(State), string.Empty) is { Length: > 0 } state ? state : "TN",
            MaxBusinesses = ReadInt(section, nameof(MaxBusinesses), 5_000),
            MaxUsers = ReadInt(section, nameof(MaxUsers), 20_000),
            MaxReviews = ReadInt(section, nameof(MaxReviews), 200_000),
            MinRatingsPerUser = ReadInt(section, nameof(MinRatingsPerUser), 5),
            MinRatingsPerItem = ReadInt(section, nameof(MinRatingsPerItem), 5),
            BatchSize = ReadInt(section, nameof(BatchSize), 1_000)
        };
    }

    private static string Read(IConfiguration section, string key, string fallback)
        => section[key] is { Length: > 0 } value ? value : fallback;

    private static int ReadInt(IConfiguration section, string key, int fallback)
        => int.TryParse(section[key], out var value) ? value : fallback;

    /// <summary>
    /// Validates the options, returning a message describing the first problem found.
    /// Returns null when the options are usable.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(BusinessFilePath))
            return "Business file path is required (--businesses or Seed:BusinessFilePath).";

        if (string.IsNullOrWhiteSpace(ReviewFilePath))
            return "Review file path is required (--reviews or Seed:ReviewFilePath).";

        if (string.IsNullOrWhiteSpace(City))
            return "City is required (--city or Seed:City).";

        if (MaxBusinesses <= 0)
            return "MaxBusinesses must be greater than zero.";

        if (MaxUsers <= 0)
            return "MaxUsers must be greater than zero.";

        if (MaxReviews <= 0)
            return "MaxReviews must be greater than zero.";

        if (MinRatingsPerUser < 0)
            return "MinRatingsPerUser cannot be negative.";

        if (MinRatingsPerItem < 0)
            return "MinRatingsPerItem cannot be negative.";

        if (BatchSize <= 0)
            return "BatchSize must be greater than zero.";

        return null;
    }
}
