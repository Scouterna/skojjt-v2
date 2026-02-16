using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class BadgeConfiguration : IEntityTypeConfiguration<Badge>
{
    public void Configure(EntityTypeBuilder<Badge> builder)
    {
        builder.ToTable("badges");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(e => e.ScoutGroupId)
            .HasColumnName("scout_group_id")
            .IsRequired();

        builder.Property(e => e.TemplateId)
            .HasColumnName("template_id");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description");

        builder.Property(e => e.PartsScoutShort)
            .HasColumnName("parts_scout_short");

        builder.Property(e => e.PartsScoutLong)
            .HasColumnName("parts_scout_long");

        builder.Property(e => e.PartsAdminShort)
            .HasColumnName("parts_admin_short");

        builder.Property(e => e.PartsAdminLong)
            .HasColumnName("parts_admin_long");

        builder.Property(e => e.ImageUrl)
            .HasColumnName("image_url")
            .HasMaxLength(500);

        builder.Property(e => e.IsArchived)
            .HasColumnName("is_archived")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.ScoutGroup)
            .WithMany(sg => sg.Badges)
            .HasForeignKey(e => e.ScoutGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Template)
            .WithMany(bt => bt.Badges)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.ScoutGroupId)
            .HasDatabaseName("idx_badges_scout_group");

        builder.HasIndex(e => e.TemplateId)
            .HasDatabaseName("idx_badges_template");

        // Unique constraint on scout_group + name
        builder.HasIndex(e => new { e.ScoutGroupId, e.Name })
            .IsUnique();

        // Ignore computed properties
        builder.Ignore(e => e.TotalScoutParts);
        builder.Ignore(e => e.TotalAdminParts);
        builder.Ignore(e => e.TotalParts);
    }
}
