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
        sb.AppendLine($"    users     {UsersInDataset} ({UsersInserted} inserted this run)");
        sb.AppendLine($"    items     {ItemsInDataset} ({ItemsInserted} inserted this run)");
        sb.AppendLine($"    ratings   {RatingsInDataset} ({RatingsInserted} inserted this run)");
        sb.AppendLine($"    avg ratings per user: {AverageRatingsPerUser:F2}");

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

    private static string Describe(SeedSkipReason reason) => reason switch
    {
        SeedSkipReason.Malformed => "malformed line",
        SeedSkipReason.MissingCoordinates => "missing coordinates",
        SeedSkipReason.InvalidCoordinates => "invalid coordinates",
        SeedSkipReason.OutOfRangeStars => "stars out of range",
        SeedSkipReason.DensityFiltered => "below density filter",
        SeedSkipReason.UnknownUserOrItem => "unknown user or item",
        SeedSkipReason.DuplicateSkipped => "already in database",
        _ => reason.ToString()
    };
}
