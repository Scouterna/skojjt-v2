using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Simulated ScoutID service for development and testing.
/// Provides fake users with configurable roles and group access.
/// Note: IsAdmin is determined from the Users table in the database, not from this service.
/// </summary>
public class FakeScoutIdService : IScoutIdSimulator
{
    private readonly List<SimulatedScoutIdUser> _testUsers;
    
    /// <summary>
    /// Default development password for all test users.
    /// In development, this is not security-critical since it's a simulated service.
    /// </summary>
    public const string DefaultDevPassword = "scout123";

    public FakeScoutIdService()
    {
        // Initialize with default test users
        _testUsers = CreateDefaultTestUsers();
    }

    public FakeScoutIdService(List<SimulatedScoutIdUser> customUsers)
    {
        _testUsers = customUsers ?? CreateDefaultTestUsers();
    }

    public IReadOnlyList<SimulatedScoutIdUser> GetAvailableUsers() => _testUsers.AsReadOnly();

    public SimulatedScoutIdUser? GetUserByUid(string uid)
        => _testUsers.FirstOrDefault(u => u.Uid == uid);

    public SimulatedScoutIdUser? GetUserByEmail(string email)
        => _testUsers.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Validates a user's password. In development mode, accepts either:
    /// - The default dev password
    /// - The user's email (for convenience)
    /// - An empty password (if allowEmptyPassword is true)
    /// </summary>
    public bool ValidatePassword(SimulatedScoutIdUser user, string? password, bool allowEmptyPassword = true)
    {
        if (allowEmptyPassword && string.IsNullOrEmpty(password))
            return true;
            
        // Accept default dev password or email as password for convenience
        return password == DefaultDevPassword || 
               password == user.Email ||
               password == user.Uid;
    }

    public ScoutIdClaims CreateClaimsForUser(SimulatedScoutIdUser user)
    {
        return new ScoutIdClaims
        {
            Email = user.Email,
            //GroupNo = user.GroupNo,
            //GroupId = user.GroupId,
            Uid = user.Uid,
            DisplayName = user.DisplayName,
            //IsMemberRegistrar = user.IsMemberRegistrar,
            IsAdmin = false, // Will be determined from database by ScoutIdClaimsTransformation
            //GroupRoles = new Dictionary<string, List<string>>(user.GroupRoles),
            AccessibleGroupIds = user.AccessibleGroupIds.ToHashSet(),
			MemberRegistrarGroups = user.IsMemberRegistrar 
				? new HashSet<int> { user.GroupId } 
				: new HashSet<int>()
		};
    }

    private static List<SimulatedScoutIdUser> CreateDefaultTestUsers()
    {
        return
        [
            // Admin/Member Registrar user (admin status is determined from database)
            // Using group ID 1137 which exists in many test databases
            new SimulatedScoutIdUser
            {
                Uid = "12345",
                Email = "admin@test.scout.se",
                DisplayName = "Test Admin",
                GroupNo = "1137",
                GroupId = 1137,
                IsMemberRegistrar = true,
                GroupRoles = new Dictionary<string, List<string>>
                {
                    ["1137"] = [ScoutIdRoles.MemberRegistrar, ScoutIdRoles.GroupLeader]
                },
                AccessibleGroupIds = [1137, 787, 736]
            },

            // Regular leader user
            new SimulatedScoutIdUser
            {
                Uid = "12346",
                Email = "ledare@test.scout.se",
                DisplayName = "Test Ledare",
                GroupNo = "1137",
                GroupId = 1137,
                IsMemberRegistrar = false,
                GroupRoles = new Dictionary<string, List<string>>
                {
                    ["1137"] = [ScoutIdRoles.AssistantGroupLeader]
                },
                AccessibleGroupIds = [1137]
            },

            // Multi-group user
            new SimulatedScoutIdUser
            {
                Uid = "12347",
                Email = "multi@test.scout.se",
                DisplayName = "Multi Grupp Ledare",
                GroupNo = "787",
                GroupId = 787,
                IsMemberRegistrar = true,
                GroupRoles = new Dictionary<string, List<string>>
                {
                    ["1137"] = [ScoutIdRoles.AssistantGroupLeader],
                    ["787"] = [ScoutIdRoles.MemberRegistrar, ScoutIdRoles.GroupLeader]
                },
                AccessibleGroupIds = [1137, 787]
            },

            // Read-only user (no special roles)
            new SimulatedScoutIdUser
            {
                Uid = "12348",
                Email = "readonly@test.scout.se",
                DisplayName = "Läsare Testsson",
                GroupNo = "1137",
                GroupId = 1137,
                IsMemberRegistrar = false,
                GroupRoles = new Dictionary<string, List<string>>(),
                AccessibleGroupIds = [1137]
            }
        ];
    }

    /// <summary>
    /// Creates a custom test user for unit testing.
    /// </summary>
    public static SimulatedScoutIdUser CreateCustomUser(
        string uid,
        string email,
        string displayName,
        int groupId,
        bool isMemberRegistrar = false,
        List<int>? accessibleGroups = null)
    {
        var groupIdStr = groupId.ToString();
        return new SimulatedScoutIdUser
        {
            Uid = uid,
            Email = email,
            DisplayName = displayName,
            GroupNo = groupIdStr,
            GroupId = groupId,
            IsMemberRegistrar = isMemberRegistrar,
            GroupRoles = isMemberRegistrar 
                ? new Dictionary<string, List<string>> { [groupIdStr] = [ScoutIdRoles.MemberRegistrar] }
                : new Dictionary<string, List<string>>(),
            AccessibleGroupIds = accessibleGroups ?? [groupId]
        };
    }
}
