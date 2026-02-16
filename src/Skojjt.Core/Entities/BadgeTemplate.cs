namespace Skojjt.Core.Entities;

/// <summary>
/// BadgeTemplate - Reusable badge templates.
/// ID is auto-generated sequence (no natural key available).
/// </summary>
public class BadgeTemplate
{
    public int Id { get; set; }

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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<BadgePart> Parts { get; set; } = new List<BadgePart>();
    public ICollection<Badge> Badges { get; set; } = new List<Badge>();
}
