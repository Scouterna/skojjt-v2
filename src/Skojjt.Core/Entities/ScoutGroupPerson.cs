namespace Skojjt.Core.Entities;

/// <summary>
/// ScoutGroupPerson - Junction table for person's scout group membership.
/// Tracks which scout groups a person belongs to, independent of troop assignment.
/// Composite primary key: PersonId + ScoutGroupId.
/// </summary>
public class ScoutGroupPerson
{
    public int PersonId { get; set; }

    public int ScoutGroupId { get; set; }

    /// <summary>
    /// Whether the person is no longer in Scoutnet for this specific scout group.
    /// Displayed as 'B' (Borttagen) in attendance pages.
    /// </summary>
    public bool NotInScoutnet { get; set; } = false;

    /// <summary>
    /// Roles the person has within this specific scout group.
    /// Comma-separated list of role names.
    /// </summary>
    public string? GroupRoles { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Person Person { get; set; } = null!;
    public ScoutGroup ScoutGroup { get; set; } = null!;
}
