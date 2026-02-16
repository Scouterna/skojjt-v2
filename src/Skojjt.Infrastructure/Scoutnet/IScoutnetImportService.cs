namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Represents a person that was affected during import.
/// </summary>
public record ImportedPerson(int Id, string FullName, string? TroopName);

/// <summary>
/// Represents a troop that was created during import.
/// </summary>
public record ImportedTroop(int ScoutnetId, string Name, int MemberCount, int LeaderCount);

/// <summary>
/// Represents a scout group that was created during import.
/// </summary>
public record ImportedScoutGroup(int Id, string Name);

/// <summary>
/// Result of a Scoutnet import operation.
/// </summary>
public class ScoutnetImportResult
{
    /// <summary>
    /// Whether the import was successful.
    /// </summary>
    public bool Success { get; set; }

	/// <summary>
	/// Number of scout groups created.
	/// </summary>
	public int ScoutGroupsCreated { get; set; }

    /// <summary>
    /// Number of new persons created.
    /// </summary>
    public int PersonsCreated { get; set; }

    /// <summary>
    /// Number of existing persons updated.
    /// </summary>
    public int PersonsUpdated { get; set; }

    /// <summary>
    /// Number of persons marked as removed (no longer in Scoutnet).
    /// </summary>
    public int PersonsRemoved { get; set; }

    /// <summary>
    /// Number of troops created or updated.
    /// </summary>
    public int TroopsProcessed { get; set; }

    /// <summary>
    /// Number of troop memberships created.
    /// </summary>
    public int TroopMembershipsCreated { get; set; }

    /// <summary>
    /// Number of troop memberships removed.
    /// </summary>
    public int TroopMembershipsRemoved { get; set; }

    /// <summary>
    /// Total duration of the import operation.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Error message if the import failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Detailed log messages from the import process.
    /// </summary>
    public List<string> LogMessages { get; set; } = new();

    /// <summary>
    /// List of persons that were created during import.
    /// </summary>
    public List<ImportedPerson> CreatedPersons { get; set; } = new();

    /// <summary>
    /// List of persons that were marked as removed during import.
    /// </summary>
    public List<ImportedPerson> RemovedPersons { get; set; } = new();

    /// <summary>
    /// List of troops that were created during import.
    /// </summary>
    public List<ImportedTroop> CreatedTroops { get; set; } = new();

    /// <summary>
    /// List of scout groups that were created during import.
    /// </summary>
    public List<ImportedScoutGroup> CreatedScoutGroups { get; set; } = new();

    public override string ToString()
    {
        if (!Success)
            return $"Import failed: {ErrorMessage}";

        return $"Import completed in {Duration.TotalSeconds:F1}s: " +
               $"{PersonsCreated} created, {PersonsUpdated} updated, {PersonsRemoved} removed, " +
               $"{TroopsProcessed} troops, {TroopMembershipsCreated} memberships added, {TroopMembershipsRemoved} removed";
    }
}

/// <summary>
/// Service for importing member data from Scoutnet.
/// </summary>
public interface IScoutnetImportService
{
    /// <summary>
    /// Imports all members for a scout group from Scoutnet.
    /// Creates new persons and updates existing ones.
    /// Also handles troop assignments for the current semester.
    /// </summary>
    /// <param name="scoutGroupId">The ID of the scout group to import for.</param>
    /// <param name="semesterId">The semester ID to assign troop memberships to.</param>
    /// <param name="progress">Optional progress callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the import operation.</returns>
    Task<ScoutnetImportResult> ImportMembersAsync(
        int scoutGroupId,
        int semesterId,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports members from a pre-fetched Scoutnet response (for testing or offline use).
    /// </summary>
    Task<ScoutnetImportResult> ImportFromResponseAsync(
        int scoutGroupId,
        int semesterId,
        ScoutnetMemberListResponse response,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
