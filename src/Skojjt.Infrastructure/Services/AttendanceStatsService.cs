using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Computes attendance statistics per semester using efficient aggregate queries.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public class AttendanceStatsService : IAttendanceStatsService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;

    public AttendanceStatsService(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IReadOnlyList<SemesterAttendanceStats>> GetStatsByScoutGroupAsync(
        int scoutGroupId,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();

        // Get all troop IDs for this group, keyed by semester
        var troopsBySemester = await context.Troops
            .Where(t => t.ScoutGroupId == scoutGroupId)
            .Select(t => new { t.Id, t.SemesterId })
            .ToListAsync(cancellationToken);

        if (troopsBySemester.Count == 0)
            return [];

        var troopIdSet = troopsBySemester.Select(t => t.Id).ToHashSet();

        // Unique non-leader members per semester
        var membersPerSemester = await context.TroopPersons
            .Where(tp => troopIdSet.Contains(tp.TroopId) && !tp.IsLeader)
            .Join(context.Troops,
                tp => tp.TroopId,
                t => t.Id,
                (tp, t) => new { t.SemesterId, tp.PersonId })
            .GroupBy(x => x.SemesterId)
            .Select(g => new
            {
                SemesterId = g.Key,
                MemberCount = g.Select(x => x.PersonId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        // Meeting counts per semester
        var meetingsPerSemester = await context.Meetings
            .Where(m => troopIdSet.Contains(m.TroopId))
            .Join(context.Troops,
                m => m.TroopId,
                t => t.Id,
                (m, t) => new { t.SemesterId })
            .GroupBy(x => x.SemesterId)
            .Select(g => new
            {
                SemesterId = g.Key,
                MeetingCount = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Total attendance records per semester
        var attendancePerSemester = await context.MeetingAttendances
            .Join(context.Meetings,
                a => a.MeetingId,
                m => m.Id,
                (a, m) => new { m.TroopId })
            .Where(x => troopIdSet.Contains(x.TroopId))
            .Join(context.Troops,
                x => x.TroopId,
                t => t.Id,
                (_, t) => new { t.SemesterId })
            .GroupBy(x => x.SemesterId)
            .Select(g => new
            {
                SemesterId = g.Key,
                TotalAttendance = g.Count()
            })
            .ToListAsync(cancellationToken);

        // Load semester entities for display names
        var semesterIds = troopsBySemester.Select(t => t.SemesterId).Distinct().ToList();
        var semesters = await context.Semesters
            .Where(s => semesterIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        var membersLookup = membersPerSemester.ToDictionary(x => x.SemesterId, x => x.MemberCount);
        var meetingsLookup = meetingsPerSemester.ToDictionary(x => x.SemesterId, x => x.MeetingCount);
        var attendanceLookup = attendancePerSemester.ToDictionary(x => x.SemesterId, x => x.TotalAttendance);

        return semesters
            .OrderBy(s => s.Id)
            .Select(s => new SemesterAttendanceStats
            {
                SemesterId = s.Id,
                SemesterLabel = s.DisplayName,
                MemberCount = membersLookup.GetValueOrDefault(s.Id),
                MeetingCount = meetingsLookup.GetValueOrDefault(s.Id),
                TotalAttendanceCount = attendanceLookup.GetValueOrDefault(s.Id)
            })
            .ToList();
    }
}
