using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class BadgePartDoneConfiguration : IEntityTypeConfiguration<BadgePartDone>
{
    public void Configure(EntityTypeBuilder<BadgePartDone> builder)
    {
        builder.ToTable("badge_parts_done");

        // Composite primary key
        builder.HasKey(e => new { e.PersonId, e.BadgeId, e.PartIndex, e.IsScoutPart });

        builder.Property(e => e.PersonId)
            .HasColumnName("person_id");

        builder.Property(e => e.BadgeId)
            .HasColumnName("badge_id");

        builder.Property(e => e.PartIndex)
            .HasColumnName("part_index");

        builder.Property(e => e.IsScoutPart)
            .HasColumnName("is_scout_part");

        builder.Property(e => e.BadgePartId)
            .HasColumnName("badge_part_id");

        builder.Property(e => e.ExaminerName)
            .HasColumnName("examiner_name")
            .HasMaxLength(50);

        builder.Property(e => e.CompletedDate)
            .HasColumnName("completed_date")
            .HasDefaultValueSql("CURRENT_DATE");

        builder.Property(e => e.UndoneAt)
            .HasColumnName("undone_at");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Person)
            .WithMany(p => p.BadgePartsDone)
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Badge)
            .WithMany(b => b.PartsDone)
            .HasForeignKey(e => e.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.BadgePart)
            .WithMany(bp => bp.PartsDone)
            .HasForeignKey(e => e.BadgePartId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(e => e.BadgeId)
            .HasDatabaseName("idx_badge_parts_done_badge");

        builder.HasIndex(e => e.BadgePartId)
            .HasDatabaseName("idx_badge_parts_done_badge_part");
    }
}
