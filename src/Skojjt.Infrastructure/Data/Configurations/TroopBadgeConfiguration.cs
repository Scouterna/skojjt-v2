using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class TroopBadgeConfiguration : IEntityTypeConfiguration<TroopBadge>
{
    public void Configure(EntityTypeBuilder<TroopBadge> builder)
    {
        builder.ToTable("troop_badges");

        // Composite primary key
        builder.HasKey(e => new { e.TroopId, e.BadgeId });

        builder.Property(e => e.TroopId)
            .HasColumnName("troop_id");

        builder.Property(e => e.BadgeId)
            .HasColumnName("badge_id");

        builder.Property(e => e.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Troop)
            .WithMany(t => t.TroopBadges)
            .HasForeignKey(e => e.TroopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Badge)
            .WithMany(b => b.TroopBadges)
            .HasForeignKey(e => e.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
