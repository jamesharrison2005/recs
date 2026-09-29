using Recs.Domain.ValueObjects;

namespace Recs.Domain.Entities;

public class User
{
    public string Id { get; private set; }
    public string Username { get; private set; }
    public GeoLocation? HomeLocation { get; private set; }
    public UserPreferences Preferences { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Used by EF Core to materialise a <see cref="User"/> from the database,
    /// bypassing the validating constructor. Not for application use.
    /// </summary>
    private User()
    {
    }

    public User(
        string id,
        string username,
        GeoLocation? homeLocation = null,
        UserPreferences? preferences = null,
        DateTimeOffset? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("User ID cannot be null or whitespace.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or whitespace.", nameof(username));
        }

        Id = id.Trim();
        Username = username.Trim();
        HomeLocation = homeLocation;
        Preferences = preferences ?? UserPreferences.Empty;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    public void UpdatePreferences(UserPreferences preferences)
    {
        Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
    }

    public void UpdateLocation(GeoLocation? location)
    {
        HomeLocation = location;
    }
}
