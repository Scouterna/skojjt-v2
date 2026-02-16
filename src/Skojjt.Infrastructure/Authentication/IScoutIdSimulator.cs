using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Interface for simulating ScoutID authentication in development and testing.
/// </summary>
public interface IScoutIdSimulator
{
    /// <summary>
    /// Gets available test users for the simulated ScoutID service.
    /// </summary>
    IReadOnlyList<SimulatedScoutIdUser> GetAvailableUsers();

    /// <summary>
    /// Gets a test user by their UID.
    /// </summary>
    SimulatedScoutIdUser? GetUserByUid(string uid);

    /// <summary>
    /// Gets a test user by their email.
    /// </summary>
    SimulatedScoutIdUser? GetUserByEmail(string email);

    /// <summary>
    /// Creates ScoutID claims for a simulated user.
    /// </summary>
    ScoutIdClaims CreateClaimsForUser(SimulatedScoutIdUser user);
}

/// <summary>
/// Represents a simulated ScoutID user for development and testing.
/// Note: IsSkojjtAdmin is determined from the Users table in the database, not from this class.
/// </summary>
public class SimulatedScoutIdUser
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GroupNo { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public bool IsMemberRegistrar { get; set; }
    public Dictionary<string, List<string>> GroupRoles { get; set; } = new();
    public List<int> AccessibleGroupIds { get; set; } = new();
}
