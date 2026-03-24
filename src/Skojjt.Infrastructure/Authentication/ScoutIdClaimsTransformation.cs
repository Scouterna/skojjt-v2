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

        // Check if transformation has already been applied
        if (identity.HasClaim(c => c.Type == ScoutIdClaimTypes.ScoutnetUid))
        {
            return principal;
        }

        // Log all incoming claims for debugging
        _logger.LogDebug("Transforming claims for user. All claims:");
        foreach (var claim in identity.Claims)
        {
            _logger.LogDebug("  Claim: {Type} = {Value}", claim.Type, claim.Value);
        }

        // Try to extract ScoutID attributes from OIDC claims
        await ExtractSignInAttributesAsync(identity);

        return principal;
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

        // Resolve troop→group mappings for any troop-based role claims
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

        // Check if user is a system administrator from the database
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

                // Don't cache null for unfound troop IDs — they may appear
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
