using Skojjt.Web.Services;

namespace Skojjt.Web.Services;

/// <summary>
/// Service for broadcasting attendance changes to other Blazor circuits.
/// Uses the singleton AttendanceStateService for in-memory pub/sub.
/// </summary>
public class AttendanceNotificationService
{
    private readonly AttendanceStateService _stateService;

    public AttendanceNotificationService(AttendanceStateService stateService)
    {
        _stateService = stateService;
    }

    /// <summary>
    /// Notify all other components viewing a troop about an attendance change.
    /// </summary>
    public Task NotifyAttendanceChangedAsync(int troopId, int personId, int meetingId, bool attending, string sourceInstanceId)
        => _stateService.NotifyAttendanceChangedAsync(troopId, personId, meetingId, attending, sourceInstanceId);

    /// <summary>
    /// Notify all other components viewing a troop about multiple attendance changes.
    /// </summary>
    public Task NotifyAttendanceChangedBatchAsync(int troopId, IEnumerable<(int PersonId, int MeetingId, bool Attending)> changes, string sourceInstanceId)
        => _stateService.NotifyAttendanceChangedBatchAsync(troopId, changes, sourceInstanceId);

    /// <summary>
    /// Notify all other components viewing a troop about a patrol change.
    /// </summary>
    public Task NotifyPatrolChangedAsync(int troopId, int personId, string? patrol, string sourceInstanceId)
        => _stateService.NotifyPatrolChangedAsync(troopId, personId, patrol, sourceInstanceId);

    /// <summary>
    /// Notify all other components viewing a troop about a meeting change.
    /// </summary>
    public Task NotifyMeetingChangedAsync(int troopId, int meetingId, MeetingChangeType changeType, string sourceInstanceId)
        => _stateService.NotifyMeetingChangedAsync(troopId, meetingId, changeType, sourceInstanceId);

    /// <summary>
    /// Notify all other components viewing a troop about a member change.
    /// </summary>
    public Task NotifyMemberChangedAsync(int troopId, int personId, TroopMemberChangeType changeType, string sourceInstanceId)
        => _stateService.NotifyMemberChangedAsync(troopId, personId, changeType, sourceInstanceId);
}
