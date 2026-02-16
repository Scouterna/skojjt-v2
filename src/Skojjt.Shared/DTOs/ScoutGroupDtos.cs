namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for semester data.
/// </summary>
public record SemesterDto(
    int Id,
    int Year,
    bool IsAutumn,
    string DisplayName
);

/// <summary>
/// DTO for scout group summary.
/// </summary>
public record ScoutGroupDto(
    int Id,
    string Name,
    string? OrganisationNumber,
    string? Email,
    string? Phone
);

/// <summary>
/// DTO for scout group detailed information.
/// </summary>
public record ScoutGroupDetailDto(
    int Id,
    string Name,
    string? OrganisationNumber,
    string? AssociationId,
    string MunicipalityId,
    string? BankAccount,
    string? Address,
    string? PostalAddress,
    string? Email,
    string? Phone,
    string? DefaultLocation,
    string? Signatory,
    string? SignatoryPhone,
    string? SignatoryEmail,
    int AttendanceMinYear,
    bool AttendanceInclHike
);
