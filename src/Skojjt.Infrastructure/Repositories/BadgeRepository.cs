using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class BadgeRepository : Repository<Badge>, IBadgeRepository
{
    public BadgeRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Badge?> GetWithPartsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Badge>()
            .Include(b => b.Parts.OrderBy(p => p.SortOrder))
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<Badge?> GetWithProgressAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Badge>()
            .Include(b => b.Parts.OrderBy(p => p.SortOrder))
            .Include(b => b.PartsDone)
            .Include(b => b.Completed)
            .Include(b => b.TroopBadges)
                .ThenInclude(tb => tb.Troop)
                .ThenInclude(t => t.Semester)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Badge>> GetByScoutGroupAsync(int scoutGroupId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var query = context.Set<Badge>()
            .Where(b => b.ScoutGroupId == scoutGroupId);

        if (!includeArchived)
            query = query.Where(b => !b.IsArchived);

        return await query
            .OrderBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Badge>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.TroopBadges
            .Where(tb => tb.TroopId == troopId)
            .Include(tb => tb.Badge)
            .OrderBy(tb => tb.SortOrder)
            .Select(tb => tb.Badge)
            .ToListAsync(cancellationToken);
    }

    public async Task<BadgePart> AddPartAsync(BadgePart part, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.BadgeParts.Add(part);
        await context.SaveChangesAsync(cancellationToken);
        return part;
    }

    public async Task DeletePartAsync(BadgePart part, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.BadgeParts.Remove(context.BadgeParts.Attach(new BadgePart { Id = part.Id }).Entity);
        await context.SaveChangesAsync(cancellationToken);
    }
}
