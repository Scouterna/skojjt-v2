using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Implementation of ICurrentUserService that extracts ScoutID information
/// from the current HTTP context's authenticated user.
/// 
/// Access Control: Users can ONLY access scout groups that are in their AccessibleGroupIds.
/// This is determined by ScoutID based on roles like 'leader' or 'member_registrar'.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAdminModeService _adminModeService;
    private ScoutIdClaims? _cachedClaims;
    private IReadOnlyList<int>? _cachedAccessibleGroupIds;

    private static readonly HashSet<int> EmptyIntSet = [];

    public CurrentUserService(IHttpContextAccessor httpContextAccessor, IAdminModeService adminModeService)
    {
        _httpContextAccessor = httpContextAccessor;
        _adminModeService = adminModeService;
    }

    public bool IsAuthenticated => 
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? UserId => GetCurrentUser()?.Uid;

    public string? Email => GetCurrentUser()?.Email;

    public string? DisplayName => GetCurrentUser()?.DisplayName;

    public bool IsAdmin => GetCurrentUser()?.IsAdmin ?? false;

    public bool IsAnyMemberRegistrar
    {
        get
        {
            var user = GetCurrentUser();
            if (user == null) return false;
            if (user.IsAdmin && _adminModeService.IsAdminModeActive) return true;
            return user.MemberRegistrarGroups.Count > 0;
        }
    }

    public ScoutIdClaims? GetCurrentUser()
    {
        if (_cachedClaims != null)
            return _cachedClaims;

        var principal = _httpContextAccessor.HttpContext?.User;
        _cachedClaims = GetUserFromPrincipal(principal);
        return _cachedClaims;
    }

    public ScoutIdClaims? GetUserFromPrincipal(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
            return null;

		var uid = identity.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value;

		if (string.IsNullOrEmpty(uid))
		{
			return null;
		}

		var email = identity.FindFirst(ClaimTypes.Email)?.Value;
		if (string.IsNullOrEmpty(email))
		{
			return null;
		}

        var displayName = identity.FindFirst(ScoutIdClaimTypes.DisplayName)?.Value;
		if (string.IsNullOrEmpty(displayName))
		{
			return null;
		}

        var memberRegistrarGroupsStr = identity.FindFirst(ScoutIdClaimTypes.MemberRegistrarGroups)?.Value ?? "";
        var memberRegistrarGroups = ParseIntSet(memberRegistrarGroupsStr);

        var accessibleGroupsStr = identity.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value ?? "";
        var accessibleGroups = ParseIntSet(accessibleGroupsStr);

        var accessibleTroopsStr = identity.FindFirst(ScoutIdClaimTypes.AccessibleTroops)?.Value ?? "";
        var accessibleTroops = ParseIntSet(accessibleTroopsStr);

        // Check for admin claim
        var isAdminStr = identity.FindFirst(ScoutIdClaimTypes.Admin)?.Value ?? "false";
        var isAdmin = string.Equals(isAdminStr, "true", StringComparison.OrdinalIgnoreCase);

		return new ScoutIdClaims
		{
			Uid = uid,
			Email = email,
			DisplayName = displayName,
			//IsMemberRegistrar = memberRegistrarGroups.Contains(groupId),
			IsAdmin = isAdmin,
			//GroupRoles = groupRoles,
			AccessibleGroupIds = accessibleGroups,
			MemberRegistrarGroups = memberRegistrarGroups,
			AccessibleTroopScoutnetIds = accessibleTroops
		};
    }

    /// <summary>
    /// Checks if the current user has access to the specified scout group.
    /// Admins have access to all groups. Regular users can only access groups
    /// listed in their AccessibleGroupIds from ScoutID claims.
    /// </summary>
    public bool HasGroupAccess(int scoutGroupId)
    {
        var user = GetCurrentUser();
        if (user == null) return false;
        
        // Admins have access to all groups only when admin mode is active
        if (user.IsAdmin && _adminModeService.IsAdminModeActive) return true;
        
        return user.AccessibleGroupIds.Contains(scoutGroupId);
    }

    /// <summary>
    /// Checks if the current user is a member registrar for the specified scout group.
    /// Admins are treated as having member registrar access to all groups.
    /// </summary>
    public bool IsMemberRegistrar(int scoutGroupId)
    {
        var user = GetCurrentUser();
        if (user == null) return false;

        // Admins have member registrar access to all groups when admin mode is active
        if (user.IsAdmin && _adminModeService.IsAdminModeActive) return true;

        // Must have access to the group first
        if (!HasGroupAccess(scoutGroupId))
            return false;

		return user.MemberRegistrarGroups.Contains(scoutGroupId);
	}

	/// <summary>
	/// Checks if the current user has access to a specific troop.
	/// Admins (with admin mode active) and member registrars have access to all troops in their group.
	/// Other leaders only have access to troops they have explicit role claims for.
	/// </summary>
	public bool HasTroopAccess(int scoutGroupId, int troopScoutnetId)
	{
		var user = GetCurrentUser();
		if (user == null) return false;

		// Admins have access to all troops when admin mode is active
		if (user.IsAdmin && _adminModeService.IsAdminModeActive) return true;

		// Must have group access first
		if (!user.AccessibleGroupIds.Contains(scoutGroupId)) return false;

		// Member registrars have access to all troops in their group
		if (user.MemberRegistrarGroups.Contains(scoutGroupId)) return true;

		return user.AccessibleTroopScoutnetIds.Contains(troopScoutnetId);
	}

	/// <summary>
	/// Gets the set of troop Scoutnet IDs the current user has direct access to.
	/// </summary>
	public IReadOnlySet<int> GetAccessibleTroopIds()
	{
		var user = GetCurrentUser();
		if (user == null)
			return EmptyIntSet;
		return user.AccessibleTroopScoutnetIds;
	}

    /// <summary>
    /// Gets all scout group IDs the current user has access to.
    /// Returns empty list if not authenticated.
    /// </summary>
    public IReadOnlyList<int> GetAccessibleGroupIds()
    {
        var user = GetCurrentUser();
        if (user == null)
            return Array.Empty<int>();
        return _cachedAccessibleGroupIds ??= [.. user.AccessibleGroupIds];
	}

    /// <summary>
    /// Throws UnauthorizedAccessException if user doesn't have access to the specified group.
    /// Admins always have access to all groups.
    /// </summary>
    public void RequireGroupAccess(int scoutGroupId)
    {
        if (!HasGroupAccess(scoutGroupId))
        {
            var userId = UserId ?? "unknown";
            throw new UnauthorizedAccessException(
                $"User {userId} does not have access to scout group {scoutGroupId}. " +
                $"Accessible groups: [{string.Join(", ", GetAccessibleGroupIds())}]");
        }
    }

    private static HashSet<int> ParseIntSet(string commaSeparated)
    {
        if (string.IsNullOrEmpty(commaSeparated))
            return [];

        HashSet<int> result = [];
        var source = commaSeparated.AsSpan();
        foreach (var range in source.Split(','))
        {
            var token = source[range].Trim();
            if (!token.IsEmpty && int.TryParse(token, out var id) && id > 0)
            {
                result.Add(id);
            }
        }
        return result;
    }
}
