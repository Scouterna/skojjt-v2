namespace Skojjt.Core.Entities;

/// <summary>
/// MeetingAttendance - Junction table for meeting attendance.
/// No primary key
/// </summary>
public class MeetingAttendance
{
    public int MeetingId { get; set; }

    public int PersonId { get; set; }

    // Navigation properties
    public Meeting Meeting { get; set; } = null!;
    public Person Person { get; set; } = null!;
}
