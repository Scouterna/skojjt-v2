using Skojjt.Core.Entities;

namespace Skojjt.Core.Services;

/// <summary>
/// Service for badge operations including progress tracking and badge management.
/// Encapsulates business logic such as auto-complete detection and undo support.
/// </summary>
public interface IBadgeService
{
    /// <summary>
    /// Gets a badge with its normalized parts, ordered by sort order.
    /// </summary>
    Task<Badge?> GetBadgeWithPartsAsync(int badgeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all badges for a scout group, optionally including archived badges.
    /// </summary>
    Task<IReadOnlyList<Badge>> GetBadgesForGroupAsync(int scoutGroupId, bool includeArchived = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the progress matrix for a badge within a troop:
    /// which persons have completed which parts.
    /// </summary>
    Task<BadgeTroopProgress> GetTroopProgressAsync(int badgeId, int troopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all badge progress for a single person across all badges they have started.
    /// </summary>
    Task<IReadOnlyList<BadgePersonSummary>> GetPersonBadgesAsync(int personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles a badge part for a person. If not done, marks it done. If already done, marks it undone.
    /// Automatically creates or removes BadgeCompleted when all parts are done/undone.
    /// Returns the new state of the part.
    /// </summary>
    Task<TogglePartResult> TogglePartAsync(int badgeId, int badgePartId, int personId, string examinerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new badge for a scout group from a template.
    /// Copies template parts into normalized BadgePart entities.
    /// </summary>
    Task<Badge> CreateFromTemplateAsync(int templateId, int scoutGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new empty badge for a scout group.
    /// </summary>
    Task<Badge> CreateBadgeAsync(int scoutGroupId, string name, string? description, string? imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives or unarchives a badge. Archived badges are hidden from new assignment
    /// but preserve existing progress data.
    /// </summary>
    Task SetArchivedAsync(int badgeId, bool isArchived, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the badges currently assigned to a troop.
    /// </summary>
    Task<IReadOnlyList<Badge>> GetTroopBadgesAsync(int troopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns a badge to a troop so members can start working on it.
    /// </summary>
    Task AssignBadgeToTroopAsync(int badgeId, int troopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a badge assignment from a troop. Does not delete progress data.
    /// </summary>
    Task UnassignBadgeFromTroopAsync(int badgeId, int troopId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress data for a badge across all members of a troop.
/// </summary>
public class BadgeTroopProgress
{
    public Badge Badge { get; set; } = null!;
    public IReadOnlyList<BadgePart> Parts { get; set; } = [];
    public IReadOnlyList<PersonPartProgress> PersonProgress { get; set; } = [];
}

/// <summary>
/// One person's progress on a badge, with per-part completion status.
/// </summary>
public class PersonPartProgress
{
    public Person Person { get; set; } = null!;

    /// <summary>
    /// Set of completed (and not undone) BadgePart IDs for this person.
    /// </summary>
    public HashSet<int> CompletedPartIds { get; set; } = [];

    /// <summary>
    /// Whether all parts are done and the badge is fully completed.
    /// </summary>
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Summary of a person's progress on a single badge.
/// </summary>
public class BadgePersonSummary
{
    public Badge Badge { get; set; } = null!;
    public int TotalParts { get; set; }
    public int CompletedParts { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// Result of toggling a badge part.
/// </summary>
public class TogglePartResult
{
    public bool IsDone { get; set; }
    public bool BadgeCompleted { get; set; }
    public bool BadgeUncompleted { get; set; }
}
