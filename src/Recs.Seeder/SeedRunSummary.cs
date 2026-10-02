using System.Text;

namespace Recs.Seeder;

/// <summary>
/// What a seed run did. Printed at the end so a surprising result set is explainable without
/// re-running the loader with a debugger attached.
/// </summary>
public sealed class SeedRunSummary
{
    public int BusinessesRead { get; set; }
    public int ReviewsRead { get; set; }

    public int UsersInserted { get; set; }
    public int ItemsInserted { get; set; }
    public int RatingsInserted { get; set; }

    /// <summary>Distinct users surviving the density filter, whether or not they were new.</summary>
    public int UsersInDataset { get; set; }

    /// <summary>Distinct items surviving the density filter, whether or not they were new.</summary>
    public int ItemsInDataset { get; set; }

    /// <summary>Ratings in the dataset that survived the density filter.</summary>
    public int RatingsInDataset { get; set; }

    public SkipCounter Skipped { get; } = new();

    /// <summary>
    /// Rows that were already present and therefore not inserted again. Tracked separately from
    /// <see cref="Skipped"/> because it is not a data quality problem: it is what a re-run over
    /// unchanged input is supposed to produce, and lumping it in with malformed rows hides which
    /// of the two actually happened.
    /// </summary>
    public long AlreadyInDatabase { get; private set; }

    /// <summary>
    /// Counts a row that was already present. The same overload shape as
    /// <see cref="SkipCounter.Count"/> so call sites read consistently.
    /// </summary>
    public void CountAlreadyInDatabase(long amount = 1) => AlreadyInDatabase += amount;

    /// <summary>
    /// Average ratings per user across the loaded dataset. Reported against the whole dataset
    /// rather than only this run's inserts, so a second run still shows the real density.
    /// </summary>
    public double AverageRatingsPerUser
        => UsersInDataset == 0 ? 0 : (double)RatingsInDataset / UsersInDataset;

    public string Render()
    {
        var sb = new StringBuilder();

        sb.AppendLine();
        sb.AppendLine("=== Seed run summary ===");
        sb.AppendLine($"  Read:     {BusinessesRead} businesses, {ReviewsRead} reviews");
        sb.AppendLine("  Dataset:");
        sb.AppendLine($"    users     {UsersInDataset} ({DescribeInserted(this, UsersInserted)})");
        sb.AppendLine($"    items     {ItemsInDataset} ({DescribeInserted(this, ItemsInserted)})");
        sb.AppendLine($"    ratings   {RatingsInDataset} ({DescribeInserted(this, RatingsInserted)})");
        sb.AppendLine($"    avg ratings per user: {AverageRatingsPerUser:F2}");

        sb.AppendLine($"  Already in database: {AlreadyInDatabase}");
        sb.AppendLine($"  Skipped:  {Skipped.Total} total");
        foreach (var reason in Enum.GetValues<SeedSkipReason>())
        {
            var count = Skipped[reason];
            if (count > 0)
                sb.AppendLine($"    {Describe(reason),-24} {count}");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Phrases an insert count for the summary. Spelled out rather than collapsed to a bare number
    /// so that a run which inserted nothing reads as an outcome instead of looking like a stall.
    /// A run that inserted nothing and saw no rows at all is described as having started rather
    /// than as finding everything present: nothing was found, because nothing was read.
    /// </summary>
    private static string DescribeInserted(SeedRunSummary summary, int inserted) => inserted switch
    {
        > 0 => $"{inserted} inserted this run",
        _ when summary.BusinessesRead == 0 && summary.ReviewsRead == 0 => "run did not start",
        _ => "inserted 0, already present"
    };

    private static string Describe(SeedSkipReason reason) => reason switch
    {
        SeedSkipReason.Malformed => "malformed line",
        SeedSkipReason.MissingCoordinates => "missing coordinates",
        SeedSkipReason.InvalidCoordinates => "invalid coordinates",
        SeedSkipReason.OutOfRangeStars => "stars out of range",
        SeedSkipReason.DensityFiltered => "below density filter",
        SeedSkipReason.UnknownUserOrItem => "unknown user or item",
        SeedSkipReason.DuplicateInFile => "repeated user/item pair",
        _ => reason.ToString()
    };
}
