using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Recs.Domain.Entities;
using Recs.Domain.Enums;
using Recs.Domain.ValueObjects;

namespace Recs.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(u => u.Username)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(u => u.Username)
            .IsUnique();

        builder.Property(u => u.CreatedAt)
            .IsRequired();

        builder.ComplexProperty(u => u.HomeLocation, loc =>
        {
            loc.Property(l => l.Latitude)
                .HasColumnName("HomeLatitude");

            loc.Property(l => l.Longitude)
                .HasColumnName("HomeLongitude");
        });

        var preferencesConverter = new ValueConverter<UserPreferences, string>(
            v => JsonSerializer.Serialize(UserPreferencesDto.FromDomain(v), (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? UserPreferences.Empty
                : (JsonSerializer.Deserialize<UserPreferencesDto>(v, (JsonSerializerOptions?)null) ?? new UserPreferencesDto()).ToDomain());

        var preferencesComparer = new ValueComparer<UserPreferences>(
            (c1, c2) => c1 != null && c2 != null
                ? c1.PreferredCategories.SetEquals(c2.PreferredCategories) &&
                  c1.PreferredPriceTiers.SetEquals(c2.PreferredPriceTiers) &&
                  c1.PreferredTags.SetEquals(c2.PreferredTags) &&
                  c1.MaxTravelDistanceKm == c2.MaxTravelDistanceKm
                : c1 == c2,
            c => HashCode.Combine(
                c.PreferredCategories.Aggregate(0, (a, v) => HashCode.Combine(a, StringComparer.OrdinalIgnoreCase.GetHashCode(v))),
                c.PreferredPriceTiers.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c.PreferredTags.Aggregate(0, (a, v) => HashCode.Combine(a, StringComparer.OrdinalIgnoreCase.GetHashCode(v))),
                c.MaxTravelDistanceKm),
            c => new UserPreferences(c.PreferredCategories, c.PreferredPriceTiers, c.PreferredTags, c.MaxTravelDistanceKm));

        builder.Property(u => u.Preferences)
            .HasConversion(preferencesConverter)
            .Metadata.SetValueComparer(preferencesComparer);
    }

    private sealed class UserPreferencesDto
    {
        public List<string>? PreferredCategories { get; set; }
        public List<PriceTier>? PreferredPriceTiers { get; set; }
        public List<string>? PreferredTags { get; set; }
        public double? MaxTravelDistanceKm { get; set; }

        public static UserPreferencesDto FromDomain(UserPreferences preferences) => new()
        {
            PreferredCategories = preferences.PreferredCategories.ToList(),
            PreferredPriceTiers = preferences.PreferredPriceTiers.ToList(),
            PreferredTags = preferences.PreferredTags.ToList(),
            MaxTravelDistanceKm = preferences.MaxTravelDistanceKm
        };

        public UserPreferences ToDomain() => new(
            preferredCategories: PreferredCategories,
            preferredPriceTiers: PreferredPriceTiers,
            preferredTags: PreferredTags,
            maxTravelDistanceKm: MaxTravelDistanceKm
        );
    }
}
