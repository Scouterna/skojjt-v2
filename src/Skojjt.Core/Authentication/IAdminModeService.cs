namespace Skojjt.Core.Authentication;

/// <summary>
/// Service that controls whether the current admin user is operating with
/// elevated admin powers (access to all groups). Admin mode is OFF by default,
/// meaning admins see the same view as regular users until they explicitly enable it.
/// This does not affect access to admin pages — only the group access bypass.
/// </summary>
public interface IAdminModeService
{
    /// <summary>
    /// Whether admin powers are currently active (all-group access bypass).
    /// </summary>
    bool IsAdminModeActive { get; }

    /// <summary>
    /// Toggles admin mode on or off.
    /// </summary>
    void SetAdminMode(bool active);
}
