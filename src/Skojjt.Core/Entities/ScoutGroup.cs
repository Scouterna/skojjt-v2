// ScoutGroup.cs

namespace Skojjt.Core.Entities;

/// <summary>
/// Scout Group (Kår) - Local scout organization.
/// ID is the Scoutnet group ID.
/// </summary>
public class ScoutGroup
{
    /// <summary>
    /// Scoutnet group ID (primary key).
    /// </summary>
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? OrganisationNumber { get; set; }

    public string? AssociationId { get; set; }

    /// <summary>
    /// Municipality ID for attendance reporting (e.g., DAK export).
    /// Must be configured in scout group settings.
    /// </summary>
    public string? MunicipalityId { get; set; }

    public string? ApiKeyWaitinglist { get; set; }

    public string? ApiKeyAllMembers { get; set; }

    public string? BankAccount { get; set; }

    public string? Address { get; set; }

    public string? PostalAddress { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? DefaultCampLocation { get; set; }

	public string? DefaultMeetingLocation { get; set; }

	public string? Signatory { get; set; }

    public string? SignatoryPhone { get; set; }

    public string? SignatoryEmail { get; set; }

    public int AttendanceMinYear { get; set; } = 10;

    /// <summary>
    /// Minimum number of meetings per semester for attendance grant eligibility (Gothenburg CSV).
    /// </summary>
    public int AttendanceMinSemester { get; set; } = 5;

    public bool AttendanceInclHike { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ScoutGroupPerson> ScoutGroupPersons { get; set; } = new List<ScoutGroupPerson>();
    public ICollection<Troop> Troops { get; set; } = new List<Troop>();
    public ICollection<Badge> Badges { get; set; } = new List<Badge>();
}
