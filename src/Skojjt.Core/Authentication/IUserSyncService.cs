namespace Skojjt.Core.Authentication;

/// <summary>
/// Service for synchronizing user data from ScoutID claims to the database.
/// Called during authentication to ensure user records are up-to-date.
/// </summary>
public interface IUserSyncService
{
    /// <summary>
    /// Synchronizes user data from ScoutID claims to the database.
    /// Creates a new user record if one doesn't exist, or updates the existing one.
    /// </summary>
    /// <param name="claims">The ScoutID claims from authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The synchronized user ID.</returns>
    Task<string> SyncUserAsync(ScoutIdClaims claims, CancellationToken cancellationToken = default);
}
