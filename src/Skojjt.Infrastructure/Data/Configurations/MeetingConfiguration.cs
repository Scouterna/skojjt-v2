using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.ToTable("meetings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd(); // Auto-increment

        builder.Property(e => e.TroopId)
            .HasColumnName("troop_id")
            .IsRequired();

        builder.Property(e => e.MeetingDate)
            .HasColumnName("meeting_date")
            .IsRequired();

        builder.Property(e => e.StartTime)
            .HasColumnName("start_time")
            .HasDefaultValue(new TimeOnly(18, 30));

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.DurationMinutes)
            .HasColumnName("duration_minutes")
            .HasDefaultValue(90);

        builder.Property(e => e.IsHike)
            .HasColumnName("is_hike")
            .HasDefaultValue(false);

		builder.Property(e => e.Location)
			.HasColumnName("location")
			.HasMaxLength(50)
			.IsRequired();

		// Relationships
		builder.HasOne(e => e.Troop)
            .WithMany(t => t.Meetings)
            .HasForeignKey(e => e.TroopId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: only one meeting per troop per date
        builder.HasIndex(e => new { e.TroopId, e.MeetingDate })
            .IsUnique()
            .HasDatabaseName("idx_meetings_troop_date");
    }
}
