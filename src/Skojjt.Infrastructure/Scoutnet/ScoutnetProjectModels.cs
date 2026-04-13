using System.Text.Json.Serialization;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Response from api/project/get/participants.
/// Participants keyed by member number (string).
/// </summary>
public class ScoutnetProjectParticipantsResponse
{
    [JsonPropertyName("participants")]
    public Dictionary<string, ScoutnetProjectParticipant> Participants { get; set; } = [];
}

/// <summary>
/// A participant in a Scoutnet project/activity.
/// Schema: https://github.com/Scouterna/scoutnet-api → components/project_member.yaml
/// </summary>
public class ScoutnetProjectParticipant
{
    [JsonPropertyName("member_no")]
    public int MemberNo { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonPropertyName("checked_in")]
    public bool CheckedIn { get; set; }

    [JsonPropertyName("attended")]
    public bool Attended { get; set; }

    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; set; }

    [JsonPropertyName("confirmed")]
    public bool Confirmed { get; set; }

    [JsonPropertyName("member_status")]
    public int MemberStatus { get; set; }

    [JsonPropertyName("group_registration")]
    public bool GroupRegistration { get; set; }

    [JsonPropertyName("date_of_birth")]
    public string? DateOfBirth { get; set; }

    [JsonPropertyName("sex")]
    public string? Sex { get; set; }

    [JsonPropertyName("primary_email")]
    public string? PrimaryEmail { get; set; }

    [JsonPropertyName("registration_date")]
    public string? RegistrationDate { get; set; }

    [JsonPropertyName("cancelled_date")]
    public string? CancelledDate { get; set; }

    /// <summary>
    /// The participant's primary group membership.
    /// Use this instead of the deprecated top-level group_id/group_name fields.
    /// </summary>
    [JsonPropertyName("primary_membership_info")]
    public ScoutnetPrimaryMembershipInfo? PrimaryMembershipInfo { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>
/// Primary membership info nested within a project participant.
/// </summary>
public class ScoutnetPrimaryMembershipInfo
{
    [JsonPropertyName("group_id")]
    public int? GroupId { get; set; }

    [JsonPropertyName("group_name")]
    public string? GroupName { get; set; }

    [JsonPropertyName("troop_id")]
    public int? TroopId { get; set; }

    [JsonPropertyName("troop_name")]
    public string? TroopName { get; set; }

    [JsonPropertyName("patrol_id")]
    public int? PatrolId { get; set; }

    [JsonPropertyName("patrol_name")]
    public string? PatrolName { get; set; }
}

/// <summary>
/// Result from the Scoutnet project checkin API.
/// </summary>
public class ProjectCheckinResult
{
    public bool Success { get; set; }
    public List<int> CheckedIn { get; set; } = [];
    public List<int> CheckedOutAttended { get; set; } = [];
    public List<int> CheckedOutNotAttended { get; set; } = [];
    public List<int> Unchanged { get; set; } = [];
    public List<int> NotFound { get; set; } = [];
    public List<int> NoMember { get; set; } = [];
    public int Total { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A project/activity returned by the viewGroupProjects endpoint (api/organisation/project).
/// The API response format is: [{"Project":{"name":"...", "starts":"...", ...}}, ...]
/// The project ID may or may not be included in the response depending on Scoutnet version.
/// Note: This endpoint is NOT documented in the Scouterna/scoutnet-api OpenAPI spec
/// (https://github.com/Scouterna/scoutnet-api) — only project-level endpoints are covered there.
/// </summary>
public class ScoutnetGroupProject
{
    /// <summary>
    /// Project ID if available in the response. May be null for older API versions.
    /// </summary>
    public int? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime? Starts { get; set; }

    public DateTime? Ends { get; set; }

    public string? Description { get; set; }

    public int? MinAge { get; set; }

    public int? MaxAge { get; set; }

    /// <summary>
    /// Display label combining name and dates for use in dropdowns.
    /// </summary>
    public string DisplayName => Starts.HasValue && Ends.HasValue
        ? $"{Name} ({Starts.Value:d MMM yyyy} – {Ends.Value:d MMM yyyy})"
        : Name;
}
