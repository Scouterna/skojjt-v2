using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class TroopPersonConfiguration : IEntityTypeConfiguration<TroopPerson>
{
    public void Configure(EntityTypeBuilder<TroopPerson> builder)
    {
        builder.ToTable("troop_persons");

        // Composite primary key
        builder.HasKey(e => new { e.TroopId, e.PersonId });

        builder.Property(e => e.TroopId)
            .HasColumnName("troop_id");

        builder.Property(e => e.PersonId)
            .HasColumnName("person_id");

        builder.Property(e => e.IsLeader)
            .HasColumnName("is_leader")
            .HasDefaultValue(false);

        builder.Property(e => e.Patrol)
            .HasColumnName("patrol")
            .HasMaxLength(100);

        builder.Property(e => e.PatrolId)
            .HasColumnName("patrol_id");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Troop)
            .WithMany(t => t.TroopPersons)
            .HasForeignKey(e => e.TroopId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Person)
            .WithMany(p => p.TroopPersons)
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.PersonId)
            .HasDatabaseName("idx_troop_persons_person");
    }
}
