using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Data;
using System.Security.Claims;
using System.Text.Json;
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
		ExtractSignInAttributes(identity);

        return principal;
    }

    private bool ExtractSignInAttributes(ClaimsIdentity identity)
    {
        var nameIdentifier = identity.FindFirst(ClaimTypes.NameIdentifier);
		var emailClaim = identity.FindFirst(ClaimTypes.Email);
		var nameClaim = identity.FindFirst("name");
		
		// Log what we found for debugging
		_logger.LogDebug("ExtractSignInAttributes - nameIdentifier: {NameId}, email: {Email}, name: {Name}",
			nameIdentifier?.Value, emailClaim?.Value, nameClaim?.Value);
		
		// Simplified check - don't rely on Subject.IsAuthenticated which may be null 
		// when claims are deserialized from cookie
		if (nameIdentifier == null || emailClaim == null || nameClaim == null)
		{
			_logger.LogWarning("Could not extract ScoutID attributes from claims - missing required claims");
			return false;
		}
		var uid = nameIdentifier.Value;
		var email = emailClaim.Value;
		var name = nameClaim.Value;

		const string scoutIdAdmin = "organisation:692:scoutid_admin"; // TODO: move to appconfig
		bool isAdmin = (identity.FindFirst(claim => claim.Value == scoutIdAdmin) != null);
		_logger.LogDebug("Admin check: looking for '{AdminClaim}', found: {IsAdmin}", scoutIdAdmin, isAdmin);
		var regexGroup = new Regex(@"group:(\d+):(.+)", RegexOptions.Compiled);
		HashSet<string> accessibleGroups = new();
		HashSet<string> memberRegistrarGroups = new();
		foreach (var role in identity.FindAll(claim => claim.Type.EndsWith("role") && claim.Value != null))
		{
			var match = regexGroup.Match(role.Value);
			if (match.Success)
			{
				// Extract group information from the role claim
				var groupId = match.Groups[1].Value;
				var roleName = match.Groups[2].Value;
				if (roleName == "leader" || 
					roleName == "assistant_leader" || 
					roleName == "member_registrar")
				{
					accessibleGroups.Add(groupId);
				} 
				else if (roleName == "member_registrar")
				{
					memberRegistrarGroups.Add(groupId);
				}
			}
		}

		// Add basic claims
		identity.AddClaim(new Claim(ScoutIdClaimTypes.ScoutnetUid, uid));
		identity.AddClaim(new Claim(ScoutIdClaimTypes.DisplayName, name));
		//identity.AddClaim(new Claim(ScoutIdClaimTypes.GroupNo, groupNo));
		//identity.AddClaim(new Claim(ScoutIdClaimTypes.GroupId, groupId.ToString()));

		// Add role-based claims
		identity.AddClaim(new Claim(ScoutIdClaimTypes.MemberRegistrarGroups,
			string.Join(",", memberRegistrarGroups)));
		identity.AddClaim(new Claim(ScoutIdClaimTypes.AccessibleGroups,
			string.Join(",", accessibleGroups)));
		//identity.AddClaim(new Claim(ScoutIdClaimTypes.GroupRoles,
		//	JsonSerializer.Serialize(groupRoles)));

		// Add role claims for authorization policies
		//if (memberRegistrarGroups.Contains(groupId))
		//{
		//	identity.AddClaim(new Claim(ClaimTypes.Role, "MemberRegistrar"));
		//}

		// Check if user is a system administrator from the database
		identity.AddClaim(new Claim(ScoutIdClaimTypes.Admin, isAdmin ? "true" : "false"));
		if (isAdmin)
		{
			identity.AddClaim(new Claim(ClaimTypes.Role, "Admin"));
		}
		return true;
	}

    /// <summary>
    /// Looks up the scout groups that a person belongs to from the database.
    /// Uses the ScoutGroupPerson table which links persons to scout groups.
    /// </summary>
    private async Task<List<int>> LookupGroupsFromDatabaseAsync(int memberNo)
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            
            var groups = await context.ScoutGroupPersons
                .Where(sgp => sgp.PersonId == memberNo && !sgp.NotInScoutnet)
                .Select(sgp => sgp.ScoutGroupId)
                .Distinct()
                .ToListAsync();

            return groups;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to look up groups from database for member {MemberNo}", memberNo);
            return [];
        }
    }

    private void ProcessRoles(
        JsonElement rolesElement, 
        int primaryGroupId,
        List<int> memberRegistrarGroups,
        List<int> accessibleGroups,
        Dictionary<string, List<string>> groupRoles)
    {
        _logger.LogDebug("Processing roles element: {Roles}", rolesElement.GetRawText());

        if (rolesElement.TryGetProperty("group", out var groupRolesElement))
        {
            foreach (var groupEntry in groupRolesElement.EnumerateObject())
            {
                if (!int.TryParse(groupEntry.Name, out var entryGroupId))
                    continue;

                accessibleGroups.Add(entryGroupId);
                var roles = new List<string>();

                if (groupEntry.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var role in groupEntry.Value.EnumerateArray())
                    {
                        var roleStr = role.ToString();
                        roles.Add(roleStr);

                        if (roleStr == ScoutIdRoles.MemberRegistrar)
                        {
                            memberRegistrarGroups.Add(entryGroupId);
                        }
                    }
                }

                groupRoles[groupEntry.Name] = roles;
            }
        }
    }

    private static string GetStringValue(Dictionary<string, object> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value))
            return "";

        return value switch
        {
            string s => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "",
            JsonElement { ValueKind: JsonValueKind.Number } je => je.GetRawText(),
            _ => value?.ToString() ?? ""
        };
    }
}
