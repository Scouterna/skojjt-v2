using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Skojjt.Web.Services;

/// <summary>
/// Singleton service for real-time attendance synchronization between Blazor Server circuits.
/// Uses in-memory pub/sub pattern since all Blazor Server components run on the same server.
/// </summary>
public class AttendanceStateService
{
    private readonly ILogger<AttendanceStateService> _logger;
    private readonly ConcurrentDictionary<string, HandlerRegistration<Func<int, int, int, bool, string, Task>>> _attendanceHandlers = new();
    private readonly ConcurrentDictionary<string, HandlerRegistration<Func<int, int, string?, string, Task>>> _patrolHandlers = new();
    private readonly ConcurrentDictionary<string, HandlerRegistration<Func<int, int, MeetingChangeType, string, Task>>> _meetingHandlers = new();
    private readonly ConcurrentDictionary<string, HandlerRegistration<Func<int, int, TroopMemberChangeType, string, Task>>> _memberHandlers = new();

    public AttendanceStateService(ILogger<AttendanceStateService> logger)
    {
        _logger = logger;
    }

    #region Attendance Handlers

    /// <summary>
    /// Register a handler for attendance changes. Returns an ID that must be used to unregister.
    /// </summary>
    public string RegisterAttendanceHandler(Func<int, int, int, bool, string, Task> handler)
    {
        var id = Guid.NewGuid().ToString();
        _attendanceHandlers[id] = new HandlerRegistration<Func<int, int, int, bool, string, Task>>(handler);
        return id;
    }

    /// <summary>
    /// Unregister an attendance handler by ID.
    /// </summary>
    public void UnregisterAttendanceHandler(string id) => _attendanceHandlers.TryRemove(id, out _);

    /// <summary>
    /// Notify all subscribers about an attendance change.
    /// </summary>
    public Task NotifyAttendanceChangedAsync(int troopId, int personId, int meetingId, bool attending, string sourceInstanceId)
    {
        foreach (var kvp in _attendanceHandlers.ToArray())
        {
            if (kvp.Value.IsDisabled) { _attendanceHandlers.TryRemove(kvp.Key, out _); continue; }
            _ = InvokeHandlerSafelyAsync(kvp.Key, kvp.Value, _attendanceHandlers,
                h => h(troopId, personId, meetingId, attending, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Notify all subscribers about multiple attendance changes.
    /// </summary>
    public Task NotifyAttendanceChangedBatchAsync(int troopId, IEnumerable<(int PersonId, int MeetingId, bool Attending)> changes, string sourceInstanceId)
    {
        var changesList = changes.ToList();
        if (changesList.Count == 0) return Task.CompletedTask;

        foreach (var kvp in _attendanceHandlers.ToArray())
        {
            if (kvp.Value.IsDisabled) { _attendanceHandlers.TryRemove(kvp.Key, out _); continue; }
            _ = InvokeHandlerBatchSafelyAsync(kvp.Key, kvp.Value, _attendanceHandlers, changesList,
                (h, c) => h(troopId, c.PersonId, c.MeetingId, c.Attending, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Patrol Handlers

    /// <summary>
    /// Register a handler for patrol changes. Returns an ID that must be used to unregister.
    /// </summary>
    public string RegisterPatrolHandler(Func<int, int, string?, string, Task> handler)
    {
        var id = Guid.NewGuid().ToString();
        _patrolHandlers[id] = new HandlerRegistration<Func<int, int, string?, string, Task>>(handler);
        return id;
    }

    /// <summary>
    /// Unregister a patrol handler by ID.
    /// </summary>
    public void UnregisterPatrolHandler(string id) => _patrolHandlers.TryRemove(id, out _);

    /// <summary>
    /// Notify all subscribers about a patrol change.
    /// </summary>
    public Task NotifyPatrolChangedAsync(int troopId, int personId, string? patrol, string sourceInstanceId)
    {
        foreach (var kvp in _patrolHandlers.ToArray())
        {
            if (kvp.Value.IsDisabled) { _patrolHandlers.TryRemove(kvp.Key, out _); continue; }
            _ = InvokeHandlerSafelyAsync(kvp.Key, kvp.Value, _patrolHandlers,
                h => h(troopId, personId, patrol, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Meeting Handlers

    /// <summary>
    /// Register a handler for meeting changes. Returns an ID that must be used to unregister.
    /// </summary>
    public string RegisterMeetingHandler(Func<int, int, MeetingChangeType, string, Task> handler)
    {
        var id = Guid.NewGuid().ToString();
        _meetingHandlers[id] = new HandlerRegistration<Func<int, int, MeetingChangeType, string, Task>>(handler);
        return id;
    }

    /// <summary>
    /// Unregister a meeting handler by ID.
    /// </summary>
    public void UnregisterMeetingHandler(string id) => _meetingHandlers.TryRemove(id, out _);

    /// <summary>
    /// Notify all subscribers about a meeting change.
    /// </summary>
    /// <param name="troopId">The troop ID</param>
    /// <param name="meetingId">The meeting ID</param>
    /// <param name="changeType">Type of change (Added, Updated, Deleted)</param>
    /// <param name="sourceInstanceId">The instance ID of the component that made the change</param>
    public Task NotifyMeetingChangedAsync(int troopId, int meetingId, MeetingChangeType changeType, string sourceInstanceId)
    {
        foreach (var kvp in _meetingHandlers.ToArray())
        {
            if (kvp.Value.IsDisabled) { _meetingHandlers.TryRemove(kvp.Key, out _); continue; }
            _ = InvokeHandlerSafelyAsync(kvp.Key, kvp.Value, _meetingHandlers,
                h => h(troopId, meetingId, changeType, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Troop Member Handlers

    /// <summary>
    /// Register a handler for troop member changes. Returns an ID that must be used to unregister.
    /// </summary>
    public string RegisterMemberHandler(Func<int, int, TroopMemberChangeType, string, Task> handler)
    {
        var id = Guid.NewGuid().ToString();
        _memberHandlers[id] = new HandlerRegistration<Func<int, int, TroopMemberChangeType, string, Task>>(handler);
        return id;
    }

    /// <summary>
    /// Unregister a member handler by ID.
    /// </summary>
    public void UnregisterMemberHandler(string id) => _memberHandlers.TryRemove(id, out _);

    /// <summary>
    /// Notify all subscribers about a troop member change.
    /// </summary>
    /// <param name="troopId">The troop ID</param>
    /// <param name="personId">The person ID</param>
    /// <param name="changeType">Type of change (Added, Removed)</param>
    /// <param name="sourceInstanceId">The instance ID of the component that made the change</param>
    public Task NotifyMemberChangedAsync(int troopId, int personId, TroopMemberChangeType changeType, string sourceInstanceId)
    {
        foreach (var kvp in _memberHandlers.ToArray())
        {
            if (kvp.Value.IsDisabled) { _memberHandlers.TryRemove(kvp.Key, out _); continue; }
            _ = InvokeHandlerSafelyAsync(kvp.Key, kvp.Value, _memberHandlers,
                h => h(troopId, personId, changeType, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    #endregion

    #region Helper Methods

    private async Task InvokeHandlerSafelyAsync<THandler>(
        string handlerId,
        HandlerRegistration<THandler> registration,
        ConcurrentDictionary<string, HandlerRegistration<THandler>> handlers,
        Func<THandler, Task> invoke) where THandler : Delegate
    {
        try
        {
            await invoke(registration.Handler).ConfigureAwait(false);
        }
        catch (Exception)
        {
            registration.Disable();
            handlers.TryRemove(handlerId, out _);
        }
    }

    private async Task InvokeHandlerBatchSafelyAsync<THandler, TChange>(
        string handlerId,
        HandlerRegistration<THandler> registration,
        ConcurrentDictionary<string, HandlerRegistration<THandler>> handlers,
        List<TChange> changes,
        Func<THandler, TChange, Task> invoke) where THandler : Delegate
    {
        try
        {
            foreach (var change in changes)
            {
                if (registration.IsDisabled) break;
                await invoke(registration.Handler, change).ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
            registration.Disable();
            handlers.TryRemove(handlerId, out _);
        }
    }

    #endregion

    private sealed class HandlerRegistration<T> where T : Delegate
    {
        private volatile bool _disabled;
        public T Handler { get; }
        public bool IsDisabled => _disabled;

        public HandlerRegistration(T handler) => Handler = handler;
        public void Disable() => _disabled = true;
    }
}

/// <summary>
/// Type of meeting change for real-time sync.
/// </summary>
public enum MeetingChangeType
{
    Added,
    Updated,
    Deleted
}

/// <summary>
/// Type of troop member change for real-time sync.
/// </summary>
public enum TroopMemberChangeType
{
    Added,
    Removed
}
