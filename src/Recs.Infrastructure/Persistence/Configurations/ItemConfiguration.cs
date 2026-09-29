using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Recs.Domain.Entities;

namespace Recs.Infrastructure.Persistence.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Name)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(i => i.Category)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(i => i.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.PriceTier)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(i => i.Description)
            .HasMaxLength(4000);

        builder.Property(i => i.CreatedAt)
            .IsRequired();

        builder.Property(i => i.EventStart);
        builder.Property(i => i.EventEnd);

        builder.Property(i => i.IsActive)
            .IsRequired();

        builder.ComplexProperty(i => i.Location, loc =>
        {
            loc.Property(l => l.Latitude)
                .HasColumnName("Latitude")
                .IsRequired();

            loc.Property(l => l.Longitude)
                .HasColumnName("Longitude")
                .IsRequired();
        });

        // Tags round trip as a JSON array. The domain lower-cases and de-duplicates
        // tags on construction and always exposes a case-insensitive set, so the
        // comparer has to be restored on the way back in.
        var tagsConverter = new ValueConverter<IReadOnlySet<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(
                    JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase));

        var tagsComparer = new ValueComparer<IReadOnlySet<string>>(
            (c1, c2) => c1 != null && c2 != null ? c1.SetEquals(c2) : c1 == c2,
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, StringComparer.OrdinalIgnoreCase.GetHashCode(v))),
            c => new HashSet<string>(c, StringComparer.OrdinalIgnoreCase));

        builder.Property(i => i.Tags)
            .HasConversion(tagsConverter)
            .Metadata.SetValueComparer(tagsComparer);

        builder.HasIndex(i => i.Category);
        builder.HasIndex(i => i.Type);
        builder.HasIndex(i => i.PriceTier);
        builder.HasIndex(i => i.IsActive);
        builder.HasIndex(i => new { i.Type, i.IsActive });
    }
}
