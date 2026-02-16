namespace Skojjt.Web.Services;

/// <summary>
/// Scoped service for broadcasting badge progress changes to other Blazor circuits.
/// Uses the singleton BadgeStateService for in-memory pub/sub.
/// </summary>
public class BadgeNotificationService
{
    private readonly BadgeStateService _stateService;

    public BadgeNotificationService(BadgeStateService stateService)
    {
        _stateService = stateService;
    }

    /// <summary>
    /// Notify all other components about a badge part toggle.
    /// </summary>
    public Task NotifyPartToggledAsync(int badgeId, int badgePartId, int personId, bool isDone, bool badgeCompleted, bool badgeUncompleted, string sourceInstanceId)
        => _stateService.NotifyPartToggledAsync(badgeId, badgePartId, personId, isDone, badgeCompleted, badgeUncompleted, sourceInstanceId);
}
