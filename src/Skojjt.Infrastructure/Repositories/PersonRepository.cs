using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class PersonRepository : Repository<Person>, IPersonRepository
{
    public PersonRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Person?> GetWithTroopsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Person>()
            .Include(p => p.TroopPersons)
                .ThenInclude(tp => tp.Troop)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Person>()
            .Where(p => p.ScoutGroupPersons.Any(sgp => sgp.ScoutGroupId == scoutGroupId))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.TroopPersons
            .Where(tp => tp.TroopId == troopId)
            .Include(tp => tp.Person)
            .Select(tp => tp.Person)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> GetActiveByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Person>()
            .Where(p => p.ScoutGroupPersons.Any(sgp => sgp.ScoutGroupId == scoutGroupId) && !p.Removed)
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Person>> SearchByNameAsync(int scoutGroupId, string searchTerm, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var lowerSearch = searchTerm.ToLower();
        return await context.Set<Person>()
            .Where(p => p.ScoutGroupPersons.Any(sgp => sgp.ScoutGroupId == scoutGroupId) && !p.Removed)
            .Where(p => p.FirstName.ToLower().Contains(lowerSearch) || 
                        p.LastName.ToLower().Contains(lowerSearch))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
    }
}
