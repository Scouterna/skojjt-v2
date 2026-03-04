using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Authentication;
using System.Security.Claims;

namespace Skojjt.Web.Controllers;

/// <summary>
/// Controller for authentication endpoints (login, logout, challenge).
/// Supports both OIDC (Keycloak) and SAML 2.0 (SimpleSAML) ScoutID authentication.
/// </summary>
[Route("auth")]
public class AuthController : Controller
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;

    public AuthController(
        ILogger<AuthController> logger, 
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiates the authentication challenge.
    /// Redirects to ScoutID for login via OIDC or SAML depending on configuration.
    /// </summary>
    [HttpGet("challenge")]
    public IActionResult Challenge([FromQuery] string? returnUrl)
    {
        // Validate return URL to prevent open redirect
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        var useSaml = _configuration.GetValue<bool>("ScoutIdSaml:Enabled");

        // If neither OIDC nor SAML is configured, redirect to unified login page
        if (!useSaml && string.IsNullOrEmpty(_configuration["ScoutId:ClientId"]))
        {
            _logger.LogWarning("ScoutID not configured, redirecting to login page");
            return Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl,
            IsPersistent = true
        };

        // Challenge with whichever scheme is configured
        var scheme = useSaml
            ? SamlAuthenticationExtensions.Saml2Scheme
            : OpenIdConnectDefaults.AuthenticationScheme;

        _logger.LogInformation("Initiating authentication challenge via {Scheme}", scheme);
        return Challenge(properties, scheme);
    }

    /// <summary>
    /// Logs out the user from both the application and ScoutID.
    /// Supports OIDC federated logout and SAML single logout.
    /// </summary>
    [HttpGet("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl)
    {
        // Validate return URL
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/";
        }

        var useSaml = _configuration.GetValue<bool>("ScoutIdSaml:Enabled");

        // If neither OIDC nor SAML is configured, just sign out of cookies
        if (!useSaml && string.IsNullOrEmpty(_configuration["ScoutId:ClientId"]))
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect(returnUrl);
        }

        var userName = User.Identity?.Name ?? "unknown";
        _logger.LogInformation("User {UserName} logging out", userName);

        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        if (useSaml)
        {
            // Sign out of both cookie and SAML
            return SignOut(properties,
                CookieAuthenticationDefaults.AuthenticationScheme,
                SamlAuthenticationExtensions.Saml2Scheme);
        }

        // Sign out of both cookie and OIDC
        return SignOut(properties, 
            CookieAuthenticationDefaults.AuthenticationScheme, 
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Simple logout that only clears the local session (no OIDC logout).
    /// Useful for testing or when federated logout isn't desired.
    /// </summary>
    [HttpGet("signout")]
    public async Task<IActionResult> SignOut([FromQuery] string? returnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            returnUrl = "/login";
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return LocalRedirect(returnUrl);
    }


    /// <summary>
    /// Returns the current user's authentication status.
    /// </summary>
    [HttpGet("status")]
    public IActionResult Status()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Ok(new
            {
                IsAuthenticated = true,
                UserName = User.Identity.Name,
                Claims = User.Claims.Select(c => new { c.Type, c.Value })
            });
        }

        return Ok(new { IsAuthenticated = false });
    }

    /// <summary>
    /// Returns detailed claims information for debugging.
    /// Shows raw claims, extracted ScoutID info, and accessible groups.
    /// </summary>
    [HttpGet("claims")]
    [Authorize]
    public IActionResult Claims()
    {
        var rawClaims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
        
        // Extract ScoutID-specific claims
        var scoutIdClaims = new Dictionary<string, string?>();
        foreach (var claimType in new[] { 
            ScoutIdClaimTypes.ScoutnetUid,
            ScoutIdClaimTypes.DisplayName,
            ScoutIdClaimTypes.AccessibleGroups,
            ScoutIdClaimTypes.MemberRegistrarGroups,
            ScoutIdClaimTypes.Admin
        })
        {
            scoutIdClaims[claimType] = User.FindFirst(claimType)?.Value;
        }

        // Parse accessible groups
        var accessibleGroupsStr = User.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value ?? "";
        var accessibleGroups = accessibleGroupsStr
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToList();

        return Ok(new
        {
            UserName = User.Identity?.Name,
            IsAuthenticated = User.Identity?.IsAuthenticated,
            RawClaimCount = rawClaims.Count,
            RawClaims = rawClaims,
            ScoutIdClaims = scoutIdClaims,
            AccessibleGroups = accessibleGroups,
            IsAdmin = User.IsInRole("Admin"),
            IsMemberRegistrar = User.IsInRole("MemberRegistrar")
        });
    }
}
