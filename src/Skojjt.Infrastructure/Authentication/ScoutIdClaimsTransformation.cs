using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Data;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Transforms ScoutID claims from the OIDC token into application-specific claims.
/// This runs after authentication and adds custom claims based on ScoutID data.
/// When group information is not provided by ScoutID, it looks up the user's group
/// membership from the database based on their member number.
/// </summary>
public class ScoutIdClaimsTransformation : IClaimsTransformation
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<ScoutIdClaimsTransformation> _logger;
    private static readonly Regex s_regexGroup = new(@"group:(\d+):(.+)", RegexOptions.Compiled);
    private static readonly Regex s_regexTroop = new(@"troop:(\d+):(.+)", RegexOptions.Compiled);
    private static readonly ConcurrentDictionary<int, int> s_troopToGroupCache = new();

    public ScoutIdClaimsTransformation(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<ScoutIdClaimsTransformation> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
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
    };

    /// <summary>
    /// Removes claims that are redundant once the compact <c>scoutid/*</c> claims have been
    /// produced, so the persisted auth ticket stays small and request headers can't overflow
    /// (HTTP 431). Two categories are trimmed:
    /// <list type="number">
    ///   <item>Raw ScoutID role claims — any claim whose value starts with <c>group:</c>,
    ///   <c>troop:</c>, or <c>organisation:</c> (e.g. "troop:999:other_leader"). This also
    ///   removes the duplicate <see cref="ClaimTypes.Role"/> copies of those values, which are
    ///   the dominant cost for a user who leads many troops.</item>
    ///   <item>Unused OIDC protocol/profile claims (<see cref="s_trimmableClaimTypes"/>).</item>
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
        var nameIdentifier = identity.FindFirst(ClaimTypes.NameIdentifier);
        var nameClaim = identity.FindFirst("name");

        // Log what we found for debugging
        _logger.LogDebug("ExtractSignInAttributes - nameIdentifier: {NameId}, name: {Name}",
            nameIdentifier?.Value, nameClaim?.Value);

        // Simplified check - don't rely on Subject.IsAuthenticated which may be null 
        // when claims are deserialized from cookie
        if (nameIdentifier == null || nameClaim == null)
        {
            _logger.LogWarning("Could not extract ScoutID attributes from claims - missing required claims");
            return false;
        }
        var uid = nameIdentifier.Value;
        var name = nameClaim.Value;

        // For now I'm using scoutid admins as admins in skojjt.
        const string scoutIdAdmin = "organisation:692:scoutid_admin"; // TODO: move to appconfig
        bool isAdmin = (identity.FindFirst(claim => claim.Value == scoutIdAdmin) != null);
        _logger.LogDebug("Admin check: looking for '{AdminClaim}', found: {IsAdmin}", scoutIdAdmin, isAdmin);
        HashSet<string> accessibleGroups = new();
        HashSet<string> memberRegistrarGroups = new();
        HashSet<string> accessibleTroops = new();

        // Collect troop Scoutnet IDs that need group lookup
        List<(int TroopScoutnetId, string RoleName)> troopRoles = [];

        foreach (var role in identity.FindAll(claim => claim.Type.EndsWith("role") && claim.Value != null))
        {
            var groupMatch = s_regexGroup.Match(role.Value);
            if (groupMatch.Success)
            {
                // Extract group information from the role claim
                var groupId = groupMatch.Groups[1].Value;
                var roleName = groupMatch.Groups[2].Value;
                if (roleName is "leader" or "assistant_leader" or "member_registrar" or "other_leader")
                {
                    accessibleGroups.Add(groupId);
                }

                if (roleName == "member_registrar")
                {
                    memberRegistrarGroups.Add(groupId);
                }
                continue;
            }

            var troopMatch = s_regexTroop.Match(role.Value);
            if (troopMatch.Success && int.TryParse(troopMatch.Groups[1].Value, out var troopScoutnetId))
            {
                troopRoles.Add((troopScoutnetId, troopMatch.Groups[2].Value));
            }
        }

        // Resolve troop-to-group mappings for any troop-based role claims
        if (troopRoles.Count > 0)
        {
            await ResolveTroopGroupMappingsAsync(troopRoles, accessibleGroups, memberRegistrarGroups, accessibleTroops);
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
                var groupIdStr = scoutGroupId.ToString();
                if (roleName is "leader" or "assistant_leader" or "member_registrar" or "other_leader")
                {
                    accessibleGroups.Add(groupIdStr);
                    accessibleTroops.Add(troopScoutnetId.ToString());
                    _logger.LogDebug("Mapped troop {TroopScoutnetId} to group {GroupId} for role {Role}", troopScoutnetId, groupIdStr, roleName);
                }

                if (roleName == "member_registrar")
                {
                    memberRegistrarGroups.Add(groupIdStr);
                }
            }
            else
            {
                _logger.LogWarning("Could not resolve scout group for troop Scoutnet ID {TroopScoutnetId}", troopScoutnetId);
            }
        }
    }
}
