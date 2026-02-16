namespace Skojjt.Core.Entities;

/// <summary>
/// TroopBadge - Badges assigned to a troop.
/// Composite primary key: TroopId + BadgeId.
/// </summary>
public class TroopBadge
{
    public int TroopId { get; set; }

    public int BadgeId { get; set; }

    public int? SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Troop Troop { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
}
