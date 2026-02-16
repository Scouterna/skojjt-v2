using Skojjt.Core.Utilities;
namespace Skojjt.Core.Entities;

/// <summary>
/// Person - Individual scout or leader.
/// ID is the Scoutnet member number.
/// A person can belong to multiple scout groups via ScoutGroupPersons.
/// </summary>
public class Person
{
    /// <summary>
    /// Scoutnet member number (primary key).
    /// </summary>
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    public Personnummer? PersonalNumber { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? Mobile { get; set; }

    public string? AltEmail { get; set; }

    public string? MumName { get; set; }

    public string? MumEmail { get; set; }

    public string? MumMobile { get; set; }

    public string? DadName { get; set; }

    public string? DadEmail { get; set; }

    public string? DadMobile { get; set; }

    public string? Street { get; set; }

    public string? ZipCode { get; set; }

    public string? ZipName { get; set; }

    public int[] MemberYears { get; set; } = [];

    public bool Removed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ScoutGroupPerson> ScoutGroupPersons { get; set; } = new List<ScoutGroupPerson>();
    public ICollection<TroopPerson> TroopPersons { get; set; } = new List<TroopPerson>();
    public ICollection<MeetingAttendance> MeetingAttendances { get; set; } = new List<MeetingAttendance>();
    public ICollection<BadgePartDone> BadgePartsDone { get; set; } = new List<BadgePartDone>();
    public ICollection<BadgeCompleted> BadgesCompleted { get; set; } = new List<BadgeCompleted>();

    // Computed properties
    public string FullName => $"{FirstName} {LastName}";

    public int Age
    {
        get
        {
            if (BirthDate == null) return 0;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - BirthDate.Value.Year;
            if (BirthDate.Value > today.AddYears(-age)) age--;
            return age;
        }
    }
}
