using System.Security.Claims;

namespace Skojjt.Core.Authentication;

/// <summary>
/// Service to access the current authenticated user's ScoutID information.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the current user's ScoutID claims, or null if not authenticated.
    /// </summary>
    ScoutIdClaims? GetCurrentUser();

    /// <summary>
    /// Gets the current user's ScoutID claims from the provided ClaimsPrincipal.
    /// </summary>
    ScoutIdClaims? GetUserFromPrincipal(ClaimsPrincipal? principal);

    /// <summary>
    /// Checks if the current user has access to the specified scout group.
    /// Users can only access groups listed in their AccessibleGroupIds from ScoutID.
    /// </summary>
    bool HasGroupAccess(int scoutGroupId);

    /// <summary>
    /// Checks if the current user is a member registrar for the specified scout group.
    /// Returns false if user doesn't have access to the group.
    /// </summary>
    bool IsMemberRegistrar(int scoutGroupId);

    /// <summary>
    /// Checks if the current user has access to a specific troop.
    /// Member registrars and admins have access to all troops in their group.
    /// Other leaders only have access to troops they have explicit role claims for.
    /// </summary>
    bool HasTroopAccess(int scoutGroupId, int troopScoutnetId);

    /// <summary>
    /// Gets the set of troop Scoutnet IDs the current user has direct access to.
    /// </summary>
    IReadOnlySet<int> GetAccessibleTroopIds();

    /// <summary>
    /// Checks if the current user has a specific role for a scout group.
    /// Returns false if user doesn't have access to the group.
    /// </summary>
    //bool HasRole(int scoutGroupId, string roleId);

    /// <summary>
    /// Gets all scout group IDs the current user has access to.
    /// </summary>
    IReadOnlyList<int> GetAccessibleGroupIds();

    /// <summary>
    /// Throws UnauthorizedAccessException if user doesn't have access to the specified group.
    /// </summary>
    void RequireGroupAccess(int scoutGroupId);

    /// <summary>
    /// Checks if the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Gets the current user's Scoutnet UID.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Gets the current user's email.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Gets the current user's display name.
    /// </summary>
    string? DisplayName { get; }

    /// <summary>
    /// Gets the current user's primary group ID.
    /// </summary>
    //int? PrimaryGroupId { get; }

    /// <summary>
    /// Checks if the current user is a member registrar for any scout group.
    /// </summary>
    bool IsAnyMemberRegistrar { get; }

    /// <summary>
    /// Checks if the current user is a system administrator.
    /// </summary>
    bool IsAdmin { get; }
}
