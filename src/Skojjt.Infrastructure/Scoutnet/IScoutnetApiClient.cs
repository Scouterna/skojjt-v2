namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Client for interacting with the Scoutnet API.
/// </summary>
public interface IScoutnetApiClient
{
    /// <summary>
    /// Fetches the member list for a scout group from Scoutnet.
    /// </summary>
    /// <param name="groupId">The Scoutnet group ID.</param>
    /// <param name="apiKey">The API key for accessing all members.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed member list response.</returns>
    Task<ScoutnetMemberListResponse> GetMemberListAsync(
        int groupId, 
        string apiKey, 
        CancellationToken cancellationToken = default);
}
