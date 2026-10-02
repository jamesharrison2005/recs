using System.Text.Json.Serialization;

namespace Recs.Seeder.Yelp;

/// <summary>
/// One line of the Yelp business file. Every field is nullable because the source data is:
/// Yelp leaves <c>latitude</c>, <c>longitude</c>, <c>price</c> and <c>categories</c> empty for a
/// meaningful number of rows, and the loader has to survive that rather than crash on it.
/// </summary>
public sealed class YelpBusiness
{
    [JsonPropertyName("business_id")]
    public string? BusinessId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("stars")]
    public double? Stars { get; set; }

    [JsonPropertyName("review_count")]
    public int? ReviewCount { get; set; }

    [JsonPropertyName("is_open")]
    public int? IsOpen { get; set; }

    /// <summary>One to five <c>$</c> characters, e.g. <c>$$</c>. Absent for many businesses.</summary>
    [JsonPropertyName("price")]
    public string? Price { get; set; }

    [JsonPropertyName("categories")]
    public string? Categories { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }
}
