using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for Badge operations.
/// </summary>
public interface IBadgeRepository : IRepository<Badge>
{
    Task<Badge?> GetWithPartsAsync(int id, CancellationToken cancellationToken = default);
    Task<Badge?> GetWithProgressAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Badge>> GetByScoutGroupAsync(int scoutGroupId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Badge>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default);
    Task<BadgePart> AddPartAsync(BadgePart part, CancellationToken cancellationToken = default);
    Task DeletePartAsync(BadgePart part, CancellationToken cancellationToken = default);
}
