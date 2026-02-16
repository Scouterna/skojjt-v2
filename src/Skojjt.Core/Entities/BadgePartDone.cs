namespace Skojjt.Core.Entities;

/// <summary>
/// BadgePartDone - Completed badge part.
/// Composite primary key: PersonId + BadgeId + PartIndex + IsScoutPart.
/// </summary>
public class BadgePartDone
{
    public int PersonId { get; set; }

    public int BadgeId { get; set; }

    /// <summary>
    /// Zero-based index of the completed part.
    /// Kept for backward compatibility with v1 data.
    /// </summary>
    public int PartIndex { get; set; }

    /// <summary>
    /// True if this is a scout-completed part, false if admin-verified.
    /// Kept for backward compatibility with v1 data.
    /// </summary>
    public bool IsScoutPart { get; set; }

    /// <summary>
    /// Reference to the normalized BadgePart. Null for v1-migrated data that hasn't been linked yet.
    /// </summary>
    public int? BadgePartId { get; set; }

    public string? ExaminerName { get; set; }

    public DateOnly CompletedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>
    /// When set, indicates this part completion has been revoked/undone.
    /// The record is kept for audit trail purposes.
    /// </summary>
    public DateTime? UndoneAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Person Person { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
    public BadgePart? BadgePart { get; set; }
}
