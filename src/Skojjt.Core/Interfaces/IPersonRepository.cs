using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for Person operations.
/// </summary>
public interface IPersonRepository : IRepository<Person>
{
    Task<Person?> GetWithTroopsAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> GetActiveByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Person>> SearchByNameAsync(int scoutGroupId, string searchTerm, CancellationToken cancellationToken = default);
}
