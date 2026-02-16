namespace Skojjt.Core.Services;

/// <summary>
/// Service for Skojjt system administration operations.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Deletes a scout group and all related data.
    /// Persons that only belong to this scout group will also be deleted.
    /// Persons that belong to multiple groups will only have their membership in this group removed.
    /// </summary>
    /// <param name="scoutGroupId">The ID of the scout group to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing information about the deletion.</returns>
    Task<ScoutGroupDeletionResult> DeleteScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about what would be deleted if a scout group is removed.
    /// </summary>
    /// <param name="scoutGroupId">The ID of the scout group to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Statistics about the deletion impact.</returns>
    Task<ScoutGroupDeletionPreview> PreviewScoutGroupDeletionAsync(int scoutGroupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a scout group deletion operation.
/// </summary>
public class ScoutGroupDeletionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int PersonsDeleted { get; set; }
    public int PersonMembershipsRemoved { get; set; }
    public int TroopsDeleted { get; set; }
    public int MeetingsDeleted { get; set; }
    public int BadgesDeleted { get; set; }
}

/// <summary>
/// Preview information about what would be deleted when removing a scout group.
/// </summary>
public class ScoutGroupDeletionPreview
{
    public int ScoutGroupId { get; set; }
    public string ScoutGroupName { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of persons that will be permanently deleted (only in this group).
    /// </summary>
    public int PersonsToDelete { get; set; }
    
    /// <summary>
    /// Number of persons that will have their membership removed but remain in other groups.
    /// </summary>
    public int PersonMembershipsToRemove { get; set; }
    
    public int TroopsToDelete { get; set; }
    public int MeetingsToDelete { get; set; }
    public int BadgesToDelete { get; set; }
}
