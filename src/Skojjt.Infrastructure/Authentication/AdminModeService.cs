using Microsoft.AspNetCore.Http;
using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Scoped service that tracks whether the current admin user has enabled
/// elevated admin mode. The choice is persisted in a cookie so it survives
/// full page reloads and manual URL navigation (both of which create a new
/// HTTP request / Blazor circuit). The cookie is read on first access via
/// <see cref="IHttpContextAccessor"/>, which is available during the initial
/// server-side render - before any component's OnInitialized runs.
/// Defaults to OFF.
/// </summary>
public class AdminModeService : IAdminModeService
{
    /// <summary>
    /// Name of the cookie that persists the admin mode choice. Must match the
    /// name used by the server endpoint that writes it (AuthController.SetAdminMode).
    /// </summary>
    public const string CookieName = "skojjt_adminmode";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private bool _initialized;
    private bool _isAdminModeActive;

    public AdminModeService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAdminModeActive
    {
        get
        {
            EnsureInitializedFromCookie();
            return _isAdminModeActive;
        }
    }

    public event Action? StateChanged;

    public void SetAdminMode(bool active)
    {
        EnsureInitializedFromCookie();
        if (_isAdminModeActive == active) return;

        _isAdminModeActive = active;
        StateChanged?.Invoke();
    }

    private void EnsureInitializedFromCookie()
    {
        if (_initialized) return;
        _initialized = true;

        var cookie = _httpContextAccessor.HttpContext?.Request.Cookies[CookieName];
        _isAdminModeActive = cookie == "1";
    }
}
