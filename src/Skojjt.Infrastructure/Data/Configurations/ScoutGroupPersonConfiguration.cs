using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class ScoutGroupPersonConfiguration : IEntityTypeConfiguration<ScoutGroupPerson>
{
    public void Configure(EntityTypeBuilder<ScoutGroupPerson> builder)
    {
        builder.ToTable("scout_group_persons");

        // Composite primary key
        builder.HasKey(e => new { e.PersonId, e.ScoutGroupId });

        builder.Property(e => e.PersonId)
            .HasColumnName("person_id")
            .IsRequired();

        builder.Property(e => e.ScoutGroupId)
            .HasColumnName("scout_group_id")
            .IsRequired();

        builder.Property(e => e.NotInScoutnet)
            .HasColumnName("not_in_scoutnet")
            .HasDefaultValue(false);

        builder.Property(e => e.GroupRoles)
            .HasColumnName("group_roles")
            .HasMaxLength(500);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Person)
            .WithMany(p => p.ScoutGroupPersons)
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ScoutGroup)
            .WithMany(sg => sg.ScoutGroupPersons)
            .HasForeignKey(e => e.ScoutGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Index for fast lookups by scout group
        builder.HasIndex(e => e.ScoutGroupId)
            .HasDatabaseName("idx_scout_group_persons_group");

        // Index for finding persons not in scoutnet
        builder.HasIndex(e => e.NotInScoutnet)
            .HasDatabaseName("idx_scout_group_persons_not_in_scoutnet");
    }
}
