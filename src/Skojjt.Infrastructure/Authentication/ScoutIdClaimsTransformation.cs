using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Data;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Transforms ScoutID claims into the compact application claims (<c>scoutid/*</c>).
/// Roles are read from either ScoutID generation:
/// <list type="bullet">
///   <item>Keycloak ScoutID (OIDC): the JSON <c>memberships</c> claim (scope <c>scoutnet-memberships</c>).</item>
///   <item>Legacy SimpleSAML ScoutID (SAML): flat <c>role</c> claims like "group:1137:leader".</item>
/// </list>
/// Troop roles whose scout group is not given by ScoutID are resolved via the database.
/// </summary>
public class ScoutIdClaimsTransformation : IClaimsTransformation
{
    /// <summary>
    /// Keycloak ScoutID claim holding the Scoutnet member number. Preferred over <c>sub</c>, which is a
    /// Keycloak UUID unless the client has a "sub member_no" mapper.
    /// </summary>
    public const string ScoutnetMemberNoClaimType = "scoutnet_member_no";

    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<ScoutIdClaimsTransformation> _logger;
    private readonly HashSet<string> _adminUids;
    private readonly string _adminScoutnetRole;
    private const string DefaultAdminScoutnetRole = "organisation:692:scoutid_admin";
    private static readonly ConcurrentDictionary<int, int> s_troopToGroupCache = new();

    /// <summary>
    /// ScoutID role keys that grant access to Skojjt. Mirrors the Scoutnet unit roles
    /// 2 = Avdelningsledare (leader), 3 = Ledare (other_leader), 4 = Vice avdelningsledare
    /// (vice_leader), 5 = Assisterande ledare (assistant_leader), plus member_registrar.
    /// </summary>
    private static readonly FrozenSet<string> s_accessGrantingRoles = new[]
    {
        "leader",
        "assistant_leader",
        "vice_leader",
        "other_leader",
        "member_registrar",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public ScoutIdClaimsTransformation(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<ScoutIdClaimsTransformation> logger,
        IConfiguration? configuration = null)
    {
        _contextFactory = contextFactory;
        _logger = logger;
        _adminUids = ParseAdminUids(configuration?["Skojjt:AdminUids"]);
        var configuredAdminRole = configuration?["Skojjt:AdminScoutnetRole"];
        _adminScoutnetRole = string.IsNullOrWhiteSpace(configuredAdminRole)
            ? DefaultAdminScoutnetRole
            : configuredAdminRole;
    }

    private static HashSet<string> ParseAdminUids(string? value)
    {
        var uids = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
        {
            return uids;
        }

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            uids.Add(part);
        }

        return uids;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        // Transform claims if not already done (dev auth pre-populates ScoutID claims)
        if (!identity.HasClaim(c => c.Type == ScoutIdClaimTypes.ScoutnetUid))
        {
            // Log all incoming claims for debugging
            _logger.LogDebug("Transforming claims for user. All claims:");
            foreach (var claim in identity.Claims)
            {
                _logger.LogDebug("  Claim: {Type} = {Value}", claim.Type, claim.Value);
            }

            await ExtractSignInAttributesAsync(identity);
        }

        return principal;
    }

    /// <summary>
    /// OIDC protocol and profile claims that ScoutID (and <c>GetClaimsFromUserInfoEndpoint</c>)
    /// add to the token but that Skojjt never reads. They are pure ticket bloat once the compact
    /// <c>scoutid/*</c> claims exist, so they are dropped to keep the persisted ticket small.
    /// Deliberately excludes <c>name</c> and <see cref="ClaimTypes.Name"/> (surface
    /// <see cref="System.Security.Principal.IIdentity.Name"/>, used in logging/status),
    /// <see cref="ClaimTypes.NameIdentifier"/>, <see cref="ClaimTypes.Email"/>, and
    /// <see cref="ClaimTypes.Role"/> (drives authorization policies) — all of which are still read.
    /// </summary>
    private static readonly HashSet<string> s_trimmableClaimTypes = new(StringComparer.Ordinal)
    {
        // OIDC protocol claims (token/validation metadata)
        "iss", "aud", "exp", "iat", "nbf", "auth_time", "nonce", "azp",
        "at_hash", "c_hash", "s_hash", "sid", "jti", "acr", "amr",
        // Profile/scope claims we do not use (display name comes from scoutid/display_name)
        "given_name", "family_name", "middle_name", "nickname", "preferred_username",
        "profile", "picture", "website", "gender", "birthdate", "zoneinfo",
        "locale", "updated_at", "email_verified", "phone_number", "phone_number_verified",
        "address",
        // Keycloak ScoutID claims, condensed into scoutid/* or unused. memberships alone
        // can be up to 2 KB of JSON.
        ScoutIdMembershipsParser.ClaimType, ScoutnetMemberNoClaimType, "group_emails_json",
        "primary_group_name", "primary_group_no", "scouterna_email", "alt_email",
    };

    /// <summary>
    /// Removes claims that are redundant once the compact <c>scoutid/*</c> claims have been
    /// produced, so the persisted auth ticket stays small and request headers can't overflow
    /// (HTTP 431). Two categories are trimmed:
    /// <list type="number">
    ///   <item>Raw legacy ScoutID role claims — any claim whose value starts with <c>group:</c>,
    ///   <c>troop:</c>, or <c>organisation:</c> (e.g. "troop:999:other_leader"). This also
    ///   removes the duplicate <see cref="ClaimTypes.Role"/> copies of those values, which are
    ///   the dominant cost for a user who leads many troops.</item>
    ///   <item>Unused OIDC protocol/profile claims and the raw Keycloak ScoutID claims such as
    ///   <c>memberships</c> (<see cref="s_trimmableClaimTypes"/>).</item>
    /// </list>
    /// The synthesized <see cref="ClaimTypes.Role"/> values ("Admin", "MemberRegistrar"), the
    /// identity claims (uid, email, name), and the compact <c>scoutid/*</c> claims are preserved
    /// because they are read at runtime. Only call this after condensing has run and the compact
    /// claims are present; on later requests <see cref="TransformAsync"/> is a no-op once
    /// <see cref="ScoutIdClaimTypes.ScoutnetUid"/> exists, so the trimmed raw claims are not needed.
    /// </summary>
    public static void TrimRedundantRoleClaims(ClaimsIdentity identity)
    {
        var redundant = identity.Claims
            .Where(c => c.Value.StartsWith("group:", StringComparison.Ordinal)
                     || c.Value.StartsWith("troop:", StringComparison.Ordinal)
                     || c.Value.StartsWith("organisation:", StringComparison.Ordinal)
                     || s_trimmableClaimTypes.Contains(c.Type))
            .ToList();

        foreach (var claim in redundant)
        {
            identity.TryRemoveClaim(claim);
        }
    }

    private async Task<bool> ExtractSignInAttributesAsync(ClaimsIdentity identity)
    {
        // Keycloak ScoutID: scoutnet_member_no is the stable Scoutnet ID; sub is a Keycloak UUID
        // unless the client maps sub to the member number. Legacy SAML / dev: NameIdentifier is the uid.
        var memberNo = identity.FindFirst(ScoutnetMemberNoClaimType)?.Value;
        var nameIdentifier = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var uid = !string.IsNullOrWhiteSpace(memberNo) ? memberNo : nameIdentifier;
        var name = identity.FindFirst("name")?.Value;

        _logger.LogDebug("ExtractSignInAttributes - scoutnet_member_no: {MemberNo}, nameIdentifier: {NameId}, name: {Name}",
            memberNo, nameIdentifier, name);

        // Simplified check - don't rely on Subject.IsAuthenticated which may be null
        // when claims are deserialized from cookie
        if (string.IsNullOrEmpty(uid) || name == null)
        {
            _logger.LogWarning("Could not extract ScoutID attributes from claims - missing required claims");
            return false;
        }

        var roles = CollectRoles(identity);

        // Match parsed roles only, never arbitrary claim values: profile claims such as given_name
        // are user-editable in Scoutnet and must not be able to grant admin.
        bool isAdmin = roles.Any(r => string.Equals(r.ToRoleString(), _adminScoutnetRole, StringComparison.Ordinal));
        _logger.LogDebug("Admin check: looking for '{AdminClaim}', found: {IsAdmin}", _adminScoutnetRole, isAdmin);

        // Also grant admin to users whose Scoutnet UID is configured in Skojjt:AdminUids.
        if (!isAdmin && _adminUids.Contains(uid))
        {
            isAdmin = true;
            _logger.LogDebug("Admin check: uid '{Uid}' matched configured AdminUids", uid);
        }
        HashSet<string> accessibleGroups = new();
        HashSet<string> memberRegistrarGroups = new();
        HashSet<string> accessibleTroops = new();

        // Troop roles whose scout group ScoutID did not provide; resolved via the database.
        List<(int TroopScoutnetId, string RoleName)> unresolvedTroopRoles = [];

        foreach (var role in roles)
        {
            switch (role.EntityType)
            {
                case "group":
                    ApplyGroupRole(role.EntityId, role.RoleKey, accessibleGroups, memberRegistrarGroups);
                    break;

                case "troop" when int.TryParse(role.EntityId, out var troopScoutnetId):
                    if (role.GroupId is int groupId)
                    {
                        ApplyTroopRole(troopScoutnetId, groupId, role.RoleKey, accessibleGroups, memberRegistrarGroups, accessibleTroops);
                    }
                    else
                    {
                        unresolvedTroopRoles.Add((troopScoutnetId, role.RoleKey));
                    }
                    break;
            }
        }

        // Resolve troop-to-group mappings for troop roles without a known group
        if (unresolvedTroopRoles.Count > 0)
        {
            await ResolveTroopGroupMappingsAsync(unresolvedTroopRoles, accessibleGroups, memberRegistrarGroups, accessibleTroops);
        }

        // Add basic claims
        identity.AddClaim(new Claim(ScoutIdClaimTypes.ScoutnetUid, uid));
        identity.AddClaim(new Claim(ScoutIdClaimTypes.DisplayName, name));

        // Add role-based claims
        identity.AddClaim(new Claim(ScoutIdClaimTypes.MemberRegistrarGroups,
            string.Join(",", memberRegistrarGroups)));
        identity.AddClaim(new Claim(ScoutIdClaimTypes.AccessibleGroups,
            string.Join(",", accessibleGroups)));
        identity.AddClaim(new Claim(ScoutIdClaimTypes.AccessibleTroops,
            string.Join(",", accessibleTroops)));

        // Check if user is a system administrator
        identity.AddClaim(new Claim(ScoutIdClaimTypes.Admin, isAdmin ? "true" : "false"));
        if (isAdmin)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        }
        return true;
    }

    /// <summary>
    /// Resolves troop Scoutnet IDs to scout group IDs by looking up the database,
    /// using a static cache to avoid repeated queries.
    /// </summary>
    private async Task ResolveTroopGroupMappingsAsync(
        List<(int TroopScoutnetId, string RoleName)> troopRoles,
        HashSet<string> accessibleGroups,
        HashSet<string> memberRegistrarGroups,
        HashSet<string> accessibleTroops)
    {
        // Find which troop IDs are not yet cached
        var uncachedTroopIds = troopRoles
            .Select(t => t.TroopScoutnetId)
            .Distinct()
            .Where(id => !s_troopToGroupCache.ContainsKey(id))
            .ToList();

        // Batch-load uncached mappings from the database
        if (uncachedTroopIds.Count > 0)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                var mappings = await context.Troops
                    .Where(t => uncachedTroopIds.Contains(t.ScoutnetId))
                    .Select(t => new { t.ScoutnetId, t.ScoutGroupId })
                    .Distinct()
                    .ToListAsync();

                foreach (var mapping in mappings)
                {
                    s_troopToGroupCache.TryAdd(mapping.ScoutnetId, mapping.ScoutGroupId);
                }

                // Don't cache null for unfound troop IDs - they may appear
                // after a new scout group is imported from Scoutnet.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to look up troop-to-group mappings from database");
                return;
            }
        }

        // Apply the cached mappings to build accessible groups
        foreach (var (troopScoutnetId, roleName) in troopRoles)
        {
            if (s_troopToGroupCache.TryGetValue(troopScoutnetId, out var scoutGroupId))
            {
                ApplyTroopRole(troopScoutnetId, scoutGroupId, roleName, accessibleGroups, memberRegistrarGroups, accessibleTroops);
            }
            else
            {
                _logger.LogWarning("Could not resolve scout group for troop Scoutnet ID {TroopScoutnetId}", troopScoutnetId);
            }
        }
    }

    /// <summary>
    /// Gathers the user's Scoutnet roles from the Keycloak <c>memberships</c> claim and from
    /// legacy SAML <c>role</c> claims. Normally only one source is present.
    /// </summary>
    private List<ScoutIdRoleAssignment> CollectRoles(ClaimsIdentity identity)
    {
        var roles = new List<ScoutIdRoleAssignment>();

        foreach (var claim in identity.FindAll(ScoutIdMembershipsParser.ClaimType))
        {
            try
            {
                var memberships = ScoutIdMembershipsParser.Parse(claim.Value);
                if (memberships.Error != null)
                {
                    // e.g. "memberships_truncated": only group roles were kept, troop roles are missing.
                    _logger.LogWarning("ScoutID memberships claim reports an error: {Error}", memberships.Error);
                }
                roles.AddRange(memberships.Roles);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Could not parse ScoutID memberships claim as JSON");
            }
        }

        foreach (var claim in identity.FindAll(c => c.Type.EndsWith("role", StringComparison.Ordinal)))
        {
            if (ScoutIdMembershipsParser.TryParseLegacyRole(claim.Value, out var role))
            {
                roles.Add(role);
            }
        }

        _logger.LogDebug("ScoutID roles: {Roles}", string.Join(", ", roles.Select(r => r.ToRoleString())));
        return roles;
    }

    private static void ApplyGroupRole(
        string groupId,
        string roleName,
        HashSet<string> accessibleGroups,
        HashSet<string> memberRegistrarGroups)
    {
        if (s_accessGrantingRoles.Contains(roleName))
        {
            accessibleGroups.Add(groupId);
        }

        if (roleName == "member_registrar")
        {
            memberRegistrarGroups.Add(groupId);
        }
    }

    private void ApplyTroopRole(
        int troopScoutnetId,
        int scoutGroupId,
        string roleName,
        HashSet<string> accessibleGroups,
        HashSet<string> memberRegistrarGroups,
        HashSet<string> accessibleTroops)
    {
        ApplyGroupRole(scoutGroupId.ToString(), roleName, accessibleGroups, memberRegistrarGroups);

        if (s_accessGrantingRoles.Contains(roleName))
        {
            accessibleTroops.Add(troopScoutnetId.ToString());
            _logger.LogDebug("Mapped troop {TroopScoutnetId} to group {GroupId} for role {Role}", troopScoutnetId, scoutGroupId, roleName);
        }
    }
}
