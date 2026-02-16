using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class ScoutGroupRepository : Repository<ScoutGroup, int>, IScoutGroupRepository
{
    public ScoutGroupRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<ScoutGroup?> GetWithAllRelationsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<ScoutGroup>()
            .Include(sg => sg.ScoutGroupPersons)
                .ThenInclude(sgp => sgp.Person)
            .Include(sg => sg.Troops)
            .Include(sg => sg.Badges)
            .FirstOrDefaultAsync(sg => sg.Id == id, cancellationToken);
    }
}
