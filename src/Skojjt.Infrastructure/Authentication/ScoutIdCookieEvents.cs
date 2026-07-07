using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Cookie authentication events shared by every ScoutID sign-in path (dev, SAML, OIDC).
///
/// Running at <c>OnSigningIn</c> — before the ticket is written to the cookie / server-side
/// ticket store — lets us (1) condense the many raw ScoutID role claims into the compact
/// <c>scoutid/*</c> claims and (2) drop the now-redundant raw claims. This keeps the persisted
/// ticket small, complementing the server-side <see cref="DistributedCacheTicketStore"/>, and
/// eliminates the oversized auth cookies that caused HTTP 431.
/// </summary>
public static class ScoutIdCookieEvents
{
    /// <summary>
    /// Attaches the ScoutID sign-in handling to a cookie scheme's events.
    /// </summary>
    public static void Configure(CookieAuthenticationOptions options)
    {
        options.Events ??= new CookieAuthenticationEvents();
        options.Events.OnSigningIn = OnSigningInAsync;
    }

    private static async Task OnSigningInAsync(CookieSigningInContext context)
    {
        var principal = context.Principal;
        if (principal is null || principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var services = context.HttpContext.RequestServices;

        // Condense the raw ScoutID role claims into compact claims BEFORE the ticket is
        // persisted. Dev auth already emits the compact claims, so this is a no-op there.
        if (!identity.HasClaim(c => c.Type == ScoutIdClaimTypes.ScoutnetUid))
        {
            var transformation = services.GetService<IClaimsTransformation>();
            if (transformation is not null)
            {
                try
                {
                    var transformed = await transformation.TransformAsync(principal);
                    context.Principal = transformed;
                    identity = transformed.Identity as ClaimsIdentity ?? identity;
                }
                catch (Exception ex)
                {
                    services.GetService<ILoggerFactory>()?
                        .CreateLogger(typeof(ScoutIdCookieEvents))
                        .LogWarning(ex, "Failed to condense ScoutID claims during sign-in; keeping raw claims for per-request transformation.");

                    // Do not trim: without the compact claims, removing the raw ones would
                    // strip the user's access. The per-request transformation will retry.
                    return;
                }
            }
        }

        // Only trim once the compact claims exist; otherwise keep the raw claims so the
        // per-request ScoutIdClaimsTransformation can still derive access on later requests.
        if (identity.HasClaim(c => c.Type == ScoutIdClaimTypes.ScoutnetUid))
        {
            ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);
        }
    }
}
