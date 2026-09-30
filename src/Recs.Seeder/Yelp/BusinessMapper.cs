using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;

namespace Recs.Seeder.Yelp;

/// <summary>
/// Outcome of mapping one Yelp business line: either an <see cref="Item"/> or the reason it was
/// dropped. Modelled as a result type because both outcomes are ordinary, not exceptional.
/// </summary>
public readonly record struct BusinessMapResult(Item? Item, string ItemId, SeedSkipReason? SkipReason)
{
    public static BusinessMapResult Success(Item item) => new(item, item.Id, null);

    public static BusinessMapResult Skipped(string itemId, SeedSkipReason reason) => new(null, itemId, reason);
}

/// <summary>
/// Maps a Yelp business to a domain <see cref="Item"/>.
/// </summary>
public static class BusinessMapper
{
    /// <summary>The category Yelp uses to mark a business as a restaurant.</summary>
    public const string RestaurantCategory = "Restaurants";

    /// <summary>Matches the <c>Category</c> column's max length.</summary>
    private const int CategoryMaxLength = 128;

    /// <summary>Matches the <c>Name</c> column's max length.</summary>
    private const int NameMaxLength = 256;

    /// <summary>Matches the <c>Description</c> column's max length.</summary>
    public const int DescriptionMaxLength = 4000;

    /// <summary>True when the business is tagged as a restaurant.</summary>
    public static bool IsRestaurant(YelpBusiness business)
        => SplitCategories(business.Categories)
            .Contains(RestaurantCategory, StringComparer.OrdinalIgnoreCase);

    /// <summary>True when the business passes the city filter and, if set, the state filter.</summary>
    public static bool MatchesLocation(YelpBusiness business, string city, string? state)
    {
        if (!string.Equals(business.City?.Trim(), city.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(state))
            return true;

        return string.Equals(business.State?.Trim(), state.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds the deterministic id for a business. Derived purely from the Yelp id so that
    /// re-running the loader recognises rows it already inserted.
    /// </summary>
    public static string ToItemId(string businessId) => $"yelp-{businessId}";

    /// <summary>
    /// Maps a business to a restaurant <see cref="Item"/>, or reports why it could not be.
    /// </summary>
    public static BusinessMapResult Map(YelpBusiness business)
    {
        if (string.IsNullOrWhiteSpace(business.BusinessId))
            return BusinessMapResult.Skipped(string.Empty, SeedSkipReason.Malformed);

        var itemId = ToItemId(business.BusinessId.Trim());

        if (business.Latitude is null || business.Longitude is null)
            return BusinessMapResult.Skipped(itemId, SeedSkipReason.MissingCoordinates);

        GeoLocation location;
        try
        {
            // The GeoLocation constructor throws on out-of-range and non-finite values, and
            // default(GeoLocation) is a silently valid (0, 0), so the input is never defaulted
            // into existence here.
            location = new GeoLocation(business.Latitude.Value, business.Longitude.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BusinessMapResult.Skipped(itemId, SeedSkipReason.InvalidCoordinates);
        }

        var name = Truncate(
            string.IsNullOrWhiteSpace(business.Name) ? "Unknown restaurant" : business.Name.Trim(),
            NameMaxLength);

        var category = Truncate(ResolveCategory(business.Categories), CategoryMaxLength);

        // The domain lower-cases, trims and de-duplicates tags on construction.
        var tags = SplitCategories(business.Categories);

        var description = Truncate(BuildDescription(business), DescriptionMaxLength);

        var item = Item.CreateRestaurant(
            id: itemId,
            name: name,
            category: category,
            priceTier: MapPriceTier(business.Price),
            location: location,
            tags: tags,
            description: description,
            // Yelp records state as an int where 1 means open. Absent means unknown, so treat
            // the business as active rather than silently hiding it.
            isActive: business.IsOpen is null or 1);

        return BusinessMapResult.Success(item);
    }

    /// <summary>
    /// Translates Yelp's <c>$</c>-per-tier price string into a <see cref="PriceTier"/>.
    /// Unrecognised or absent values become <see cref="PriceTier.Free"/> rather than being
    /// dropped, since a missing price says nothing about whether the venue is worth showing.
    /// </summary>
    public static PriceTier MapPriceTier(string? price)
    {
        if (string.IsNullOrWhiteSpace(price))
            return PriceTier.Free;

        return price.Trim() switch
        {
            "$" => PriceTier.Budget,
            "$$" => PriceTier.Moderate,
            "$$$" => PriceTier.Expensive,
            "$$$$" => PriceTier.Luxury,
            _ => PriceTier.Free
        };
    }

    /// <summary>Splits Yelp's comma-separated category string into trimmed entries.</summary>
    public static IReadOnlyList<string> SplitCategories(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories))
            return [];

        return categories
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    /// <summary>
    /// Picks the most specific category. Yelp always puts the broad category first, so the last
    /// entry is the most specific one — "Restaurants, Italian, Pizza" becomes "Pizza" rather than
    /// "Restaurants", which would make every item share one category and defeat the filter index.
    /// </summary>
    public static string ResolveCategory(string? categories)
    {
        var entries = SplitCategories(categories);
        if (entries.Count == 0)
            return RestaurantCategory;

        for (var i = entries.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(entries[i], RestaurantCategory, StringComparison.OrdinalIgnoreCase))
                return entries[i];
        }

        return RestaurantCategory;
    }

    private static string BuildDescription(YelpBusiness business)
    {
        var address = business.Address?.Trim();
        if (string.IsNullOrWhiteSpace(address))
            return business.Name?.Trim() ?? string.Empty;

        return $"{business.Name?.Trim()} — {address}";
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
