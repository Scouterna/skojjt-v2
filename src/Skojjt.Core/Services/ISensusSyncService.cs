namespace Skojjt.Core.Services;

/// <summary>
/// Syncs attendance data from Skojjt to Sensus e-tjänst.
/// </summary>
public interface ISensusSyncService
{
    /// <summary>
    /// Lists available arrangemang in Sensus for the authenticated user.
    /// </summary>
    Task<IReadOnlyList<SensusArrangemangDto>> GetArrangemangAsync(
        SensusCredentials credentials,
        CancellationToken ct = default);

    /// <summary>
    /// Syncs attendance for a troop's meetings to a Sensus arrangemang.
    /// </summary>
    Task<SensusSyncResult> SyncAttendanceAsync(
        SensusCredentials credentials,
        int troopId,
        int arrangemangId,
        CancellationToken ct = default);
}

public record SensusCredentials(string Username, string Password)
{
    public override string ToString() => $"SensusCredentials {{ Username = {Username} }}";
}

public record SensusArrangemangDto(int Id, string Name, int SchemaCount);

public record SensusSyncResult(
    int SyncedCount,
    int SkippedCount,
    int NoMatchCount,
    int ErrorCount,
    int MatchedPersons,
    int TotalPersons,
    List<string> LogMessages);
