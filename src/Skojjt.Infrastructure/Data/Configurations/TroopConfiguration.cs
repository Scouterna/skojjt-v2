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

		builder.Property(e => e.UnitTypeId)
			.HasColumnName("unit_type_id");

		builder.Property(e => e.IsLocked)
			.HasColumnName("is_locked")
			.HasDefaultValue(false);

		builder.Property(e => e.TroopType)
			.HasColumnName("troop_type")
			.HasDefaultValue(TroopType.Regular);

		builder.Property(e => e.CampStartDate)
			.HasColumnName("camp_start_date");

		builder.Property(e => e.CampEndDate)
			.HasColumnName("camp_end_date");

		builder.Property(e => e.ScoutnetProjectId)
			.HasColumnName("scoutnet_project_id");

		builder.Property(e => e.ScoutnetCheckinApiKey)
			.HasColumnName("scoutnet_checkin_api_key")
			.HasMaxLength(200);

		builder.Ignore(e => e.IsCamp);

        // Relationships
        builder.HasOne(e => e.ScoutGroup)
            .WithMany(sg => sg.Troops)
            .HasForeignKey(e => e.ScoutGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => new { e.ScoutGroupId, e.SemesterId })
            .HasDatabaseName("idx_troops_scout_group_semester");

        // Unique constraint: only one troop per scoutnet_id per scout group per semester
        builder.HasIndex(e => new { e.ScoutnetId, e.ScoutGroupId, e.SemesterId })
            .IsUnique()
            .HasDatabaseName("idx_troops_natural_key");
    }
}
