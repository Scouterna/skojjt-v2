namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Service for registering new members on the Scoutnet waiting list.
/// </summary>
public interface IScoutnetRegistrationService
{
    /// <summary>
    /// Adds a person to the Scoutnet waiting list for the specified scout group.
    /// On success, also creates the Person and ScoutGroupPerson records in the local database.
    /// Optionally adds the person to a troop.
    /// </summary>
    /// <param name="scoutGroupId">The scout group ID.</param>
    /// <param name="request">The registration request with personal details.</param>
    /// <param name="troopId">Optional troop ID to add the new person to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the registration, including the assigned member number on success.</returns>
    Task<WaitinglistRegistrationResult> AddToWaitinglistAsync(
        int scoutGroupId,
        WaitinglistRegistrationRequest request,
        int? troopId = null,
        CancellationToken cancellationToken = default);
}
