using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for Meeting operations.
/// </summary>
public interface IMeetingRepository : IRepository<Meeting, int>
{
    Task<Meeting?> GetWithAttendanceAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Meeting>> GetByTroopAsync(int troopId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all meetings for a troop with their attendance records loaded.
    /// This is more efficient than calling GetWithAttendanceAsync for each meeting.
    /// </summary>
    Task<IReadOnlyList<Meeting>> GetByTroopWithAttendanceAsync(int troopId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Meeting>> GetByTroopAndDateRangeAsync(int troopId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    Task<Meeting?> GetByTroopAndDateAsync(int troopId, DateOnly date, CancellationToken cancellationToken = default);
    Task<Meeting?> GetPreviousMeetingAsync(int troopId, DateOnly beforeDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets attendance for a person at a meeting.
    /// </summary>
    /// <param name="meetingId">The meeting ID</param>
    /// <param name="personId">The person ID</param>
    /// <param name="attending">True to mark as attending, false to remove attendance</param>
    Task SetAttendanceAsync(int meetingId, int personId, bool attending, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Sets attendance for multiple person/meeting combinations in a batch.
    /// </summary>
    Task SetAttendanceBatchAsync(IEnumerable<(int MeetingId, int PersonId, bool Attending)> attendanceChanges, CancellationToken cancellationToken = default);
}
