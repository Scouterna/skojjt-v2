using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Badge service implementation with business logic for progress tracking,
/// auto-complete detection, undo support, and template-based creation.
/// </summary>
public class BadgeService : IBadgeService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<BadgeService> _logger;

    public BadgeService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<BadgeService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Badge?> GetBadgeWithPartsAsync(int badgeId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Badges
            .Include(b => b.Parts.OrderBy(p => p.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == badgeId, cancellationToken);
    }

    public async Task<IReadOnlyList<Badge>> GetBadgesForGroupAsync(int scoutGroupId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var query = context.Badges
            .Where(b => b.ScoutGroupId == scoutGroupId);

        if (!includeArchived)
            query = query.Where(b => !b.IsArchived);

        return await query
            .Include(b => b.Parts.OrderBy(p => p.SortOrder))
            .OrderBy(b => b.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<BadgeTroopProgress> GetTroopProgressAsync(int badgeId, int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var badge = await context.Badges
            .Include(b => b.Parts.OrderBy(p => p.SortOrder))
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == badgeId, cancellationToken)
            ?? throw new ArgumentException($"Badge {badgeId} not found");

        // Get troop members (non-leaders)
        var troopPersons = await context.TroopPersons
            .Where(tp => tp.TroopId == troopId && !tp.IsLeader)
            .Include(tp => tp.Person)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var personIds = troopPersons.Select(tp => tp.PersonId).ToList();

        // Get all active (non-undone) progress for this badge and these persons
        var partsDone = await context.BadgePartsDone
            .Where(pd => pd.BadgeId == badgeId
                && personIds.Contains(pd.PersonId)
                && pd.UndoneAt == null
                && pd.BadgePartId != null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Get completions
        var completions = await context.BadgesCompleted
            .Where(bc => bc.BadgeId == badgeId && personIds.Contains(bc.PersonId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var completedPersonIds = completions.Select(c => c.PersonId).ToHashSet();

        var progressByPerson = partsDone
            .GroupBy(pd => pd.PersonId)
            .ToDictionary(g => g.Key, g => g.Where(pd => pd.BadgePartId.HasValue).Select(pd => pd.BadgePartId!.Value).ToHashSet());

        var personProgress = troopPersons
            .Where(tp => !tp.Person.Removed)
            .OrderBy(tp => tp.Person.FirstName).ThenBy(tp => tp.Person.LastName)
            .Select(tp => new PersonPartProgress
            {
                Person = tp.Person,
                CompletedPartIds = progressByPerson.GetValueOrDefault(tp.PersonId, []),
                IsCompleted = completedPersonIds.Contains(tp.PersonId)
            })
            .ToList();

        return new BadgeTroopProgress
        {
            Badge = badge,
            Parts = badge.Parts.ToList(),
            PersonProgress = personProgress
        };
    }

    public async Task<IReadOnlyList<BadgePersonSummary>> GetPersonBadgesAsync(int personId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Get all active parts done by this person
        var partsDone = await context.BadgePartsDone
            .Where(pd => pd.PersonId == personId && pd.UndoneAt == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var badgeIds = partsDone.Select(pd => pd.BadgeId).Distinct().ToList();

        if (badgeIds.Count == 0)
            return [];

        var badges = await context.Badges
            .Where(b => badgeIds.Contains(b.Id))
            .Include(b => b.Parts)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var completions = await context.BadgesCompleted
            .Where(bc => bc.PersonId == personId && badgeIds.Contains(bc.BadgeId))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var completedBadgeIds = completions.Select(c => c.BadgeId).ToHashSet();
        var doneByBadge = partsDone.GroupBy(pd => pd.BadgeId).ToDictionary(g => g.Key, g => g.Count());

        return badges
            .OrderBy(b => b.Name)
            .Select(b => new BadgePersonSummary
            {
                Badge = b,
                TotalParts = b.Parts.Count,
                CompletedParts = doneByBadge.GetValueOrDefault(b.Id, 0),
                IsCompleted = completedBadgeIds.Contains(b.Id)
            })
            .ToList();
    }

    public async Task<TogglePartResult> TogglePartAsync(int badgeId, int badgePartId, int personId, string examinerName, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Validate the badge part exists and belongs to this badge
        var badgePart = await context.BadgeParts
            .FirstOrDefaultAsync(bp => bp.Id == badgePartId && bp.BadgeId == badgeId, cancellationToken)
            ?? throw new ArgumentException($"BadgePart {badgePartId} not found for badge {badgeId}");

        // Look for an existing active record
        var existing = await context.BadgePartsDone
            .FirstOrDefaultAsync(pd =>
                pd.PersonId == personId
                && pd.BadgeId == badgeId
                && pd.BadgePartId == badgePartId
                && pd.UndoneAt == null, cancellationToken);

        var result = new TogglePartResult();

        if (existing != null)
        {
            // Undo: mark as undone rather than deleting for audit trail
            existing.UndoneAt = DateTime.UtcNow;
            result.IsDone = false;

            // Check if badge was completed and needs to be uncompleted
            var completion = await context.BadgesCompleted
                .FirstOrDefaultAsync(bc => bc.PersonId == personId && bc.BadgeId == badgeId, cancellationToken);
            if (completion != null)
            {
                context.BadgesCompleted.Remove(completion);
                result.BadgeUncompleted = true;
                _logger.LogInformation("Badge {BadgeId} uncompleted for person {PersonId}", badgeId, personId);
            }
        }
        else
        {
            // Check for a previously undone record to reactivate (same composite PK)
            var undone = await context.BadgePartsDone
                .FirstOrDefaultAsync(pd =>
                    pd.PersonId == personId
                    && pd.BadgeId == badgeId
                    && pd.BadgePartId == badgePartId
                    && pd.UndoneAt != null, cancellationToken);

            if (undone != null)
            {
                // Reactivate the existing record
                undone.UndoneAt = null;
                undone.ExaminerName = examinerName;
                undone.CompletedDate = DateOnly.FromDateTime(DateTime.Today);
                undone.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new record
                context.BadgePartsDone.Add(new BadgePartDone
                {
                    PersonId = personId,
                    BadgeId = badgeId,
                    BadgePartId = badgePartId,
                    PartIndex = badgePart.SortOrder,
                    IsScoutPart = !badgePart.IsAdminPart,
                    ExaminerName = examinerName,
                    CompletedDate = DateOnly.FromDateTime(DateTime.Today)
                });
            }
            result.IsDone = true;

            // Check if all parts are now done ? auto-complete
            var allPartIds = await context.BadgeParts
                .Where(bp => bp.BadgeId == badgeId)
                .Select(bp => bp.Id)
                .ToListAsync(cancellationToken);

            var donePartIds = await context.BadgePartsDone
                .Where(pd => pd.PersonId == personId
                    && pd.BadgeId == badgeId
                    && pd.BadgePartId != null
                    && pd.UndoneAt == null)
                .Select(pd => pd.BadgePartId!.Value)
                .ToListAsync(cancellationToken);

            // Include the one we're about to add
            var allDone = allPartIds.All(id => id == badgePartId || donePartIds.Contains(id));

            if (allDone && allPartIds.Count > 0)
            {
                var alreadyCompleted = await context.BadgesCompleted
                    .AnyAsync(bc => bc.PersonId == personId && bc.BadgeId == badgeId, cancellationToken);

                if (!alreadyCompleted)
                {
                    context.BadgesCompleted.Add(new BadgeCompleted
                    {
                        PersonId = personId,
                        BadgeId = badgeId,
                        Examiner = examinerName,
                        CompletedDate = DateOnly.FromDateTime(DateTime.Today)
                    });
                    result.BadgeCompleted = true;
                    _logger.LogInformation("Badge {BadgeId} completed for person {PersonId} by {Examiner}",
                        badgeId, personId, examinerName);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<Badge> CreateFromTemplateAsync(int templateId, int scoutGroupId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var template = await context.BadgeTemplates
            .Include(t => t.Parts.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
            ?? throw new ArgumentException($"Template {templateId} not found");

        var badge = new Badge
        {
            ScoutGroupId = scoutGroupId,
            TemplateId = templateId,
            Name = template.Name,
            Description = template.Description,
            ImageUrl = template.ImageUrl,
            // Copy legacy arrays for backward compatibility
            PartsScoutShort = template.PartsScoutShort,
            PartsScoutLong = template.PartsScoutLong,
            PartsAdminShort = template.PartsAdminShort,
            PartsAdminLong = template.PartsAdminLong
        };

        context.Badges.Add(badge);
        await context.SaveChangesAsync(cancellationToken);

        // Copy normalized parts
        foreach (var templatePart in template.Parts)
        {
            context.BadgeParts.Add(new BadgePart
            {
                BadgeId = badge.Id,
                SortOrder = templatePart.SortOrder,
                IsAdminPart = templatePart.IsAdminPart,
                ShortDescription = templatePart.ShortDescription,
                LongDescription = templatePart.LongDescription
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created badge '{Name}' (ID {BadgeId}) from template {TemplateId} for group {GroupId}",
            badge.Name, badge.Id, templateId, scoutGroupId);

        return badge;
    }

    public async Task<Badge> CreateBadgeAsync(int scoutGroupId, string name, string? description, string? imageUrl, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var badge = new Badge
        {
            ScoutGroupId = scoutGroupId,
            Name = name,
            Description = description,
            ImageUrl = imageUrl
        };

        context.Badges.Add(badge);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created empty badge '{Name}' (ID {BadgeId}) for group {GroupId}",
            name, badge.Id, scoutGroupId);

        return badge;
    }

    public async Task SetArchivedAsync(int badgeId, bool isArchived, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var badge = await context.Badges.FindAsync([badgeId], cancellationToken)
            ?? throw new ArgumentException($"Badge {badgeId} not found");

        badge.IsArchived = isArchived;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Badge {BadgeId} archived={IsArchived}", badgeId, isArchived);
    }

    public async Task<IReadOnlyList<Badge>> GetTroopBadgesAsync(int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.TroopBadges
            .Where(tb => tb.TroopId == troopId)
            .OrderBy(tb => tb.SortOrder)
            .Include(tb => tb.Badge)
                .ThenInclude(b => b.Parts)
            .Select(tb => tb.Badge)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AssignBadgeToTroopAsync(int badgeId, int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var exists = await context.TroopBadges
            .AnyAsync(tb => tb.TroopId == troopId && tb.BadgeId == badgeId, cancellationToken);

        if (exists)
            return;

        var maxSort = await context.TroopBadges
            .Where(tb => tb.TroopId == troopId)
            .Select(tb => (int?)tb.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        context.TroopBadges.Add(new TroopBadge
        {
            TroopId = troopId,
            BadgeId = badgeId,
            SortOrder = maxSort + 1
        });

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Assigned badge {BadgeId} to troop {TroopId}", badgeId, troopId);
    }

    public async Task UnassignBadgeFromTroopAsync(int badgeId, int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var troopBadge = await context.TroopBadges
            .FirstOrDefaultAsync(tb => tb.TroopId == troopId && tb.BadgeId == badgeId, cancellationToken);

        if (troopBadge == null)
            return;

        context.TroopBadges.Remove(troopBadge);
        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Unassigned badge {BadgeId} from troop {TroopId}", badgeId, troopId);
    }
}
