namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for badge summary in lists.
/// </summary>
public record BadgeSummaryDto(
    int Id,
    string Name,
    string? Description,
    string? ImageUrl,
    int TotalParts
);

/// <summary>
/// DTO for badge detailed information.
/// </summary>
public record BadgeDetailDto(
    int Id,
    int ScoutGroupId,
    string Name,
    string? Description,
    string[] PartsScoutShort,
    string[] PartsScoutLong,
    string[] PartsAdminShort,
    string[] PartsAdminLong,
    string? ImageUrl
);

/// <summary>
/// DTO for badge progress for a person.
/// </summary>
public record BadgeProgressDto(
    int BadgeId,
    string BadgeName,
    int TotalScoutParts,
    int TotalAdminParts,
    int CompletedScoutParts,
    int CompletedAdminParts,
    bool IsCompleted,
    DateOnly? CompletedDate
);

/// <summary>
/// DTO for marking a badge part as done.
/// </summary>
public record BadgePartDoneDto(
    int PersonId,
    int BadgeId,
    int PartIndex,
    bool IsScoutPart,
    string? ExaminerName
);

/// <summary>
/// DTO for badge template.
/// </summary>
public record BadgeTemplateDto(
    int Id,
    string Name,
    string? Description,
    string[] PartsScoutShort,
    string[] PartsScoutLong,
    string[] PartsAdminShort,
    string[] PartsAdminLong,
    string? ImageUrl
);
