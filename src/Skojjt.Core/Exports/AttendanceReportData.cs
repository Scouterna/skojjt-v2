using Skojjt.Core.Entities;

namespace Skojjt.Core.Exports;

/// <summary>
/// Contains all data needed to generate an attendance report.
/// This is the common data structure passed to all exporters.
/// </summary>
public class AttendanceReportData
{
    /// <summary>
    /// The scout group (kår) for the report.
    /// </summary>
    public required ScoutGroup ScoutGroup { get; init; }

    /// <summary>
    /// The troop (avdelning) for the report.
    /// </summary>
    public required Troop Troop { get; init; }

    /// <summary>
    /// The semester for the report.
    /// </summary>
    public required Semester Semester { get; init; }

    /// <summary>
    /// All persons in the troop with their membership info.
    /// </summary>
    public required IReadOnlyList<TroopPersonInfo> TroopPersons { get; init; }

    /// <summary>
    /// All meetings for the troop in the semester.
    /// </summary>
    public required IReadOnlyList<MeetingInfo> Meetings { get; init; }

    /// <summary>
    /// Default location for meetings.
    /// </summary>
    public string DefaultLocation { get; init; } = string.Empty;

    /// <summary>
    /// Whether to include hike meetings in the report.
    /// </summary>
    public bool IncludeHikeMeetings { get; init; } = true;
}

/// <summary>
/// Information about a person in a troop for reporting.
/// </summary>
public class TroopPersonInfo
{
    public required Person Person { get; init; }
    public required bool IsLeader { get; init; }
    public string? Patrol { get; init; }
}

/// <summary>
/// Information about a meeting for reporting.
/// </summary>
public class MeetingInfo
{
    public required Meeting Meeting { get; init; }
    
    /// <summary>
    /// Person IDs of attendees for this meeting.
    /// </summary>
    public required IReadOnlyList<int> AttendingPersonIds { get; init; }
}
