namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for meeting summary in lists.
/// </summary>
public record MeetingSummaryDto(
    int Id,
    int TroopId,
    DateOnly MeetingDate,
    TimeOnly StartTime,
    string Name,
    int DurationMinutes,
    bool IsHike,
    int AttendanceCount
);

/// <summary>
/// DTO for meeting detailed information with attendance.
/// </summary>
public record MeetingDetailDto(
    int Id,
    int TroopId,
    string? TroopName,
    DateOnly MeetingDate,
    TimeOnly StartTime,
    string Name,
    int DurationMinutes,
    bool IsHike,
    List<int> AttendingPersonIds
);

/// <summary>
/// DTO for creating/updating a meeting.
/// </summary>
public record MeetingUpsertDto(
    int TroopId,
    DateOnly MeetingDate,
    TimeOnly StartTime,
    string Name,
    int DurationMinutes,
    bool IsHike
);

/// <summary>
/// DTO for updating meeting attendance.
/// </summary>
public record AttendanceUpdateDto(
    int MeetingId,
    List<int> AttendingPersonIds
);

/// <summary>
/// DTO for single attendance toggle.
/// </summary>
public record AttendanceToggleDto(
    int MeetingId,
    int PersonId,
    bool IsAttending
);
