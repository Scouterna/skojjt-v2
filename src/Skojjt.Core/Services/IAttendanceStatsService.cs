namespace Skojjt.Core.Services;

/// <summary>
/// Service for computing attendance statistics per semester for a scout group.
/// Used for attendance-over-time charts on the scout group detail page.
/// </summary>
public interface IAttendanceStatsService
{
    /// <summary>
    /// Gets attendance statistics per semester for a scout group, ordered chronologically.
    /// </summary>
    Task<IReadOnlyList<SemesterAttendanceStats>> GetStatsByScoutGroupAsync(
        int scoutGroupId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Attendance statistics for a single semester.
/// </summary>
public class SemesterAttendanceStats
{
    /// <summary>
    /// The semester ID.
    /// </summary>
    public required int SemesterId { get; init; }

    /// <summary>
    /// Display label, e.g. "HT 2025".
    /// </summary>
    public required string SemesterLabel { get; init; }

    /// <summary>
    /// Total unique members across all troops (non-leaders).
    /// </summary>
    public required int MemberCount { get; init; }

    /// <summary>
    /// Total number of meetings across all troops.
    /// </summary>
    public required int MeetingCount { get; init; }

    /// <summary>
    /// Total attendance records (sum of all attendees across all meetings).
    /// </summary>
    public required int TotalAttendanceCount { get; init; }

    /// <summary>
    /// Average attendance per meeting. Zero if no meetings.
    /// </summary>
    public double AverageAttendancePerMeeting =>
        MeetingCount > 0 ? (double)TotalAttendanceCount / MeetingCount : 0;
}
