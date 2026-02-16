namespace Skojjt.Core.Entities;

/// <summary>
/// Troop (Avdelning) - Scout troop per semester.
/// Uses auto-increment ID with unique constraint on (ScoutnetId, SemesterId).
/// </summary>
public class Troop
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Scoutnet troop ID. Required and part of unique constraint with SemesterId.
    /// </summary>
    public int ScoutnetId { get; set; }

    public int ScoutGroupId { get; set; }

    public int SemesterId { get; set; }

    public string Name { get; set; } = string.Empty;

    public TimeOnly DefaultStartTime { get; set; } = new TimeOnly(18, 30);

    public int DefaultDurationMinutes { get; set; } = 90;

	public string ? DefaultMeetingLocation { get; set; }

	/// <summary>
	/// When true, prevents users from accidentally editing attendance after it has been reported.
	/// </summary>
	public bool IsLocked { get; set; } = false;

    // Navigation properties
    public ScoutGroup ScoutGroup { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
    public ICollection<TroopPerson> TroopPersons { get; set; } = new List<TroopPerson>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<TroopBadge> TroopBadges { get; set; } = new List<TroopBadge>();
}
