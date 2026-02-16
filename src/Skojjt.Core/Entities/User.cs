namespace Skojjt.Core.Entities;

/// <summary>
/// User - System user preferences and access.
/// ID is the ScoutID identifier (Scoutnet UID).
/// </summary>
public class User
{
    /// <summary>
    /// ScoutID identifier / Scoutnet UID (primary key).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Name { get; set; }

    /// <summary>
    /// Display name from ScoutID.
    /// </summary>
    public string? DisplayName { get; set; }

    public int? ActiveSemesterId { get; set; }

    /// <summary>
    /// Last time the user logged in.
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
