using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Skojjt.Core.Entities;

namespace Skojjt.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for Skojjt.
/// </summary>
public class SkojjtDbContext : DbContext
{
    public SkojjtDbContext(DbContextOptions<SkojjtDbContext> options) : base(options)
    {
    }

    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<ScoutGroup> ScoutGroups => Set<ScoutGroup>();
    public DbSet<Person> Persons => Set<Person>();
    public DbSet<ScoutGroupPerson> ScoutGroupPersons => Set<ScoutGroupPerson>();
    public DbSet<Troop> Troops => Set<Troop>();
    public DbSet<TroopPerson> TroopPersons => Set<TroopPerson>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAttendance> MeetingAttendances => Set<MeetingAttendance>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<BadgePart> BadgeParts => Set<BadgePart>();
    public DbSet<BadgePartDone> BadgePartsDone => Set<BadgePartDone>();
    public DbSet<BadgeCompleted> BadgesCompleted => Set<BadgeCompleted>();
    public DbSet<TroopBadge> TroopBadges => Set<TroopBadge>();
    public DbSet<BadgeTemplate> BadgeTemplates => Set<BadgeTemplate>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // Suppress pending model changes warning during development/migration
        optionsBuilder.ConfigureWarnings(w =>
            w.Ignore(RelationalEventId.PendingModelChangesWarning));
        optionsBuilder.EnableSensitiveDataLogging();
	}

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SkojjtDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Auto-update timestamps for entities with UpdatedAt
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Modified)
            {
                var updatedAtProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");
                if (updatedAtProperty != null)
                {
                    updatedAtProperty.CurrentValue = DateTime.UtcNow;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
