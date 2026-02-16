namespace Skojjt.Core.Entities;

/// <summary>
/// BadgePart - A single requirement/step within a badge definition.
/// Parts are ordered by SortOrder and identified by a stable Id,
/// which survives reordering and insertion without breaking progress data.
/// </summary>
public class BadgePart
{
    public int Id { get; set; }

    /// <summary>
    /// The badge this part belongs to. Null if this is a template part.
    /// </summary>
    public int? BadgeId { get; set; }

    /// <summary>
    /// The badge template this part belongs to. Null if this is a badge part.
    /// </summary>
    public int? BadgeTemplateId { get; set; }

    /// <summary>
    /// Display order within the badge (0-based).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// True if this is an admin/leader-verified part, false if scout-completed.
    /// </summary>
    public bool IsAdminPart { get; set; }

    /// <summary>
    /// Short description shown in column headers and compact views.
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// Long/detailed description shown in expanded views.
    /// </summary>
    public string? LongDescription { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Badge? Badge { get; set; }
    public BadgeTemplate? BadgeTemplate { get; set; }
    public ICollection<BadgePartDone> PartsDone { get; set; } = new List<BadgePartDone>();
}
