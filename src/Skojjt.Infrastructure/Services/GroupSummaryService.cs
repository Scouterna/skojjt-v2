using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Implementation of group summary statistics service.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public class GroupSummaryService : IGroupSummaryService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;

    // Roles that indicate board membership
    private static readonly string[] BoardMemberRoles = ["Ordförande", "Kassör", "Sekreterare", "Styrelseledamot", "Styrelsesuppleant"];

    public GroupSummaryService(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<GroupSummaryData> GetGroupSummaryAsync(
        int scoutGroupId,
        int year,
        bool includeHikeMeetings = true,
        int minMeetingsForYear = 10,
        CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateDbContext();
        
        // Define age groups as per the old implementation
        const int startAge = 7;
        const int endAge = 25;

        // Initialize age group stats
        var ageGroups = new List<AgeGroupStats>
        {
            new() { AgeLabel = "0 - 6" }
        };
        
        for (int i = startAge; i <= endAge; i++)
        {
            ageGroups.Add(new AgeGroupStats { AgeLabel = i.ToString() });
        }
        
        ageGroups.Add(new AgeGroupStats { AgeLabel = "26 - 64" });
        ageGroups.Add(new AgeGroupStats { AgeLabel = "65 -" });

        var leaders = new List<LeaderStats>
        {
            new() { AgeLabel = "t.o.m. 25 år" },
            new() { AgeLabel = "över 25 år" }
        };

        var boardMembers = new BoardMemberStats();
        var totals = new TotalStats();
        var emails = new List<string>();

        // Date range for the year
        var fromDate = new DateOnly(year, 1, 1);
        var toDate = new DateOnly(year, 12, 31);

        // Get all persons in this scout group who were members this year
        var persons = await context.Set<Person>()
            .Include(p => p.ScoutGroupPersons)
            .Where(p => p.ScoutGroupPersons.Any(sgp => sgp.ScoutGroupId == scoutGroupId))
            .Where(p => p.MemberYears.Contains(year))
            .ToListAsync(cancellationToken);

        // Get ScoutGroupPerson data for board member detection
        var scoutGroupPersons = await context.Set<ScoutGroupPerson>()
            .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
            .ToListAsync(cancellationToken);

        var scoutGroupPersonDict = scoutGroupPersons.ToDictionary(sgp => sgp.PersonId);

        // Get all meetings for this scout group in this year
        var meetingsQuery = context.Set<Meeting>()
            .Include(m => m.Attendances)
            .Where(m => m.Troop.ScoutGroupId == scoutGroupId)
            .Where(m => m.MeetingDate >= fromDate && m.MeetingDate <= toDate);

        if (!includeHikeMeetings)
        {
            meetingsQuery = meetingsQuery.Where(m => !m.IsHike);
        }

        var meetings = await meetingsQuery.ToListAsync(cancellationToken);

        // Create a lookup for person meeting counts
        var personMeetingCounts = meetings
            .SelectMany(m => m.Attendances)
            .GroupBy(a => a.PersonId)
            .ToDictionary(g => g.Key, g => g.Count());

        // Get leader info from TroopPersons
        var leaderPersonIds = await context.Set<TroopPerson>()
            .Where(tp => tp.IsLeader)
            .Where(tp => tp.Troop.ScoutGroupId == scoutGroupId)
            .Where(tp => tp.Troop.Semester.Year == year)
            .Select(tp => tp.PersonId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var leaderPersonIdSet = leaderPersonIds.ToHashSet();

        // Process each person
        foreach (var person in persons)
        {
            // Collect emails
            if (!string.IsNullOrEmpty(person.Email) && !emails.Contains(person.Email))
            {
                emails.Add(person.Email);
            }

			if (person.PersonalNumber is null)
				continue; // Skip if personal number is missing, as we can't

			var age = person.BirthDate.HasValue 
                ? year - person.BirthDate.Value.Year 
                : 0;

            var isFemale = person.PersonalNumber.IsFemale;
            personMeetingCounts.TryGetValue(person.Id, out var meetingCount);
            var meetsMinimum = meetingCount >= minMeetingsForYear;

            // Determine age group index
            int ageGroupIndex;
            if (age < 7)
                ageGroupIndex = 0;
            else if (age >= 7 && age <= 25)
                ageGroupIndex = age - startAge + 1;
            else if (age >= 26 && age <= 64)
                ageGroupIndex = endAge - startAge + 2;
            else
                ageGroupIndex = endAge - startAge + 3;

            // Update age group stats
            if (isFemale)
            {
                ageGroups[ageGroupIndex].Women++;
                totals.TotalWomen++;
                if (meetsMinimum)
                {
                    ageGroups[ageGroupIndex].WomenWithMinMeetings++;
                    totals.TotalWomenWithMinMeetings++;
                }
            }
            else
            {
                ageGroups[ageGroupIndex].Men++;
                totals.TotalMen++;
                if (meetsMinimum)
                {
                    ageGroups[ageGroupIndex].MenWithMinMeetings++;
                    totals.TotalMenWithMinMeetings++;
                }
            }

            // Update leader stats
            if (leaderPersonIdSet.Contains(person.Id))
            {
                var leaderIndex = age <= 25 ? 0 : 1;
                if (isFemale)
                    leaders[leaderIndex].Women++;
                else
                    leaders[leaderIndex].Men++;
            }

            // Update board member stats (check GroupRoles for board-related roles)
            if (scoutGroupPersonDict.TryGetValue(person.Id, out var sgp) && IsBoardMember(sgp.GroupRoles))
            {
                if (isFemale)
                    boardMembers.Women++;
                else
                    boardMembers.Men++;
            }
        }

        // Add totals row to age groups
        ageGroups.Add(new AgeGroupStats
        {
            AgeLabel = "Totalt",
            Women = totals.TotalWomen,
            WomenWithMinMeetings = totals.TotalWomenWithMinMeetings,
            Men = totals.TotalMen,
            MenWithMinMeetings = totals.TotalMenWithMinMeetings
        });

        return new GroupSummaryData
        {
            Year = year,
            MinMeetingsRequired = minMeetingsForYear,
            IncludesHikeMeetings = includeHikeMeetings,
            AgeGroups = ageGroups,
            Leaders = leaders,
            BoardMembers = boardMembers,
            Totals = totals,
            MemberEmails = emails
        };
    }

    private static bool IsFemale(string? personnummer)
    {
        if (string.IsNullOrEmpty(personnummer) || personnummer.Length < 11)
            return false;
        
        return int.TryParse(personnummer[^2].ToString(), out var digit) && (digit & 1) == 0;
    }

    private static bool IsBoardMember(string? groupRoles)
    {
        if (string.IsNullOrEmpty(groupRoles))
            return false;

        var roles = groupRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return roles.Any(role => BoardMemberRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
    }
}
