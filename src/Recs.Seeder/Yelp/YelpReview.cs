using System.Text.Json.Serialization;

namespace Recs.Seeder.Yelp;

/// <summary>
/// One line of the Yelp review file. As with <see cref="YelpBusiness"/>, everything is nullable
/// so a sparse row deserialises and can be skipped deliberately rather than throwing.
/// </summary>
public sealed class YelpReview
{
    [JsonPropertyName("review_id")]
    public string? ReviewId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("business_id")]
    public string? BusinessId { get; set; }

    [JsonPropertyName("stars")]
    public double? Stars { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}
