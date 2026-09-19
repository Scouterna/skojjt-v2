using Skojjt.Core.Entities;

namespace Skojjt.Core.Services;

/// <summary>
/// Service for loading data for the "My Profile" (/me) page.
/// </summary>
public interface IMyProfileService
{
    /// <summary>
    /// Gets the person record for the given Scoutnet member ID.
    /// </summary>
    Task<Person?> GetPersonAsync(int personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the scout groups that a person belongs to.
    /// </summary>
    Task<IReadOnlyList<MyGroupMembership>> GetGroupMembershipsAsync(int personId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets attendance summary grouped by troop and semester.
    /// </summary>
    Task<IReadOnlyList<PersonAttendanceSummary>> GetAttendanceSummaryAsync(int personId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A scout group membership for the current user.
/// </summary>
public class MyGroupMembership
{
    public int ScoutGroupId { get; set; }
    public string ScoutGroupName { get; set; } = "";
    public string Roles { get; set; } = "";
}

/// <summary>
/// Attendance summary for a single troop/semester combination.
/// </summary>
public class PersonAttendanceSummary
{
    public string TroopName { get; set; } = "";
    public int Year { get; set; }
    public bool IsAutumn { get; set; }
    public int AttendedMeetings { get; set; }

    /// <summary>
    /// Scout group the troop belongs to. Used to build the troop link.
    /// </summary>
    public int ScoutGroupId { get; set; }

    /// <summary>
    /// Semester id of the troop. Used to build the troop link.
    /// </summary>
    public int SemesterId { get; set; }

    /// <summary>
    /// Scoutnet id of the troop. Used to build the troop link.
    /// </summary>
    public int TroopScoutnetId { get; set; }

    /// <summary>
    /// Number of camp nights (lägernätter) calculated from consecutive hike meeting dates.
    /// N consecutive days = N-1 nights.
    /// </summary>
    public int CampNights { get; set; }

    /// <summary>
    /// Patrol the person belongs to in this troop, when known.
    /// </summary>
    public string? Patrol { get; set; }

    /// <summary>
    /// Whether the person is a leader in this troop.
    /// </summary>
    public bool IsLeader { get; set; }

    public string SemesterDisplayName => $"{(IsAutumn ? "HT" : "VT")} {Year}";
}
