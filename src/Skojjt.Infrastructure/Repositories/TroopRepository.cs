using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class TroopRepository : Repository<Troop, int>, ITroopRepository
{
    public TroopRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Troop?> GetWithMembersAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Include(t => t.TroopPersons)
                .ThenInclude(tp => tp.Person)
            .Include(t => t.Semester)
            .Include(t => t.ScoutGroup)
                .ThenInclude(sg => sg.ScoutGroupPersons)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Troop?> GetWithMeetingsAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Include(t => t.Meetings.OrderByDescending(m => m.MeetingDate))
                .ThenInclude(m => m.Attendances)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Troop?> GetByScoutnetIdAndSemesterAsync(int scoutnetId, int semesterId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .FirstOrDefaultAsync(t => t.ScoutnetId == scoutnetId && t.SemesterId == semesterId, cancellationToken);
    }

    public async Task<Troop?> GetWithMembersByScoutnetIdAsync(int scoutnetId, int semesterId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Include(t => t.TroopPersons)
                .ThenInclude(tp => tp.Person)
            .Include(t => t.Semester)
            .Include(t => t.ScoutGroup)
                .ThenInclude(sg => sg.ScoutGroupPersons)
            .FirstOrDefaultAsync(t => t.ScoutnetId == scoutnetId && t.SemesterId == semesterId, cancellationToken);
    }

    public async Task<IReadOnlyList<Troop>> GetByScoutGroupAndSemesterAsync(int scoutGroupId, int semesterId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Where(t => t.ScoutGroupId == scoutGroupId && t.SemesterId == semesterId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Troop>> GetByScoutGroupAndSemesterWithMembersAsync(int scoutGroupId, int semesterId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Where(t => t.ScoutGroupId == scoutGroupId && t.SemesterId == semesterId)
            .Include(t => t.TroopPersons)
                .ThenInclude(tp => tp.Person)
            .Include(t => t.Meetings)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Troop>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Troop>()
            .Where(t => t.ScoutGroupId == scoutGroupId)
            .Include(t => t.Semester)
            .OrderByDescending(t => t.SemesterId)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdatePatrolAsync(int troopId, int personId, string? patrol, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var troopPersonSet = context.Set<TroopPerson>();
        var troopPerson = await troopPersonSet
            .FirstOrDefaultAsync(tp => tp.TroopId == troopId && tp.PersonId == personId, cancellationToken);
        
        if (troopPerson != null)
        {
            troopPerson.Patrol = patrol;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AddMemberAsync(int troopId, int personId, bool isLeader = false, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var troopPersonSet = context.Set<TroopPerson>();
        
        // Check if already a member
        var existing = await troopPersonSet
            .FirstOrDefaultAsync(tp => tp.TroopId == troopId && tp.PersonId == personId, cancellationToken);
        
        if (existing == null)
        {
            troopPersonSet.Add(new TroopPerson
            {
                TroopId = troopId,
                PersonId = personId,
                IsLeader = isLeader,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveMemberAsync(int troopId, int personId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var troopPersonSet = context.Set<TroopPerson>();
        
        var troopPerson = await troopPersonSet
            .FirstOrDefaultAsync(tp => tp.TroopId == troopId && tp.PersonId == personId, cancellationToken);
        
        if (troopPerson != null)
        {
            troopPersonSet.Remove(troopPerson);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> IsMemberAsync(int troopId, int personId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TroopPerson>()
            .AnyAsync(tp => tp.TroopId == troopId && tp.PersonId == personId, cancellationToken);
    }

    public async Task SetLeaderStatusAsync(int troopId, int personId, bool isLeader, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var troopPerson = await context.Set<TroopPerson>()
            .FirstOrDefaultAsync(tp => tp.TroopId == troopId && tp.PersonId == personId, cancellationToken);
        
        if (troopPerson != null)
        {
            troopPerson.IsLeader = isLeader;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
