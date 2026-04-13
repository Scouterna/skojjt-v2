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
	/// Scoutnet unit type numeric ID. Lower values = younger age groups.
	/// Used for stable age-based sorting in charts.
	/// </summary>
	public int? UnitTypeId { get; set; }

	/// <summary>
	/// When true, prevents users from accidentally editing attendance after it has been reported.
	/// </summary>
	public bool IsLocked { get; set; } = false;

	/// <summary>
	/// Distinguishes regular troops from camps. Default is Regular (0).
	/// </summary>
	public TroopType TroopType { get; set; } = TroopType.Regular;

	/// <summary>
	/// First day of camp. Null for regular troops.
	/// </summary>
	public DateOnly? CampStartDate { get; set; }

	/// <summary>
	/// Last day of camp. Null for regular troops.
	/// </summary>
	public DateOnly? CampEndDate { get; set; }

	/// <summary>
	/// Scoutnet project/activity ID if imported from Scoutnet. Null for manually created troops/camps.
	/// </summary>
	public int? ScoutnetProjectId { get; set; }

	/// <summary>
	/// Scoutnet project checkin API key. Stored to enable pushing attendance
	/// as check-in state back to Scoutnet. Null for non-Scoutnet camps.
	/// </summary>
	public string? ScoutnetCheckinApiKey { get; set; }

	/// <summary>
	/// Whether this troop is a camp.
	/// </summary>
	public bool IsCamp => TroopType == TroopType.Camp;

    // Navigation properties
    public ScoutGroup ScoutGroup { get; set; } = null!;
    public Semester Semester { get; set; } = null!;
    public ICollection<TroopPerson> TroopPersons { get; set; } = new List<TroopPerson>();
    public ICollection<Meeting> Meetings { get; set; } = new List<Meeting>();
    public ICollection<TroopBadge> TroopBadges { get; set; } = new List<TroopBadge>();
}
