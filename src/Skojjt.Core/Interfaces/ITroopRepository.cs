using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for Troop operations.
/// </summary>
public interface ITroopRepository : IRepository<Troop, int>
{
    Task<Troop?> GetWithMembersAsync(int id, CancellationToken cancellationToken = default);
    Task<Troop?> GetWithMeetingsAsync(int id, CancellationToken cancellationToken = default);
    Task<Troop?> GetByScoutnetIdAndSemesterAsync(int scoutnetId, int semesterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a troop by its Scoutnet ID and semester, with members and related data loaded.
    /// Combines the lookup of GetByScoutnetIdAndSemesterAsync with the includes of GetWithMembersAsync.
    /// </summary>
    Task<Troop?> GetWithMembersByScoutnetIdAsync(int scoutnetId, int semesterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Troop>> GetByScoutGroupAndSemesterAsync(int scoutGroupId, int semesterId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all troops for a scout group and semester with their members loaded.
    /// This is more efficient than calling GetWithMembersAsync for each troop.
    /// </summary>
    Task<IReadOnlyList<Troop>> GetByScoutGroupAndSemesterWithMembersAsync(int scoutGroupId, int semesterId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Troop>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default);
    Task UpdatePatrolAsync(int troopId, int personId, string? patrol, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Adds a person as a member of a troop.
    /// </summary>
    Task AddMemberAsync(int troopId, int personId, bool isLeader = false, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Removes a person from a troop.
    /// </summary>
    Task RemoveMemberAsync(int troopId, int personId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Checks if a person is already a member of a troop.
    /// </summary>
    Task<bool> IsMemberAsync(int troopId, int personId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates the leader status for a person in a troop.
    /// </summary>
    Task SetLeaderStatusAsync(int troopId, int personId, bool isLeader, CancellationToken cancellationToken = default);
}
