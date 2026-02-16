namespace Skojjt.Core.Entities;

/// <summary>
/// Badge - Badge definition per scout group.
/// ID is auto-generated sequence (no natural key available).
/// </summary>
public class Badge
{
    public int Id { get; set; }

    public int ScoutGroupId { get; set; }

    /// <summary>
    /// Optional reference to the template this badge was created from.
    /// Allows tracking template origin for potential future sync.
    /// </summary>
    public int? TemplateId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Short descriptions for scout-completed parts.
    /// Kept for backward compatibility with v1 data. New code should use the Parts collection.
    /// </summary>
    public string[] PartsScoutShort { get; set; } = [];

    /// <summary>
    /// Long descriptions for scout-completed parts.
    /// Kept for backward compatibility with v1 data. New code should use the Parts collection.
    /// </summary>
    public string[] PartsScoutLong { get; set; } = [];

    /// <summary>
    /// Short descriptions for admin/leader-verified parts.
    /// Kept for backward compatibility with v1 data. New code should use the Parts collection.
    /// </summary>
    public string[] PartsAdminShort { get; set; } = [];

    /// <summary>
    /// Long descriptions for admin/leader-verified parts.
    /// Kept for backward compatibility with v1 data. New code should use the Parts collection.
    /// </summary>
    public string[] PartsAdminLong { get; set; } = [];

    public string? ImageUrl { get; set; }

    /// <summary>
    /// Soft-archive flag. Archived badges are hidden from new assignment but preserve existing progress.
    /// </summary>
    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ScoutGroup ScoutGroup { get; set; } = null!;
    public BadgeTemplate? Template { get; set; }
    public ICollection<BadgePart> Parts { get; set; } = new List<BadgePart>();
    public ICollection<TroopBadge> TroopBadges { get; set; } = new List<TroopBadge>();
    public ICollection<BadgePartDone> PartsDone { get; set; } = new List<BadgePartDone>();
    public ICollection<BadgeCompleted> Completed { get; set; } = new List<BadgeCompleted>();

    // Computed properties
    public int TotalScoutParts => PartsScoutShort.Length;
    public int TotalAdminParts => PartsAdminShort.Length;
    public int TotalParts => TotalScoutParts + TotalAdminParts;
}
