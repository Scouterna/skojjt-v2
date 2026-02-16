using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for ScoutGroup operations.
/// </summary>
public interface IScoutGroupRepository : IRepository<ScoutGroup, int>
{
    Task<ScoutGroup?> GetWithAllRelationsAsync(int id, CancellationToken cancellationToken = default);
}
