using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for loading data for the "My Profile" (/me) page.
/// </summary>
public class MyProfileService : IMyProfileService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;

    public MyProfileService(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Person?> GetPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Persons.FindAsync([personId], cancellationToken);
    }

    public async Task<IReadOnlyList<MyGroupMembership>> GetGroupMembershipsAsync(int personId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.ScoutGroupPersons
            .Where(sgp => sgp.PersonId == personId && !sgp.NotInScoutnet)
            .Include(sgp => sgp.ScoutGroup)
            .Select(sgp => new MyGroupMembership
            {
                ScoutGroupId = sgp.ScoutGroupId,
                ScoutGroupName = sgp.ScoutGroup.Name,
                Roles = sgp.GroupRoles ?? ""
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MyAttendanceSummary>> GetAttendanceSummaryAsync(int personId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.MeetingAttendances
            .Where(ma => ma.PersonId == personId)
            .Select(ma => new
            {
                ma.Meeting.Troop.Name,
                ma.Meeting.Troop.Semester.Year,
                ma.Meeting.Troop.Semester.IsAutumn
            })
            .GroupBy(x => new { x.Name, x.Year, x.IsAutumn })
            .Select(g => new MyAttendanceSummary
            {
                TroopName = g.Key.Name,
                Year = g.Key.Year,
                IsAutumn = g.Key.IsAutumn,
                AttendedMeetings = g.Count()
            })
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.IsAutumn)
            .ToListAsync(cancellationToken);
    }
}
