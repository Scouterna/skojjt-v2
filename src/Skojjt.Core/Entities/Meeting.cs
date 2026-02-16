namespace Skojjt.Core.Entities;

/// <summary>
/// Meeting - Scout meeting/event.
/// Uses auto-increment ID with unique constraint on (TroopId, MeetingDate).
/// </summary>
public class Meeting
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    public int TroopId { get; set; }

    public DateOnly MeetingDate { get; set; }

    public TimeOnly StartTime { get; set; } = new TimeOnly(18, 30);

    public string Name { get; set; } = string.Empty;

    public int DurationMinutes { get; set; } = 90;

    public bool IsHike { get; set; } = false;

	public string Location { get; set; } = string.Empty;

	// Navigation properties
	public Troop Troop { get; set; } = null!;
    public ICollection<MeetingAttendance> Attendances { get; set; } = new List<MeetingAttendance>();
}
