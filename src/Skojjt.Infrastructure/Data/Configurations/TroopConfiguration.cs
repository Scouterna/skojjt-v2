using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class TroopConfiguration : IEntityTypeConfiguration<Troop>
{
    public void Configure(EntityTypeBuilder<Troop> builder)
    {
        builder.ToTable("troops");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); // Auto-increment

        builder.Property(e => e.ScoutnetId)
            .HasColumnName("scoutnet_id")
            .IsRequired();

        builder.Property(e => e.ScoutGroupId)
            .HasColumnName("scout_group_id")
            .IsRequired();

        builder.Property(e => e.SemesterId)
            .HasColumnName("semester_id")
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.DefaultStartTime)
            .HasColumnName("default_start_time")
            .HasDefaultValue(new TimeOnly(18, 30));

        builder.Property(e => e.DefaultDurationMinutes)
            .HasColumnName("default_duration_minutes")
            .HasDefaultValue(90);

		builder.Property(e => e.DefaultMeetingLocation)
			.HasColumnName("default_meeting_location")
			.HasMaxLength(100);

		builder.Property(e => e.IsLocked)
            .HasColumnName("is_locked")
            .HasDefaultValue(false);

        // Relationships
        builder.HasOne(e => e.ScoutGroup)
            .WithMany(sg => sg.Troops)
            .HasForeignKey(e => e.ScoutGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.ScoutGroupId, e.SemesterId })
            .HasDatabaseName("idx_troops_scout_group_semester");

        // Unique constraint: only one troop per scoutnet_id per semester
        builder.HasIndex(e => new { e.ScoutnetId, e.SemesterId })
            .IsUnique()
            .HasDatabaseName("idx_troops_natural_key");
    }
}
