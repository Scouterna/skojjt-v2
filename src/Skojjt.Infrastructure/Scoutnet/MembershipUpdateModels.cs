namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Represents a membership update for a single member via the Scoutnet UpdateGroupMembership API.
/// Fields are optional — only include the fields you want to change.
/// </summary>
public class MembershipUpdate
{
    /// <summary>
    /// Membership status. Valid values: "confirmed", "waiting", "cancelled".
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Scoutnet troop/unit ID to assign the member to.
    /// </summary>
    public int? TroopId { get; set; }

    /// <summary>
    /// Scoutnet patrol ID to assign the member to.
    /// Must belong to the specified troop (or the member's current troop).
    /// </summary>
    public int? PatrolId { get; set; }
}

/// <summary>
/// Known status values for the Scoutnet UpdateGroupMembership API.
/// </summary>
public static class ScoutnetMembershipStatus
{
    public const string Confirmed = "confirmed";
    public const string Waiting = "waiting";
    public const string Cancelled = "cancelled";
}

/// <summary>
/// Result from the Scoutnet UpdateGroupMembership API.
/// </summary>
public class MembershipUpdateResult
{
    /// <summary>
    /// Whether the entire batch update succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Member numbers that were successfully updated (empty on failure).
    /// </summary>
    public List<int> UpdatedMemberNumbers { get; set; } = [];

    /// <summary>
    /// Per-member, per-field error messages (only populated on failure).
    /// Outer key = member number, inner key = field name, value = error message.
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> Errors { get; set; } = [];
}
