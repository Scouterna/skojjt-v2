using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Defensive server-side guard against stale, oversized authentication cookies.
///
/// After moving to a server-side ticket store (<see cref="DistributedCacheTicketStore"/>),
/// newly issued auth cookies are tiny. However, users who signed in <em>before</em> that change
/// still carry the old, large chunked cookie (<c>.AspNetCore.Cookies</c>, <c>.AspNetCore.CookiesC1</c>,
/// …). Every request re-sends those bytes, and once the total exceeds the server's request-header
/// limit the user gets a hard <c>HTTP 431 (Request Header Fields Too Large)</c> and can no longer
/// reach any page — including the sign-out page that would clear the cookie. That leaves the user
/// stuck until they manually clear site data.
///
/// This middleware runs at the very front of the pipeline. For requests that still get through, it
/// measures the combined size of the inbound cookie-auth cookies. When they exceed a threshold set
/// comfortably below the header limit, it proactively deletes them (base cookie plus every chunk)
/// and redirects idempotent GET navigations to the sign-out endpoint, so the browser stops sending
/// the oversized cookie <em>before</em> it grows past the hard limit. The result is self-healing:
/// affected users recover on their next navigation without manual intervention.
/// </summary>
public sealed class StaleAuthCookieGuardMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StaleAuthCookieGuardMiddleware> _logger;
    private readonly StaleAuthCookieGuardOptions _options;

    public StaleAuthCookieGuardMiddleware(
        RequestDelegate next,
        StaleAuthCookieGuardOptions options,
        ILogger<StaleAuthCookieGuardMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldInspect(context) && TryMeasureAuthCookieBytes(context, out var totalBytes, out var cookieNames)
            && totalBytes > _options.MaxAuthCookieBytes)
        {
            _logger.LogWarning(
                "Stale oversized auth cookie detected ({TotalBytes} bytes across {CookieCount} cookie(s)); " +
                "clearing and redirecting to {SignOutPath} to self-heal.",
                totalBytes, cookieNames.Count, _options.SignOutPath);

            DeleteAuthCookies(context, cookieNames);

            // Only redirect safe, idempotent navigations. A redirect on a POST/PUT would drop the
            // request body (and could break a SAML callback), so we simply clear the cookies and
            // let the request continue; the browser will send the reduced cookie set next time.
            if (HttpMethods.IsGet(context.Request.Method))
            {
                context.Response.Redirect(_options.SignOutPath);
                return;
            }
        }

        await _next(context);
    }

    private bool ShouldInspect(HttpContext context)
    {
        var path = context.Request.Path;

        // Never interfere with the sign-out endpoint itself (would loop), the SAML callback
        // (a redirect there drops the POSTed SAML response), or health checks.
        if (path.StartsWithSegments(_options.SignOutPath, StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Saml2", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/healthz", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Static assets never rely on the auth cookie; skip the cookie scan for them.
        foreach (var prefix in _options.IgnoredPathPrefixes)
        {
            if (path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Sums the byte size (name + value) of every inbound cookie that belongs to the cookie-auth
    /// scheme: the base cookie and its numbered chunk cookies (<c>{name}C1</c>, <c>{name}C2</c>, …).
    /// </summary>
    private bool TryMeasureAuthCookieBytes(HttpContext context, out int totalBytes, out List<string> cookieNames)
    {
        totalBytes = 0;
        cookieNames = [];

        foreach (var (name, value) in context.Request.Cookies)
        {
            if (IsAuthCookie(name))
            {
                cookieNames.Add(name);
                // Header size is what matters for HTTP 431, so count the raw name and value bytes.
                totalBytes += System.Text.Encoding.UTF8.GetByteCount(name)
                            + System.Text.Encoding.UTF8.GetByteCount(value ?? string.Empty);
            }
        }

        return cookieNames.Count > 0;
    }

    private bool IsAuthCookie(string name)
    {
        var baseName = _options.CookieName;
        if (string.Equals(name, baseName, StringComparison.Ordinal))
        {
            return true;
        }

        // Chunked cookies are named "{baseName}C{n}" (n = 1, 2, 3, …).
        if (name.Length > baseName.Length + 1
            && name.StartsWith(baseName, StringComparison.Ordinal)
            && name[baseName.Length] == 'C')
        {
            var suffix = name.AsSpan(baseName.Length + 1);
            return suffix.Length > 0 && int.TryParse(suffix, out _);
        }

        return false;
    }

    /// <summary>
    /// Expires the auth cookie and all of its chunks. Delete options must match the attributes the
    /// cookie was written with (Path=/, Secure, SameSite) or the browser keeps the original cookie.
    /// </summary>
    private void DeleteAuthCookies(HttpContext context, IReadOnlyCollection<string> cookieNames)
    {
        var deleteOptions = new CookieOptions
        {
            Path = "/",
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
        };

        foreach (var name in cookieNames)
        {
            context.Response.Cookies.Delete(name, deleteOptions);
        }
    }
}

/// <summary>
/// Options controlling <see cref="StaleAuthCookieGuardMiddleware"/>.
/// </summary>
public sealed class StaleAuthCookieGuardOptions
{
    /// <summary>
    /// Base name of the cookie-auth cookie. Defaults to ASP.NET Core's default
    /// (<see cref="CookieAuthenticationDefaults.CookiePrefix"/> + the scheme name), which is what
    /// the app uses because no custom <c>Cookie.Name</c> is configured.
    /// </summary>
    public string CookieName { get; set; } =
        CookieAuthenticationDefaults.CookiePrefix + CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// Combined byte size (name + value across all chunks) above which the auth cookie is treated as
    /// stale/oversized and cleared. Set well below the Kestrel request-header limit (64 KB) so the
    /// guard intervenes before a subsequent request can trigger HTTP 431. Default: 32 KB.
    /// </summary>
    public int MaxAuthCookieBytes { get; set; } = 32 * 1024;

    /// <summary>
    /// Path the guard redirects to after clearing the cookie. Must be a clean sign-out endpoint that
    /// issues a fresh (reduced) cookie / no cookie. Default: <c>/auth/signout</c>.
    /// </summary>
    public string SignOutPath { get; set; } = "/auth/signout";

    /// <summary>
    /// Request path prefixes that are skipped entirely (static assets, framework endpoints).
    /// </summary>
    public string[] IgnoredPathPrefixes { get; set; } =
    [
        "/_framework",
        "/_content",
        "/css",
        "/js",
        "/lib",
        "/images",
        "/favicon",
    ];
}

/// <summary>
/// Extension methods for registering <see cref="StaleAuthCookieGuardMiddleware"/>.
/// </summary>
public static class StaleAuthCookieGuardExtensions
{
    /// <summary>
    /// Adds the stale-oversized-auth-cookie guard. Register this as early as possible in the
    /// pipeline (before authentication and before any redirect middleware) so it inspects the raw
    /// inbound cookies and can clear them before they trigger HTTP 431.
    /// </summary>
    public static IApplicationBuilder UseStaleAuthCookieGuard(
        this IApplicationBuilder app,
        Action<StaleAuthCookieGuardOptions>? configure = null)
    {
        var options = new StaleAuthCookieGuardOptions();
        configure?.Invoke(options);
        return app.UseMiddleware<StaleAuthCookieGuardMiddleware>(options);
    }
}
