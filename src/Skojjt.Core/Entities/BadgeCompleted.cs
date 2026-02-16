namespace Skojjt.Core.Entities;

/// <summary>
/// BadgeCompleted - Fully completed badge.
/// Composite primary key: PersonId + BadgeId.
/// </summary>
public class BadgeCompleted
{
    public int PersonId { get; set; }

    public int BadgeId { get; set; }

    public string? Examiner { get; set; }

    public DateOnly CompletedDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Person Person { get; set; } = null!;
    public Badge Badge { get; set; } = null!;
}
