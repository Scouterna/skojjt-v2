using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class BadgeCompletedConfiguration : IEntityTypeConfiguration<BadgeCompleted>
{
    public void Configure(EntityTypeBuilder<BadgeCompleted> builder)
    {
        builder.ToTable("badges_completed");

        // Composite primary key
        builder.HasKey(e => new { e.PersonId, e.BadgeId });

        builder.Property(e => e.PersonId)
            .HasColumnName("person_id");

        builder.Property(e => e.BadgeId)
            .HasColumnName("badge_id");

        builder.Property(e => e.Examiner)
            .HasColumnName("examiner")
            .HasMaxLength(50);

        builder.Property(e => e.CompletedDate)
            .HasColumnName("completed_date")
            .HasDefaultValueSql("CURRENT_DATE");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        // Relationships
        builder.HasOne(e => e.Person)
            .WithMany(p => p.BadgesCompleted)
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Badge)
            .WithMany(b => b.Completed)
            .HasForeignKey(e => e.BadgeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
