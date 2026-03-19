using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class ScoutGroupConfiguration : IEntityTypeConfiguration<ScoutGroup>
{
    public void Configure(EntityTypeBuilder<ScoutGroup> builder)
    {
        builder.ToTable("scout_groups");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.OrganisationNumber)
            .HasColumnName("organisation_number")
            .HasMaxLength(20);

        builder.Property(e => e.AssociationId)
            .HasColumnName("association_id")
            .HasMaxLength(50);

        builder.Property(e => e.MunicipalityId)
            .HasColumnName("municipality_id")
            .HasMaxLength(10);

        builder.Property(e => e.ApiKeyWaitinglist)
            .HasColumnName("api_key_waitinglist")
            .HasMaxLength(100);

        builder.Property(e => e.ApiKeyAllMembers)
            .HasColumnName("api_key_all_members")
            .HasMaxLength(100);

        builder.Property(e => e.BankAccount)
            .HasColumnName("bank_account")
            .HasMaxLength(50);

        builder.Property(e => e.Address)
            .HasColumnName("address")
            .HasMaxLength(100);

        builder.Property(e => e.PostalAddress)
            .HasColumnName("postal_address")
            .HasMaxLength(100);

        builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(100);

        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(e => e.DefaultCampLocation)
            .HasColumnName("default_camp_location")
            .HasMaxLength(150);
		
        builder.Property(e => e.DefaultMeetingLocation)
            .HasColumnName("default_meeting_location")
            .HasMaxLength(150);

        builder.Property(e => e.Signatory)
            .HasColumnName("signatory")
            .HasMaxLength(150);

        builder.Property(e => e.SignatoryPhone)
            .HasColumnName("signatory_phone")
            .HasMaxLength(50);

        builder.Property(e => e.SignatoryEmail)
            .HasColumnName("signatory_email")
            .HasMaxLength(100);

        builder.Property(e => e.AttendanceMinYear)
            .HasColumnName("attendance_min_year")
            .HasDefaultValue(10);

        builder.Property(e => e.AttendanceInclHike)
            .HasColumnName("attendance_incl_hike")
            .HasDefaultValue(true);

        builder.Property(e => e.NextLocalTroopId)
            .HasColumnName("next_local_troop_id")
            .HasDefaultValue(250);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");
    }
}
