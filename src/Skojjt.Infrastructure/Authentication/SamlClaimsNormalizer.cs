using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Normalizes SimpleSAML SAML 2.0 attributes into standard claim types so the
/// downstream <see cref="ScoutIdClaimsTransformation"/> works identically
/// regardless of whether the user authenticated via OIDC or SAML.
///
/// ScoutID SimpleSAML provides attributes as flat names (uid, email, displayName, role, etc.)
/// rather than OID URNs. The role attribute values use the format "group:{groupId}:{roleName}"
/// (e.g. "group:1137:leader", "organisation:692:scoutid_admin") which is the same format
/// used by the Keycloak-based OIDC ScoutID.
///
/// See ScoutnetAuth.php in the ScoutID source for the full attribute list.
/// </summary>
public static class SamlClaimsNormalizer
{
    /// <summary>
    /// Ensures the SAML identity contains the standard claims that
    /// <see cref="ScoutIdClaimsTransformation"/> looks for:
    ///   - <see cref="ClaimTypes.NameIdentifier"/> (from SAML 'uid')
    ///   - <see cref="ClaimTypes.Email"/> (from SAML 'email')
    ///   - "name" (from SAML 'displayName' or 'firstlast')
    ///   - <see cref="ClaimTypes.Role"/> (from SAML 'role' — one claim per role value)
    /// </summary>
    public static void Normalize(ClaimsIdentity identity, ILogger? logger = null)
    {
        // --- uid → NameIdentifier ---
        var uid = FindFirstValue(identity, "uid");
        if (!string.IsNullOrEmpty(uid))
        {
            EnsureClaim(identity, ClaimTypes.NameIdentifier, uid);
        }

        // --- email → ClaimTypes.Email ---
        var email = FindFirstValue(identity, "email");
        if (!string.IsNullOrEmpty(email))
        {
            EnsureClaim(identity, ClaimTypes.Email, email);
        }

        // --- displayName → "name" ---
        var displayName = FindFirstValue(identity, "displayName")
                          ?? FindFirstValue(identity, "firstlast");
        if (!string.IsNullOrEmpty(displayName))
        {
            EnsureClaim(identity, "name", displayName);
        }

        // --- role → ClaimTypes.Role (multi-valued) ---
        // ScoutID sends role values like "group:1137:leader", "organisation:692:scoutid_admin".
        // ScoutIdClaimsTransformation.ExtractSignInAttributes already matches on
        //   claim.Type.EndsWith("role") && claim.Value != null
        // so ClaimTypes.Role (http://schemas.microsoft.com/ws/2008/06/identity/claims/role) works.
        var roleValues = identity.FindAll("role")
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .ToList();

        foreach (var roleValue in roleValues)
        {
            if (!identity.HasClaim(ClaimTypes.Role, roleValue))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, roleValue));
            }
        }

        logger?.LogDebug(
            "SAML claims normalized: uid={Uid}, email={Email}, name={Name}, roles={RoleCount}",
            uid, email, displayName, roleValues.Count);
    }

    private static string? FindFirstValue(ClaimsIdentity identity, string type)
    {
        return identity.FindFirst(type)?.Value;
    }

    private static void EnsureClaim(ClaimsIdentity identity, string type, string value)
    {
        if (!identity.HasClaim(c => c.Type == type))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }
}
