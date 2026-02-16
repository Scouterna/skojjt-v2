using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class MeetingAttendanceConfiguration : IEntityTypeConfiguration<MeetingAttendance>
{
    public void Configure(EntityTypeBuilder<MeetingAttendance> builder)
    {
        builder.ToTable("meeting_attendances");

        // Composite primary key
        builder.HasKey(e => new { e.MeetingId, e.PersonId });

        builder.Property(e => e.MeetingId)
            .HasColumnName("meeting_id")
            .HasColumnType("int")
            .IsRequired();

        builder.Property(e => e.PersonId)
            .HasColumnName("person_id");

        // Relationships
        builder.HasOne(e => e.Meeting)
            .WithMany(m => m.Attendances)
            .HasForeignKey(e => e.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Person)
            .WithMany(p => p.MeetingAttendances)
            .HasForeignKey(e => e.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.PersonId)
            .HasDatabaseName("idx_meeting_attendances_person");
    }
}
