namespace Skojjt.Core.Authentication;

/// <summary>
/// Represents the claims data received from ScoutID after successful authentication.
/// Maps to the sign_in_attributes from Firebase/ScoutID JWT token.
/// </summary>
public record ScoutIdClaims
{
    /// <summary>
    /// User's email address from Scoutnet.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Scoutnet user ID (person identifier).
    /// </summary>
    public string Uid { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the user.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the user has member registrar role (role 9) for their group.
    /// </summary>
    //public bool IsMemberRegistrar { get; init; }

    /// <summary>
    /// Whether the user is a system administrator.
    /// Determined from the Users table in the database.
    /// </summary>
    public bool IsAdmin { get; init; }

	/// <summary>
	/// Is Member registrar in these groups.
	/// </summary>
	public HashSet<int> MemberRegistrarGroups { get; init; } = new();
	public bool IsMemberRegistrar(int groupId) { return MemberRegistrarGroups.Contains(groupId); }


	/// <summary>
	/// All group IDs the user has access to.
	/// </summary>
	public HashSet<int> AccessibleGroupIds { get; init; } = new();
	public bool IsAccessibleGroupId(int groupId) { return AccessibleGroupIds.Contains(groupId); }

	/// <summary>
	/// Troop Scoutnet IDs the user has direct access to via troop-level role claims.
	/// Member registrars have access to all troops in their group regardless of this set.
	/// </summary>
	public HashSet<int> AccessibleTroopScoutnetIds { get; init; } = new();

	/// <summary>
	/// Checks if the user has access to a specific troop.
	/// Returns true if the user is a member registrar for the group, has the troop in their
	/// accessible troops, or is an admin.
	/// </summary>
	public bool HasTroopAccess(int troopScoutnetId, int scoutGroupId)
	{
		if (IsAdmin) return true;
		if (MemberRegistrarGroups.Contains(scoutGroupId)) return true;
		return AccessibleTroopScoutnetIds.Contains(troopScoutnetId);
	}
}

/// <summary>
/// Known ScoutID role identifiers.
/// </summary>
public static class ScoutIdRoles
{
    /// <summary>
    /// Member registrar role - can manage members in the group.
    /// </summary>
    public const string MemberRegistrar = "9";

    /// <summary>
    /// Group leader role.
    /// </summary>
    public const string GroupLeader = "1";

    /// <summary>
    /// Assistant group leader role.
    /// </summary>
    public const string AssistantGroupLeader = "2";
}
