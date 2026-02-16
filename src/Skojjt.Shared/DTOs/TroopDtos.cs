namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for troop summary in lists.
/// </summary>
public record TroopSummaryDto(
    int Id,
    int ScoutnetId,
    string Name,
    int SemesterId,
    string? SemesterDisplayName,
    int MemberCount
);

/// <summary>
/// DTO for troop detailed information.
/// </summary>
public record TroopDetailDto(
    int Id,
    int ScoutnetId,
    int ScoutGroupId,
    int SemesterId,
    string Name,
    TimeOnly DefaultStartTime,
    int DefaultDurationMinutes,
    bool IsLocked,
    List<TroopMemberDto> Members,
    List<MeetingSummaryDto> Meetings
);

/// <summary>
/// DTO for troop member (person with leadership status).
/// </summary>
public record TroopMemberDto(
    int PersonId,
    string FullName,
    int Age,
    string? Patrol,
    bool IsLeader
);

/// <summary>
/// DTO for creating a new troop.
/// </summary>
public record TroopCreateDto(
    int ScoutnetId,
    string Name,
    int SemesterId,
    TimeOnly? DefaultStartTime,
    int? DefaultDurationMinutes
);
