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

    /// <summary>
    /// Updates membership data (status, troop, patrol) for one or more members
    /// via the Scoutnet UpdateGroupMembership API.
    /// </summary>
    /// <param name="groupId">The Scoutnet group ID.</param>
    /// <param name="apiKey">The API key for the update membership endpoint.</param>
    /// <param name="updates">Dictionary of member number → fields to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result indicating which members were updated or any errors.</returns>
    Task<MembershipUpdateResult> UpdateMembershipAsync(
        int groupId,
        string apiKey,
        Dictionary<int, MembershipUpdate> updates,
        CancellationToken cancellationToken = default);
}
