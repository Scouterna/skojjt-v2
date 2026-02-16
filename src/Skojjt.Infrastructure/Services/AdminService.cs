using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for Skojjt system administration operations.
/// </summary>
public class AdminService : IAdminService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<AdminService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ScoutGroupDeletionPreview> PreviewScoutGroupDeletionAsync(
        int scoutGroupId, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var scoutGroup = await context.ScoutGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(sg => sg.Id == scoutGroupId, cancellationToken);

        if (scoutGroup == null)
        {
            return new ScoutGroupDeletionPreview
            {
                ScoutGroupId = scoutGroupId,
                ScoutGroupName = "Not found"
            };
        }

        // Count persons that ONLY belong to this group (will be deleted)
        var personsOnlyInThisGroup = await context.ScoutGroupPersons
            .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
            .Where(sgp => !context.ScoutGroupPersons
                .Any(other => other.PersonId == sgp.PersonId && other.ScoutGroupId != scoutGroupId))
            .CountAsync(cancellationToken);

        // Count persons in multiple groups (only membership removed)
        var personsInMultipleGroups = await context.ScoutGroupPersons
            .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
            .Where(sgp => context.ScoutGroupPersons
                .Any(other => other.PersonId == sgp.PersonId && other.ScoutGroupId != scoutGroupId))
            .CountAsync(cancellationToken);

        // Count troops
        var troopsCount = await context.Troops
            .Where(t => t.ScoutGroupId == scoutGroupId)
            .CountAsync(cancellationToken);

        // Count meetings (via troops)
        var meetingsCount = await context.Meetings
            .Where(m => m.Troop.ScoutGroupId == scoutGroupId)
            .CountAsync(cancellationToken);

        // Count badges
        var badgesCount = await context.Badges
            .Where(b => b.ScoutGroupId == scoutGroupId)
            .CountAsync(cancellationToken);

        return new ScoutGroupDeletionPreview
        {
            ScoutGroupId = scoutGroupId,
            ScoutGroupName = scoutGroup.Name,
            PersonsToDelete = personsOnlyInThisGroup,
            PersonMembershipsToRemove = personsInMultipleGroups,
            TroopsToDelete = troopsCount,
            MeetingsToDelete = meetingsCount,
            BadgesToDelete = badgesCount,
        };
    }

    public async Task<ScoutGroupDeletionResult> DeleteScoutGroupAsync(
        int scoutGroupId, 
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var scoutGroup = await context.ScoutGroups
                .FirstOrDefaultAsync(sg => sg.Id == scoutGroupId, cancellationToken);

            if (scoutGroup == null)
            {
                return new ScoutGroupDeletionResult
                {
                    Success = false,
                    ErrorMessage = $"Scout group {scoutGroupId} not found"
                };
            }

            _logger.LogInformation("Starting deletion of scout group {ScoutGroupId}: {Name}", 
                scoutGroupId, scoutGroup.Name);

            var result = new ScoutGroupDeletionResult { Success = true };

            // 1. Find persons that ONLY belong to this group
            var personIdsOnlyInThisGroup = await context.ScoutGroupPersons
                .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
                .Where(sgp => !context.ScoutGroupPersons
                    .Any(other => other.PersonId == sgp.PersonId && other.ScoutGroupId != scoutGroupId))
                .Select(sgp => sgp.PersonId)
                .ToListAsync(cancellationToken);

            // 2. Delete meeting attendances for persons being deleted
            var attendancesToDelete = await context.MeetingAttendances
                .Where(ma => personIdsOnlyInThisGroup.Contains(ma.PersonId))
                .ToListAsync(cancellationToken);
            context.MeetingAttendances.RemoveRange(attendancesToDelete);

            // 3. Delete badge parts done for persons being deleted
            var badgePartsDoneToDelete = await context.BadgePartsDone
                .Where(bpd => personIdsOnlyInThisGroup.Contains(bpd.PersonId))
                .ToListAsync(cancellationToken);
            context.BadgePartsDone.RemoveRange(badgePartsDoneToDelete);

            // 4. Delete badges completed for persons being deleted
            var badgesCompletedToDelete = await context.BadgesCompleted
                .Where(bc => personIdsOnlyInThisGroup.Contains(bc.PersonId))
                .ToListAsync(cancellationToken);
            context.BadgesCompleted.RemoveRange(badgesCompletedToDelete);

            // 5. Delete troop persons for this group's troops
            var troopIds = await context.Troops
                .Where(t => t.ScoutGroupId == scoutGroupId)
                .Select(t => t.Id)
                .ToListAsync(cancellationToken);

            var troopPersonsToDelete = await context.TroopPersons
                .Where(tp => troopIds.Contains(tp.TroopId))
                .ToListAsync(cancellationToken);
            context.TroopPersons.RemoveRange(troopPersonsToDelete);

            // 6. Delete meetings and their attendances for this group's troops
            var meetingsToDelete = await context.Meetings
                .Where(m => troopIds.Contains(m.TroopId))
                .ToListAsync(cancellationToken);
            result.MeetingsDeleted = meetingsToDelete.Count;
            
            var meetingIds = meetingsToDelete.Select(m => m.Id).ToList();
            var meetingAttendancesToDelete = await context.MeetingAttendances
                .Where(ma => meetingIds.Contains(ma.MeetingId))
                .ToListAsync(cancellationToken);
            context.MeetingAttendances.RemoveRange(meetingAttendancesToDelete);
            context.Meetings.RemoveRange(meetingsToDelete);

            // 7. Delete troop badges for this group's troops
            var troopBadgesToDelete = await context.TroopBadges
                .Where(tb => troopIds.Contains(tb.TroopId))
                .ToListAsync(cancellationToken);
            context.TroopBadges.RemoveRange(troopBadgesToDelete);

            // 8. Delete troops
            var troopsToDelete = await context.Troops
                .Where(t => t.ScoutGroupId == scoutGroupId)
                .ToListAsync(cancellationToken);
            result.TroopsDeleted = troopsToDelete.Count;
            context.Troops.RemoveRange(troopsToDelete);

            // 9. Delete badges and their related data for this group
            var badgeIds = await context.Badges
                .Where(b => b.ScoutGroupId == scoutGroupId)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

            var badgePartsToDelete = await context.BadgePartsDone
                .Where(bpd => badgeIds.Contains(bpd.BadgeId))
                .ToListAsync(cancellationToken);
            context.BadgePartsDone.RemoveRange(badgePartsToDelete);

            var badgeCompletedToDelete = await context.BadgesCompleted
                .Where(bc => badgeIds.Contains(bc.BadgeId))
                .ToListAsync(cancellationToken);
            context.BadgesCompleted.RemoveRange(badgeCompletedToDelete);

            var badgesToDelete = await context.Badges
                .Where(b => b.ScoutGroupId == scoutGroupId)
                .ToListAsync(cancellationToken);
            result.BadgesDeleted = badgesToDelete.Count;
            context.Badges.RemoveRange(badgesToDelete);

            // 10. Delete scout group persons for this group
            var scoutGroupPersonsToDelete = await context.ScoutGroupPersons
                .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
                .ToListAsync(cancellationToken);
            result.PersonMembershipsRemoved = scoutGroupPersonsToDelete.Count - personIdsOnlyInThisGroup.Count;
            context.ScoutGroupPersons.RemoveRange(scoutGroupPersonsToDelete);

            // 11. Delete persons that only belonged to this group
            var personsToDelete = await context.Persons
                .Where(p => personIdsOnlyInThisGroup.Contains(p.Id))
                .ToListAsync(cancellationToken);
            result.PersonsDeleted = personsToDelete.Count;
            context.Persons.RemoveRange(personsToDelete);

            // 12. Finally delete the scout group
            context.ScoutGroups.Remove(scoutGroup);

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully deleted scout group {ScoutGroupId}. " +
                "Deleted: {PersonsDeleted} persons, {TroopsDeleted} troops, {MeetingsDeleted} meetings, {BadgesDeleted} badges. " +
                "Removed {MembershipsRemoved} memberships from persons in other groups.",
                scoutGroupId, result.PersonsDeleted, result.TroopsDeleted, 
                result.MeetingsDeleted, result.BadgesDeleted, result.PersonMembershipsRemoved);

            return result;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to delete scout group {ScoutGroupId}", scoutGroupId);

            return new ScoutGroupDeletionResult
            {
                Success = false,
                ErrorMessage = $"Failed to delete scout group: {ex.Message}"
            };
        }
    }
}
