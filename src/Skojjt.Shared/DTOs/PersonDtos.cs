namespace Skojjt.Shared.DTOs;

/// <summary>
/// DTO for person summary in lists.
/// </summary>
public record PersonSummaryDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    int? Age,
    bool IsRemoved
);

/// <summary>
/// DTO for person detailed information.
/// </summary>
public record PersonDetailDto(
    int Id,
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? PersonalNumber,
    string? Email,
    string? Phone,
    string? Mobile,
    string? AltEmail,
    string? MumName,
    string? MumEmail,
    string? MumMobile,
    string? DadName,
    string? DadEmail,
    string? DadMobile,
    string? Street,
    string? ZipCode,
    string? ZipName,
    int[] MemberYears,
    bool Removed
)
{
    public string FullName => $"{FirstName} {LastName}";
    public int? Age
    {
        get
        {
            if (BirthDate == null) return null;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value > today.AddYears(-age)) age--;
            return age;
        }
    }
    public string? PostalAddress => !string.IsNullOrEmpty(ZipCode) ? $"{ZipCode} {ZipName}" : null;
}

/// <summary>
/// DTO for creating/updating a person.
/// </summary>
public record PersonUpsertDto(
    string FirstName,
    string LastName,
    DateOnly? BirthDate,
    string? PersonalNumber,
    string? Email,
    string? Phone,
    string? Mobile
);
