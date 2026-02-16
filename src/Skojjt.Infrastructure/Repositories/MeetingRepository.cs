using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

public class MeetingRepository : Repository<Meeting, int>, IMeetingRepository
{
    public MeetingRepository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public async Task<Meeting?> GetWithAttendanceAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Include(m => m.Attendances)
                .ThenInclude(a => a.Person)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Meeting>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Where(m => m.TroopId == troopId)
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Meeting>> GetByTroopWithAttendanceAsync(int troopId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Where(m => m.TroopId == troopId)
            .Include(m => m.Attendances)
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Meeting>> GetByTroopAndDateRangeAsync(int troopId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Where(m => m.TroopId == troopId && m.MeetingDate >= startDate && m.MeetingDate <= endDate)
            .Include(m => m.Attendances)
            .OrderByDescending(m => m.MeetingDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Meeting?> GetByTroopAndDateAsync(int troopId, DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Include(m => m.Attendances)
            .FirstOrDefaultAsync(m => m.TroopId == troopId && m.MeetingDate == date, cancellationToken);
    }

    public async Task<Meeting?> GetPreviousMeetingAsync(int troopId, DateOnly beforeDate, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<Meeting>()
            .Where(m => m.TroopId == troopId && m.MeetingDate < beforeDate)
            .Include(m => m.Attendances)
            .OrderByDescending(m => m.MeetingDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Override DeleteAsync to fetch and delete the entity within the same context.
    /// This avoids issues with detached entities that have navigation properties loaded from a different context.
    /// </summary>
    public override async Task DeleteAsync(Meeting entity, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        
        // Find the entity in this context by ID
        var meetingToDelete = await context.Set<Meeting>().FindAsync([entity.Id], cancellationToken);
        
        if (meetingToDelete != null)
        {
            context.Set<Meeting>().Remove(meetingToDelete);
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SetAttendanceAsync(int meetingId, int personId, bool attending, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var attendanceSet = context.Set<MeetingAttendance>();
        var existing = await attendanceSet
            .FirstOrDefaultAsync(a => a.MeetingId == meetingId && a.PersonId == personId, cancellationToken);

        if (attending)
        {
            if (existing == null)
            {
                attendanceSet.Add(new MeetingAttendance
                {
                    MeetingId = meetingId,
                    PersonId = personId
                });
            }
        }
        else
        {
            if (existing != null)
            {
                attendanceSet.Remove(existing);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAttendanceBatchAsync(IEnumerable<(int MeetingId, int PersonId, bool Attending)> attendanceChanges, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var attendanceSet = context.Set<MeetingAttendance>();
        var changesList = attendanceChanges.ToList();
        
        if (changesList.Count == 0) return;
        
        // Get unique meeting IDs to load existing attendance
        var meetingIds = changesList.Select(c => c.MeetingId).Distinct().ToList();
        var personIds = changesList.Select(c => c.PersonId).Distinct().ToList();
        
        // Load existing attendance records for the relevant meetings and persons
        var existingAttendance = await attendanceSet
            .Where(a => meetingIds.Contains(a.MeetingId) && personIds.Contains(a.PersonId))
            .ToListAsync(cancellationToken);
        
        var existingLookup = existingAttendance
            .ToDictionary(a => (a.MeetingId, a.PersonId));

        foreach (var (meetingId, personId, attending) in changesList)
        {
            var key = (meetingId, personId);
            var exists = existingLookup.TryGetValue(key, out var existing);

            if (attending)
            {
                if (!exists)
                {
                    attendanceSet.Add(new MeetingAttendance
                    {
                        MeetingId = meetingId,
                        PersonId = personId
                    });
                }
            }
            else
            {
                if (exists && existing != null)
                {
                    attendanceSet.Remove(existing);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
