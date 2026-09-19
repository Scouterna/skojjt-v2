using System.Globalization;
using Skojjt.Core.Services;

namespace Skojjt.Web.Components.Shared;

/// <summary>
/// View model for a single row in the shared attendance summary table.
/// Optional fields (<see cref="Href"/>, <see cref="Patrol"/>, <see cref="IsLeader"/>)
/// are only populated when the context provides them (e.g. PersonDetail).
/// </summary>
public sealed record AttendanceRow(
    string TroopName,
    string SemesterDisplayName,
    int AttendedMeetings,
    int CampNights,
    string? Href = null,
    string? Patrol = null,
    bool IsLeader = false)
{
    /// <summary>
    /// Maps attendance summaries to rows, linking each troop only when
    /// <paramref name="canAccessTroop"/> says the current user may open it.
    /// </summary>
    public static List<AttendanceRow> FromSummaries(
        IEnumerable<PersonAttendanceSummary> summaries,
        Func<PersonAttendanceSummary, bool> canAccessTroop)
    {
        return summaries
            .Select(a => new AttendanceRow(
                a.TroopName,
                a.SemesterDisplayName,
                a.AttendedMeetings,
                a.CampNights,
                canAccessTroop(a)
                    ? string.Create(CultureInfo.InvariantCulture, $"/sk/{a.ScoutGroupId}/t/{a.SemesterId}/{a.TroopScoutnetId}")
                    : null,
                a.Patrol,
                a.IsLeader))
            .ToList();
    }
}
