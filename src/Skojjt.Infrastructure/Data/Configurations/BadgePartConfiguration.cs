using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class BadgePartConfiguration : IEntityTypeConfiguration<BadgePart>
{
    public void Configure(EntityTypeBuilder<BadgePart> builder)
    {
        builder.ToTable("badge_parts");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(e => e.BadgeId)
            .HasColumnName("badge_id");

        builder.Property(e => e.BadgeTemplateId)
            .HasColumnName("badge_template_id");

        builder.Property(e => e.SortOrder)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(e => e.IsAdminPart)
            .HasColumnName("is_admin_part")
            .IsRequired();

        builder.Property(e => e.ShortDescription)
            .HasColumnName("short_description")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(e => e.LongDescription)
            .HasColumnName("long_description");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Badge)
            .WithMany(b => b.Parts)
            .HasForeignKey(e => e.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.BadgeTemplate)
            .WithMany(bt => bt.Parts)
            .HasForeignKey(e => e.BadgeTemplateId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes
        builder.HasIndex(e => new { e.BadgeId, e.SortOrder })
            .HasDatabaseName("idx_badge_parts_badge_sort");

        builder.HasIndex(e => new { e.BadgeTemplateId, e.SortOrder })
            .HasDatabaseName("idx_badge_parts_template_sort");

        // Check constraint: must belong to either a badge or a template, not both
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_badge_parts_owner",
            "(badge_id IS NOT NULL AND badge_template_id IS NULL) OR (badge_id IS NULL AND badge_template_id IS NOT NULL)"));
    }
}
