namespace Skojjt.Core.Authentication;

/// <summary>
/// Custom claim types used for ScoutID authentication.
/// </summary>
public static class ScoutIdClaimTypes
{
    /// <summary>
    /// Scoutnet user ID (uid).
    /// </summary>
    public const string ScoutnetUid = "scoutid/uid";

    /// <summary>
    /// Scoutnet group number.
    /// </summary>
    //public const string GroupNo = "scoutid/group_no";

    /// <summary>
    /// Scoutnet group ID.
    /// </summary>
    //public const string GroupId = "scoutid/group_id";

    /// <summary>
    /// Display name from ScoutID.
    /// </summary>
    public const string DisplayName = "scoutid/display_name";

    /// <summary>
    /// Whether user is member registrar (comma-separated group IDs).
    /// </summary>
    public const string MemberRegistrarGroups = "scoutid/member_registrar_groups";

    /// <summary>
    /// Accessible group IDs (comma-separated).
    /// </summary>
    public const string AccessibleGroups = "scoutid/accessible_groups";

    /// <summary>
    /// Accessible troop Scoutnet IDs (comma-separated).
    /// Users with troop-level roles only get access to specific troops, not all troops in a group.
    /// </summary>
    public const string AccessibleTroops = "scoutid/accessible_troops";

    /// <summary>
    /// Role assignments in JSON format.
    /// </summary>
    //public const string GroupRoles = "scoutid/group_roles";

    /// <summary>
    /// Whether the user is a system administrator.
    /// </summary>
    public const string Admin = "scoutid/admin";
}
