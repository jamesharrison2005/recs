using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;

namespace Recs.Domain.Entities;

public class Item
{
    public string Id { get; private set; }
    public string Name { get; private set; }
    public ItemType Type { get; private set; }
    public string Category { get; private set; }
    public PriceTier PriceTier { get; private set; }
    public GeoLocation Location { get; private set; }
    public IReadOnlySet<string> Tags { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? EventStart { get; private set; }
    public DateTimeOffset? EventEnd { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>
    /// Used by EF Core to materialise an <see cref="Item"/> from the database,
    /// bypassing the validating constructor. Not for application use.
    /// </summary>
    private Item()
    {
    }

    public Item(
        string id,
        string name,
        ItemType type,
        string category,
        PriceTier priceTier,
        GeoLocation location,
        IEnumerable<string>? tags = null,
        string? description = null,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? eventStart = null,
        DateTimeOffset? eventEnd = null,
        bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Item ID cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name cannot be null or whitespace.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Item category cannot be null or whitespace.", nameof(category));
        }

        if (eventStart.HasValue && eventEnd.HasValue && eventEnd.Value < eventStart.Value)
        {
            throw new ArgumentException("Event end time cannot be earlier than event start time.", nameof(eventEnd));
        }

        Id = id.Trim();
        Name = name.Trim();
        Type = type;
        Category = category.Trim();
        PriceTier = priceTier;
        Location = location;
        Description = description?.Trim();
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        EventStart = eventStart;
        EventEnd = eventEnd;
        IsActive = isActive;

        Tags = tags != null
            ? new HashSet<string>(tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToLowerInvariant()), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static Item CreateRestaurant(
        string id,
        string name,
        string category,
        PriceTier priceTier,
        GeoLocation location,
        IEnumerable<string>? tags = null,
        string? description = null,
        DateTimeOffset? createdAt = null,
        bool isActive = true)
    {
        return new Item(
            id: id,
            name: name,
            type: ItemType.Restaurant,
            category: category,
            priceTier: priceTier,
            location: location,
            tags: tags,
            description: description,
            createdAt: createdAt,
            isActive: isActive);
    }

    public static Item CreateEvent(
        string id,
        string name,
        string category,
        PriceTier priceTier,
        GeoLocation location,
        DateTimeOffset eventStart,
        DateTimeOffset? eventEnd = null,
        IEnumerable<string>? tags = null,
        string? description = null,
        DateTimeOffset? createdAt = null,
        bool isActive = true)
    {
        return new Item(
            id: id,
            name: name,
            type: ItemType.Event,
            category: category,
            priceTier: priceTier,
            location: location,
            tags: tags,
            description: description,
            createdAt: createdAt,
            eventStart: eventStart,
            eventEnd: eventEnd,
            isActive: isActive);
    }

    public double DistanceToKm(GeoLocation other) => Location.DistanceToKm(other);

    public double DistanceToMiles(GeoLocation other) => Location.DistanceToMiles(other);

    public bool MatchesTags(IEnumerable<string> queryTags)
    {
        if (queryTags == null) return false;
        return queryTags
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Any(t => Tags.Contains(t.Trim()));
    }

    public void UpdateStatus(bool isActive)
    {
        IsActive = isActive;
    }
}
