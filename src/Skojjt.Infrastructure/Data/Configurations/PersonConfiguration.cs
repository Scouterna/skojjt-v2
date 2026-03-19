using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;
using Skojjt.Core.Utilities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("persons");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever(); // Scoutnet member number

        builder.Property(e => e.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(e => e.BirthDate)
            .HasColumnName("birth_date");

        builder.Property(e => e.PersonalNumber)
            .HasColumnName("personal_number")
            .HasMaxLength(15)
			.HasConversion(
				v => v == null ? null : v.ToString(), // Convert Personnummer to string for storage
				v => v == null ? null : new Personnummer(v)); // Convert string back to Personnummer when reading from the database

		builder.Property(e => e.Email)
            .HasColumnName("email")
            .HasMaxLength(100);

        builder.Property(e => e.Phone)
            .HasColumnName("phone")
            .HasMaxLength(25);

        builder.Property(e => e.Mobile)
            .HasColumnName("mobile")
            .HasMaxLength(50);

        builder.Property(e => e.AltEmail)
            .HasColumnName("alt_email")
            .HasMaxLength(100);

        builder.Property(e => e.MumName)
            .HasColumnName("mum_name")
            .HasMaxLength(60);

        builder.Property(e => e.MumEmail)
            .HasColumnName("mum_email")
            .HasMaxLength(100);

        builder.Property(e => e.MumMobile)
            .HasColumnName("mum_mobile")
            .HasMaxLength(50);

        builder.Property(e => e.DadName)
            .HasColumnName("dad_name")
            .HasMaxLength(50);

        builder.Property(e => e.DadEmail)
            .HasColumnName("dad_email")
            .HasMaxLength(100);

        builder.Property(e => e.DadMobile)
            .HasColumnName("dad_mobile")
            .HasMaxLength(50);

        builder.Property(e => e.Street)
            .HasColumnName("street")
            .HasMaxLength(100);

        builder.Property(e => e.ZipCode)
            .HasColumnName("zip_code")
            .HasMaxLength(20);

        builder.Property(e => e.ZipName)
            .HasColumnName("zip_name")
            .HasMaxLength(50);

        builder.Property(e => e.MemberYears)
            .HasColumnName("member_years");

        builder.Property(e => e.Removed)
            .HasColumnName("removed")
            .HasDefaultValue(false);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("NOW()");

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at")
            .HasDefaultValueSql("NOW()");

        // Ignore computed properties
        builder.Ignore(e => e.FullName);
        builder.Ignore(e => e.Age);
    }
}
