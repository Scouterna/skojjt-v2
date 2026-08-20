using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Scoped service that tracks whether the current admin user has enabled
/// elevated admin mode for this session. Defaults to OFF.
/// </summary>
public class AdminModeService : IAdminModeService
{
    public bool IsAdminModeActive { get; private set; }

    public event Action? StateChanged;

    public void SetAdminMode(bool active)
    {
        if (IsAdminModeActive == active) return;

        IsAdminModeActive = active;
        StateChanged?.Invoke();
    }
}
