using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recs.Domain.Entities;

namespace Recs.Infrastructure.Persistence.Configurations;

public class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.UserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.ItemId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(r => r.Score)
            .IsRequired();

        builder.Property(r => r.Review)
            .HasMaxLength(4000);

        builder.Property(r => r.Timestamp)
            .IsRequired();

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.ItemId);

        // Unique constraint: A user can rate an item only once
        builder.HasIndex(r => new { r.UserId, r.ItemId })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Item>()
            .WithMany()
            .HasForeignKey(r => r.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
