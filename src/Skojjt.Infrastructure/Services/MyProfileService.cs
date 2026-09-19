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

    public async Task<IReadOnlyList<PersonAttendanceSummary>> GetAttendanceSummaryAsync(int personId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Aggregate the attended-meeting counts on the database instead of pulling every
        // attendance row into memory. This returns one row per (troop, semester) group.
        var counts = await context.MeetingAttendances
            .Where(ma => ma.PersonId == personId)
            .GroupBy(ma => new
            {
                TroopName = ma.Meeting.Troop.Name,
                ma.Meeting.Troop.ScoutGroupId,
                ma.Meeting.Troop.SemesterId,
                TroopScoutnetId = ma.Meeting.Troop.ScoutnetId,
                ma.Meeting.Troop.Semester.Year,
                ma.Meeting.Troop.Semester.IsAutumn
            })
            .Select(g => new
            {
                g.Key.TroopName,
                g.Key.ScoutGroupId,
                g.Key.SemesterId,
                g.Key.TroopScoutnetId,
                g.Key.Year,
                g.Key.IsAutumn,
                AttendedMeetings = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Camp nights need per-date logic that cannot run in SQL, but only hike meetings
        // are relevant, so fetch just those dates (a much smaller set).
        var hikeDates = await context.MeetingAttendances
            .Where(ma => ma.PersonId == personId && ma.Meeting.IsHike)
            .Select(ma => new
            {
                ma.Meeting.Troop.ScoutGroupId,
                ma.Meeting.Troop.SemesterId,
                TroopScoutnetId = ma.Meeting.Troop.ScoutnetId,
                ma.Meeting.MeetingDate
            })
            .ToListAsync(cancellationToken);

        var campNightsByGroup = hikeDates
            .GroupBy(x => new { x.ScoutGroupId, x.SemesterId, x.TroopScoutnetId })
            .ToDictionary(g => g.Key, g => CalculateCampNights(g.Select(a => a.MeetingDate)));

        // Troop memberships (including troops the person belongs to but has no attendance in,
        // e.g. leader/admin troops) so they appear as 0/0 rows alongside attended troops.
        var memberships = await context.TroopPersons
            .Where(tp => tp.PersonId == personId)
            .Select(tp => new
            {
                TroopName = tp.Troop.Name,
                tp.Troop.ScoutGroupId,
                tp.Troop.SemesterId,
                TroopScoutnetId = tp.Troop.ScoutnetId,
                tp.Troop.Semester.Year,
                tp.Troop.Semester.IsAutumn,
                tp.Patrol,
                tp.IsLeader
            })
            .ToListAsync(cancellationToken);

        var membershipByTroop = memberships
            .GroupBy(m => new { m.ScoutGroupId, m.SemesterId, m.TroopScoutnetId })
            .ToDictionary(g => g.Key, g => g.First());

        var results = counts
            .Select(c =>
            {
                membershipByTroop.TryGetValue(
                    new { c.ScoutGroupId, c.SemesterId, c.TroopScoutnetId },
                    out var membership);

                return new PersonAttendanceSummary
                {
                    TroopName = c.TroopName,
                    Year = c.Year,
                    IsAutumn = c.IsAutumn,
                    ScoutGroupId = c.ScoutGroupId,
                    SemesterId = c.SemesterId,
                    TroopScoutnetId = c.TroopScoutnetId,
                    AttendedMeetings = c.AttendedMeetings,
                    CampNights = campNightsByGroup.TryGetValue(new { c.ScoutGroupId, c.SemesterId, c.TroopScoutnetId }, out var nights) ? nights : 0,
                    Patrol = membership?.Patrol,
                    IsLeader = membership?.IsLeader ?? false
                };
            })
            .ToList();

        var attendedTroops = counts
            .Select(c => new { c.ScoutGroupId, c.SemesterId, c.TroopScoutnetId })
            .ToHashSet();

        results.AddRange(membershipByTroop
            .Where(kvp => !attendedTroops.Contains(kvp.Key))
            .Select(kvp => new PersonAttendanceSummary
            {
                TroopName = kvp.Value.TroopName,
                Year = kvp.Value.Year,
                IsAutumn = kvp.Value.IsAutumn,
                ScoutGroupId = kvp.Value.ScoutGroupId,
                SemesterId = kvp.Value.SemesterId,
                TroopScoutnetId = kvp.Value.TroopScoutnetId,
                AttendedMeetings = 0,
                CampNights = 0,
                Patrol = kvp.Value.Patrol,
                IsLeader = kvp.Value.IsLeader
            }));

        return results
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.IsAutumn)
            .ThenBy(r => r.TroopName)
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
