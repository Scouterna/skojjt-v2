// TroopPerson.cs

namespace Skojjt.Core.Entities;

/// <summary>
/// TroopPerson - Person membership in a troop.
/// Composite primary key: TroopId + PersonId.
/// Patrol assignment is per troop membership, not per person.
/// </summary>
public class TroopPerson
{
    public int TroopId { get; set; }

    public int PersonId { get; set; }

    public bool IsLeader { get; set; } = false;

    /// <summary>
    /// Patrol assignment for this specific troop membership.
    /// A person can have different patrols in different troops.
    /// </summary>
    public string? Patrol { get; set; }

    /// <summary>
    /// Scoutnet patrol ID corresponding to <see cref="Patrol"/>.
    /// Populated from the patrol raw_value during Scoutnet import.
    /// Required for pushing patrol changes back to Scoutnet via UpdateGroupMembership.
    /// </summary>
    public int? PatrolId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Troop Troop { get; set; } = null!;
    public Person Person { get; set; } = null!;
}
