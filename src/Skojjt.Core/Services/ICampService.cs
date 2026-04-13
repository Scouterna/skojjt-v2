using Skojjt.Core.Entities;

namespace Skojjt.Core.Services;

/// <summary>
/// Result of creating a camp troop.
/// </summary>
public class CampCreationResult
{
    public bool Success { get; set; }
    public Troop? Troop { get; set; }
    public int MeetingsCreated { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of importing participants from a Scoutnet project into a camp troop.
/// </summary>
public class CampImportResult
{
    public bool Success { get; set; }
    public CampCreationResult? CampResult { get; set; }
    public int ParticipantsImported { get; set; }
    public int ParticipantsSkipped { get; set; }
    public List<string> SkippedNames { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A participant preview entry from a Scoutnet project.
/// </summary>
public record CampParticipantPreview(
    int MemberNo,
    string FullName,
    bool Cancelled,
    bool ExistsInDatabase);

/// <summary>
/// Result of fetching project participants for preview.
/// </summary>
public class CampPreviewResult
{
    public bool Success { get; set; }
    public List<CampParticipantPreview> Participants { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of pushing attendance as check-in state to Scoutnet.
/// </summary>
public class CampCheckinResult
{
    public bool Success { get; set; }
    public int CheckedInCount { get; set; }
    public int CheckedOutCount { get; set; }
    public int UnchangedCount { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for creating and managing camp troops.
/// </summary>
public interface ICampService
{
    /// <summary>
    /// Fetches participants from a Scoutnet project and returns a preview
    /// showing which participants exist in the database.
    /// </summary>
    Task<CampPreviewResult> PreviewParticipantsAsync(
        int projectId,
        string projectApiKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a camp troop with auto-generated daily meetings.
    /// </summary>
    Task<CampCreationResult> CreateCampAsync(
        int scoutGroupId,
        int semesterId,
        string name,
        string location,
        DateOnly startDate,
        DateOnly endDate,
        int? scoutnetProjectId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports participants from a Scoutnet project, creates a camp troop, and adds matched members.
    /// </summary>
    Task<CampImportResult> ImportFromScoutnetAsync(
        int scoutGroupId,
        int semesterId,
        int projectId,
        string projectApiKey,
        string campName,
        string location,
        DateOnly startDate,
        DateOnly endDate,
        string? checkinApiKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes attendance as check-in state to Scoutnet for a camp troop.
    /// Requires the troop to have a ScoutnetProjectId and ScoutnetCheckinApiKey.
    /// </summary>
    Task<CampCheckinResult> PushCheckinAsync(
        int troopId,
        IReadOnlyList<(int PersonId, bool Attended)> attendanceState,
        CancellationToken cancellationToken = default);
}
