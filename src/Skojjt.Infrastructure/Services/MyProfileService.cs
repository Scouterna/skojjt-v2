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

        var attendances = await context.MeetingAttendances
            .Where(ma => ma.PersonId == personId)
            .Select(ma => new
            {
                TroopName = ma.Meeting.Troop.Name,
                ma.Meeting.Troop.Semester.Year,
                ma.Meeting.Troop.Semester.IsAutumn,
                ma.Meeting.MeetingDate,
                ma.Meeting.IsHike
            })
            .ToListAsync(cancellationToken);

        return attendances
            .GroupBy(x => new { x.TroopName, x.Year, x.IsAutumn })
            .Select(g => new MyAttendanceSummary
            {
                TroopName = g.Key.TroopName,
                Year = g.Key.Year,
                IsAutumn = g.Key.IsAutumn,
                AttendedMeetings = g.Count(),
                CampNights = CalculateCampNights(g.Where(a => a.IsHike).Select(a => a.MeetingDate))
            })
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.IsAutumn)
            .ToList();
    }

    /// <summary>
    /// Calculates camp nights from a set of hike meeting dates.
    /// Consecutive days form a camp stay: N consecutive days = N-1 nights.
    /// </summary>
    internal static int CalculateCampNights(IEnumerable<DateOnly> hikeDates)
    {
        var sorted = hikeDates.Distinct().OrderBy(d => d).ToList();
        if (sorted.Count < 2)
            return 0;

        var nights = 0;
        for (var i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].DayNumber - sorted[i - 1].DayNumber == 1)
                nights++;
        }

        return nights;
    }
}
