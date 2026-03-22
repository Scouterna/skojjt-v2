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

    /// <summary>
    /// Registers a new member on the Scoutnet waiting list.
    /// </summary>
    /// <param name="groupId">The Scoutnet group ID.</param>
    /// <param name="apiKeyWaitinglist">The API key for waiting list registration.</param>
    /// <param name="formData">Form data fields to send to the Scoutnet registration API.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registration result with the assigned member number.</returns>
    Task<WaitinglistRegistrationResult> RegisterMemberAsync(
        int groupId,
        string apiKeyWaitinglist,
        Dictionary<string, string> formData,
        CancellationToken cancellationToken = default);
}
