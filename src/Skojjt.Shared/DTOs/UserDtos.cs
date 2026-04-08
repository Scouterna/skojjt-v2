namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for user information.
/// </summary>
public record UserDto(
    string Id,
    string Email,
    string? Name,
    int? ScoutGroupId,
    string? ScoutGroupName,
    int? ActiveSemesterId,
    bool HasAccess,
    bool IsAdmin
);

/// <summary>
/// DTO for updating user access.
/// </summary>
public record UserAccessUpdateDto(
    string UserId,
    int? ScoutGroupId,
    bool HasAccess,
    bool IsAdmin
);

/// <summary>
/// DTO for current user context.
/// </summary>
public record CurrentUserDto(
    string Id,
    string Email,
    string? Name,
    int? ScoutGroupId,
    string? ScoutGroupName,
    int? ActiveSemesterId,
    string? ActiveSemesterDisplayName,
    bool HasAccess,
    bool IsGroupAdmin,
    bool IsAdmin,
    bool CanImport
);

/// <summary>
/// DTO for the /api/v1/me endpoint response.
/// </summary>
public record MeResponseDto(
    string Uid,
    string DisplayName,
    string Email,
    bool IsMemberRegistrar,
    IReadOnlyList<ScoutGroupDto> AccessibleGroups,
    IReadOnlyList<int> AccessibleTroopScoutnetIds
);
