using Recs.Domain.Enums;

namespace Recs.Domain.ValueObjects;

public sealed record UserPreferences
{
    public static UserPreferences Empty { get; } = new();

    public IReadOnlySet<string> PreferredCategories { get; init; }
    public IReadOnlySet<PriceTier> PreferredPriceTiers { get; init; }
    public IReadOnlySet<string> PreferredTags { get; init; }
    public double? MaxTravelDistanceKm { get; init; }

    public UserPreferences(
        IEnumerable<string>? preferredCategories = null,
        IEnumerable<PriceTier>? preferredPriceTiers = null,
        IEnumerable<string>? preferredTags = null,
        double? maxTravelDistanceKm = null)
    {
        if (maxTravelDistanceKm is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTravelDistanceKm), "Max travel distance must be greater than zero.");
        }

        PreferredCategories = preferredCategories != null
            ? new HashSet<string>(preferredCategories.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        PreferredPriceTiers = preferredPriceTiers != null
            ? new HashSet<PriceTier>(preferredPriceTiers)
            : new HashSet<PriceTier>();

        PreferredTags = preferredTags != null
            ? new HashSet<string>(preferredTags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        MaxTravelDistanceKm = maxTravelDistanceKm;
    }
}
