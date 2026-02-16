using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for BadgeTemplate operations.
/// </summary>
public interface IBadgeTemplateRepository : IRepository<BadgeTemplate>
{
    Task<BadgeTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BadgeTemplate>> GetAllWithPartsAsync(CancellationToken cancellationToken = default);
}
