namespace Skojjt.Core.Services;

/// <summary>
/// Interface for group summary statistics service.
/// Provides attendance statistics aggregated by age, gender, and role.
/// </summary>
public interface IGroupSummaryService
{
    /// <summary>
    /// Generate a summary report for a scout group for a specific year.
    /// </summary>
    /// <param name="scoutGroupId">The scout group ID.</param>
    /// <param name="year">The year to report on.</param>
    /// <param name="includeHikeMeetings">Whether to include hike meetings in attendance counts.</param>
    /// <param name="minMeetingsForYear">Minimum meetings to count a person as "active" for the year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Group summary data.</returns>
    Task<GroupSummaryData> GetGroupSummaryAsync(
        int scoutGroupId,
        int year,
        bool includeHikeMeetings = true,
        int minMeetingsForYear = 10,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Group summary statistics data.
/// </summary>
public class GroupSummaryData
{
    /// <summary>
    /// The year for this summary.
    /// </summary>
    public int Year { get; init; }

    /// <summary>
    /// Minimum meetings required to be counted as active.
    /// </summary>
    public int MinMeetingsRequired { get; init; }

    /// <summary>
    /// Whether hike meetings were included in the counts.
    /// </summary>
    public bool IncludesHikeMeetings { get; init; }

    /// <summary>
    /// Statistics broken down by age group.
    /// </summary>
    public required IReadOnlyList<AgeGroupStats> AgeGroups { get; init; }

    /// <summary>
    /// Statistics for leaders by age group.
    /// </summary>
    public required IReadOnlyList<LeaderStats> Leaders { get; init; }

    /// <summary>
    /// Statistics for board members.
    /// </summary>
    public required BoardMemberStats BoardMembers { get; init; }

    /// <summary>
    /// Total statistics.
    /// </summary>
    public required TotalStats Totals { get; init; }

    /// <summary>
    /// List of email addresses for active members.
    /// </summary>
    public required IReadOnlyList<string> MemberEmails { get; init; }
}

/// <summary>
/// Statistics for an age group.
/// </summary>
public class AgeGroupStats
{
    /// <summary>
    /// Display label for the age group (e.g., "7", "8", "0 - 6", "26 - 64").
    /// </summary>
    public required string AgeLabel { get; init; }

    /// <summary>
    /// Number of women/girls in this age group.
    /// </summary>
    public int Women { get; set; }

    /// <summary>
    /// Number of women/girls who met the minimum meeting requirement.
    /// </summary>
    public int WomenWithMinMeetings { get; set; }

    /// <summary>
    /// Number of men/boys in this age group.
    /// </summary>
    public int Men { get; set; }

    /// <summary>
    /// Number of men/boys who met the minimum meeting requirement.
    /// </summary>
    public int MenWithMinMeetings { get; set; }
}

/// <summary>
/// Statistics for leaders.
/// </summary>
public class LeaderStats
{
    /// <summary>
    /// Age group label (e.g., "t.o.m. 25 år", "över 25 år").
    /// </summary>
    public required string AgeLabel { get; init; }

    /// <summary>
    /// Number of female leaders.
    /// </summary>
    public int Women { get; set; }

    /// <summary>
    /// Number of male leaders.
    /// </summary>
    public int Men { get; set; }
}

/// <summary>
/// Statistics for board members.
/// </summary>
public class BoardMemberStats
{
    /// <summary>
    /// Number of female board members.
    /// </summary>
    public int Women { get; set; }

    /// <summary>
    /// Number of male board members.
    /// </summary>
    public int Men { get; set; }
}

/// <summary>
/// Total statistics across all groups.
/// </summary>
public class TotalStats
{
    /// <summary>
    /// Total number of women/girls.
    /// </summary>
    public int TotalWomen { get; set; }

    /// <summary>
    /// Total number of women/girls who met the minimum meeting requirement.
    /// </summary>
    public int TotalWomenWithMinMeetings { get; set; }

    /// <summary>
    /// Total number of men/boys.
    /// </summary>
    public int TotalMen { get; set; }

    /// <summary>
    /// Total number of men/boys who met the minimum meeting requirement.
    /// </summary>
    public int TotalMenWithMinMeetings { get; set; }
}
