using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class BadgeTemplateConfiguration : IEntityTypeConfiguration<BadgeTemplate>
{
    public void Configure(EntityTypeBuilder<BadgeTemplate> builder)
    {
        builder.ToTable("badge_templates");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
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

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Unique constraint on name
        builder.HasIndex(e => e.Name)
            .IsUnique();
    }
}
