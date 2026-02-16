using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Skojjt.Web.Hubs;

/// <summary>
/// SignalR hub for real-time attendance synchronization.
/// Allows multiple clients viewing the same troop to see attendance changes in real-time.
/// Also synchronizes meeting additions/deletions and member additions/removals.
/// </summary>
[Authorize]
public class AttendanceHub : Hub
{
    /// <summary>
    /// Join a troop group to receive updates for that troop.
    /// </summary>
    public async Task JoinTroopGroup(int troopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetTroopGroupName(troopId));
    }

    /// <summary>
    /// Leave a troop group when navigating away.
    /// </summary>
    public async Task LeaveTroopGroup(int troopId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetTroopGroupName(troopId));
    }

    /// <summary>
    /// Broadcast attendance change to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastAttendanceChange(int troopId, int personId, int meetingId, bool attending)
    {
        // Send to all clients in the troop group except the sender
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "AttendanceChanged", personId, meetingId, attending);
    }

    /// <summary>
    /// Broadcast patrol change to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastPatrolChange(int troopId, int personId, string? patrol)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "PatrolChanged", personId, patrol);
    }

    /// <summary>
    /// Broadcast meeting addition to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastMeetingAdded(int troopId, int meetingId)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "MeetingAdded", meetingId);
    }

    /// <summary>
    /// Broadcast meeting update to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastMeetingUpdated(int troopId, int meetingId)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "MeetingUpdated", meetingId);
    }

    /// <summary>
    /// Broadcast meeting deletion to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastMeetingDeleted(int troopId, int meetingId)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "MeetingDeleted", meetingId);
    }

    /// <summary>
    /// Broadcast member addition to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastMemberAdded(int troopId, int personId)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "MemberAdded", personId);
    }

    /// <summary>
    /// Broadcast member removal to all clients viewing the same troop.
    /// </summary>
    public async Task BroadcastMemberRemoved(int troopId, int personId)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetTroopGroupName(troopId)),
            "MemberRemoved", personId);
    }

    public static string GetTroopGroupName(int troopId) => $"troop-{troopId}";

    /// <summary>
    /// Safely sends a message to clients, handling disconnection scenarios gracefully.
    /// </summary>
    private static async Task SafeSendAsync(IClientProxy? clientProxy, string method, params object?[] args)
    {
        if (clientProxy is null)
        {
            return;
        }

        try
        {
            await clientProxy.SendCoreAsync(method, args);
        }
        catch (ObjectDisposedException)
        {
            // Circuit has been disposed, ignore
        }
        catch (InvalidOperationException)
        {
            // Connection is no longer available, ignore
        }
    }
}
