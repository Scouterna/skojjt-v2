namespace Skojjt.Core.Services;

/// <summary>
/// A single pending membership change to push to Scoutnet.
/// </summary>
public record MembershipChange(
    int MemberNo,
    string MemberName,
    int? NewTroopId,
    string? NewTroopName,
    int? NewPatrolId,
    string? NewPatrolName,
    string? CurrentTroopName,
    string? CurrentPatrolName);

/// <summary>
/// Result of previewing membership changes before pushing to Scoutnet.
/// </summary>
public class MembershipSyncPreview
{
    public List<MembershipChange> TroopChanges { get; set; } = [];
    public List<MembershipChange> PatrolChanges { get; set; } = [];

    /// <summary>
    /// Members in Skojjt troops whose ScoutnetId does not appear in the
    /// Scoutnet member list, meaning the troop is locally created and cannot be synced.
    /// </summary>
    public List<string> SkippedLocalTroopMembers { get; set; } = [];

    /// <summary>
    /// Members whose patrol was changed in Skojjt but have no PatrolId,
    /// so the change cannot be mapped to a Scoutnet patrol_id.
    /// </summary>
    public List<string> UnmappedPatrolWarnings { get; set; } = [];

    public int TotalChanges => TroopChanges.Count + PatrolChanges.Count;
}

/// <summary>
/// Result of pushing membership changes to Scoutnet.
/// </summary>
public class MembershipSyncResult
{
    public bool Success { get; set; }
    public int UpdatedCount { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Details { get; set; } = [];
}

/// <summary>
/// Service for syncing troop and patrol assignments from Skojjt back to Scoutnet.
/// Compares Skojjt's current assignments with Scoutnet's data and pushes differences.
/// </summary>
public interface IMembershipSyncService
{
    /// <summary>
    /// Previews what troop/patrol changes would be pushed to Scoutnet.
    /// Fetches current Scoutnet data and compares with Skojjt assignments.
    /// </summary>
    Task<MembershipSyncPreview> PreviewChangesAsync(
        int scoutGroupId,
        int semesterId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes the given membership changes to Scoutnet via UpdateGroupMembership API.
    /// </summary>
    Task<MembershipSyncResult> PushChangesAsync(
        int scoutGroupId,
        IReadOnlyList<MembershipChange> changes,
        CancellationToken cancellationToken = default);
}
