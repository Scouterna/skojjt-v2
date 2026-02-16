using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class BadgeTemplateRepository : Repository<BadgeTemplate>, IBadgeTemplateRepository
{
    public BadgeTemplateRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<BadgeTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<BadgeTemplate>()
            .FirstOrDefaultAsync(bt => bt.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<BadgeTemplate>> GetAllWithPartsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<BadgeTemplate>()
            .Include(bt => bt.Parts.OrderBy(p => p.SortOrder))
            .OrderBy(bt => bt.Name)
            .ToListAsync(cancellationToken);
    }
}
