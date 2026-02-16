using System.Collections.Concurrent;

namespace Skojjt.Web.Services;

/// <summary>
/// Singleton service for real-time badge progress synchronization between Blazor Server circuits.
/// Uses in-memory pub/sub pattern since all Blazor Server components run on the same server.
/// </summary>
public class BadgeStateService
{
    private readonly ConcurrentDictionary<string, HandlerRegistration> _handlers = new();

    /// <summary>
    /// Register a handler for badge part toggle events. Returns an ID for unregistration.
    /// Handler params: badgeId, badgePartId, personId, isDone, badgeCompleted, badgeUncompleted, sourceInstanceId
    /// </summary>
    public string RegisterPartToggledHandler(Func<int, int, int, bool, bool, bool, string, Task> handler)
    {
        var id = Guid.NewGuid().ToString();
        _handlers[id] = new HandlerRegistration(handler);
        return id;
    }

    public void UnregisterPartToggledHandler(string id) => _handlers.TryRemove(id, out _);

    /// <summary>
    /// Notify all subscribers about a badge part toggle.
    /// </summary>
    public Task NotifyPartToggledAsync(int badgeId, int badgePartId, int personId, bool isDone, bool badgeCompleted, bool badgeUncompleted, string sourceInstanceId)
    {
        foreach (var kvp in _handlers.ToArray())
        {
            if (kvp.Value.IsDisabled)
            {
                _handlers.TryRemove(kvp.Key, out _);
                continue;
            }
            _ = InvokeHandlerSafelyAsync(kvp.Key, kvp.Value,
                h => h(badgeId, badgePartId, personId, isDone, badgeCompleted, badgeUncompleted, sourceInstanceId));
        }
        return Task.CompletedTask;
    }

    private async Task InvokeHandlerSafelyAsync(string handlerId, HandlerRegistration registration, Func<Func<int, int, int, bool, bool, bool, string, Task>, Task> invoke)
    {
        try
        {
            await invoke(registration.Handler).ConfigureAwait(false);
        }
        catch (Exception)
        {
            registration.Disable();
            _handlers.TryRemove(handlerId, out _);
        }
    }

    private sealed class HandlerRegistration
    {
        private volatile bool _disabled;
        public Func<int, int, int, bool, bool, bool, string, Task> Handler { get; }
        public bool IsDisabled => _disabled;

        public HandlerRegistration(Func<int, int, int, bool, bool, bool, string, Task> handler) => Handler = handler;
        public void Disable() => _disabled = true;
    }
}
