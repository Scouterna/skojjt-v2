using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class SemesterRepository : Repository<Semester, int>, ISemesterRepository
{
    public SemesterRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Semester?> GetByYearAndTermAsync(int year, bool isAutumn, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var id = Semester.GenerateId(year, isAutumn);
        return await context.Set<Semester>().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<Semester?> GetCurrentSemesterAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var isAutumn = now.Month >= 7; // July onwards is autumn semester
        var year = now.Year;

        var current = await GetByYearAndTermAsync(year, isAutumn, cancellationToken);
        
        // If current doesn't exist, try to get the most recent one
        if (current == null)
        {
            await using var context = CreateContext();
            current = await context.Set<Semester>()
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return current;
    }

    public async Task<Semester> GetOrCreateCurrentSemesterAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var isAutumn = now.Month >= 7; // July onwards is autumn semester (HT)
        var year = now.Year;

        var current = await GetByYearAndTermAsync(year, isAutumn, cancellationToken);
        
        if (current == null)
        {
            await using var context = CreateContext();
            // Create the semester
            current = new Semester
            {
                Id = Semester.GenerateId(year, isAutumn),
                Year = year,
                IsAutumn = isAutumn
            };
            
            context.Set<Semester>().Add(current);
            await context.SaveChangesAsync(cancellationToken);
        }

        return current;
    }

    public async Task<IReadOnlyList<Semester>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Semester>()
            .OrderByDescending(s => s.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Semester>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Where(t => t.ScoutGroupId == scoutGroupId)
            .Select(t => t.Semester)
            .Distinct()
            .OrderByDescending(s => s.Id)
            .ToListAsync(cancellationToken);
    }
}
