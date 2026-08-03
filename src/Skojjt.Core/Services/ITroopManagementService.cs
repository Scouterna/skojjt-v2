using Skojjt.Core.Entities;

namespace Skojjt.Core.Services;

/// <summary>
/// Result of creating a locally managed troop (not sourced from Scoutnet).
/// </summary>
public class LocalTroopCreationResult
{
    public bool Success { get; set; }
    public Troop? Troop { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service for creating and managing troops that do not originate from Scoutnet.
/// </summary>
public interface ITroopManagementService
{
    /// <summary>
    /// Creates a regular troop (avdelning) that does not exist in Scoutnet. A local
    /// ScoutnetId is allocated from the scout group's reserved range (250–7000).
    /// </summary>
    Task<LocalTroopCreationResult> CreateLocalTroopAsync(
        int scoutGroupId,
        int semesterId,
        string name,
        int? unitTypeId = null,
        TimeOnly? defaultStartTime = null,
        int? defaultDurationMinutes = null,
        string? defaultMeetingLocation = null,
        CancellationToken cancellationToken = default);
}
