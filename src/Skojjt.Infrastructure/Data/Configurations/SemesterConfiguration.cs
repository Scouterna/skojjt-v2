using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data.Configurations;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("semesters", t =>
        {
            t.HasCheckConstraint(
                "chk_semester_id",
                "id = (year * 10) + CASE WHEN is_autumn THEN 1 ELSE 0 END");
        });

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(e => e.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(e => e.IsAutumn)
            .HasColumnName("is_autumn")
            .IsRequired();
    }
}
