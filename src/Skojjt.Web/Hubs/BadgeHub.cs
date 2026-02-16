using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Skojjt.Web.Hubs;

/// <summary>
/// SignalR hub for real-time badge progress synchronization.
/// Allows multiple leaders viewing the same troop badge to see changes in real-time.
/// </summary>
[Authorize]
public class BadgeHub : Hub
{
    /// <summary>
    /// Join a badge-troop group to receive progress updates.
    /// </summary>
    public async Task JoinBadgeTroopGroup(int badgeId, int troopId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(badgeId, troopId));
    }

    /// <summary>
    /// Leave a badge-troop group when navigating away.
    /// </summary>
    public async Task LeaveBadgeTroopGroup(int badgeId, int troopId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(badgeId, troopId));
    }

    /// <summary>
    /// Broadcast a badge part toggle to all clients viewing the same badge-troop.
    /// </summary>
    public async Task BroadcastPartToggled(int badgeId, int troopId, int badgePartId, int personId, bool isDone, bool badgeCompleted, bool badgeUncompleted)
    {
        await SafeSendAsync(
            Clients.OthersInGroup(GetGroupName(badgeId, troopId)),
            "PartToggled", badgeId, badgePartId, personId, isDone, badgeCompleted, badgeUncompleted);
    }

    public static string GetGroupName(int badgeId, int troopId) => $"badge-{badgeId}-troop-{troopId}";

    private static async Task SafeSendAsync(IClientProxy? clientProxy, string method, params object?[] args)
    {
        if (clientProxy is null)
            return;

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
