using System.Text.Json;
using System.Text.RegularExpressions;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// A single Scoutnet role held by the user on one entity (group, troop, organisation, ...).
/// </summary>
/// <param name="EntityType">Singular entity type, e.g. "group", "troop", "organisation".</param>
/// <param name="EntityId">Scoutnet ID of the entity.</param>
/// <param name="RoleKey">Scoutnet role key, e.g. "leader", "member_registrar", "scoutid_admin".</param>
/// <param name="GroupId">
/// Scout group the entity belongs to, when ScoutID knows it (troops and patrols only).
/// Null when unknown, in which case troop roles are resolved against the database.
/// </param>
public sealed record ScoutIdRoleAssignment(string EntityType, string EntityId, string RoleKey, int? GroupId = null)
{
    /// <summary>
    /// The role in the flat "{type}:{id}:{key}" format used by the legacy SimpleSAML ScoutID
    /// (e.g. "organisation:692:scoutid_admin"), which is what <c>Skojjt:AdminScoutnetRole</c> is written in.
    /// </summary>
    public string ToRoleString() => $"{EntityType}:{EntityId}:{RoleKey}";
}

/// <summary>
/// Result of parsing the ScoutID <c>memberships</c> claim.
/// </summary>
/// <param name="Roles">All role assignments found, across every entity type.</param>
/// <param name="Error">
/// The provider's <c>error</c> field, if set. ScoutID sets it to "memberships_truncated: ..." when the
/// full payload exceeded Keycloak's attribute size limit and only <c>groups</c> was kept, so troop
/// and organisation roles are missing.
/// </param>
public sealed record ScoutIdMemberships(IReadOnlyList<ScoutIdRoleAssignment> Roles, string? Error);

/// <summary>
/// Parses role data from the two ScoutID generations into <see cref="ScoutIdRoleAssignment"/>s:
/// <list type="bullet">
///   <item>Keycloak ScoutID: the JSON <c>memberships</c> claim from the <c>scoutnet-memberships</c> scope.</item>
///   <item>Legacy SimpleSAML ScoutID: flat <c>role</c> strings like "group:1137:leader".</item>
/// </list>
/// </summary>
/// <remarks>
/// The <c>memberships</c> claim is an object keyed by plural entity type. Each type maps entity IDs to an
/// entry with a <c>roles</c> array of <c>{"id": int, "key": string, "name"?: string}</c>. Troop and patrol
/// entries also carry <c>groupId</c> (int or null). See config_support/id_token_example.json in
/// scoutid-keycloak-provider for a full example.
/// </remarks>
public static class ScoutIdMembershipsParser
{
    /// <summary>Claim type of the Keycloak ScoutID memberships claim.</summary>
    public const string ClaimType = "memberships";

    /// <summary>Plural JSON keys in the memberships claim, mapped to the singular legacy entity type.</summary>
    private static readonly (string JsonKey, string EntityType)[] s_entityTypes =
    [
        ("groups", "group"),
        ("troops", "troop"),
        ("patrols", "patrol"),
        ("organisations", "organisation"),
        ("regions", "region"),
        ("districts", "district"),
        ("corps", "corps"),
        ("networks", "network"),
        ("projects", "project"),
    ];

    private static readonly Regex s_legacyRole = new(@"^([a-z_]+):(\d+):(.+)$", RegexOptions.Compiled);

    /// <summary>
    /// Parses the JSON value of the <c>memberships</c> claim. Entries with an unexpected shape are skipped.
    /// </summary>
    /// <exception cref="JsonException">The value is not valid JSON.</exception>
    public static ScoutIdMemberships Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new ScoutIdMemberships([], $"unexpected memberships JSON kind: {root.ValueKind}");
        }

        var roles = new List<ScoutIdRoleAssignment>();
        foreach (var (jsonKey, entityType) in s_entityTypes)
        {
            if (!root.TryGetProperty(jsonKey, out var entities) || entities.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var entity in entities.EnumerateObject())
            {
                // Same rule as the legacy "\d+": IDs end up in comma-joined claims, so a key like
                // "12,34" must not be able to smuggle in extra IDs.
                if (!IsScoutnetId(entity.Name)
                    || entity.Value.ValueKind != JsonValueKind.Object
                    || !entity.Value.TryGetProperty("roles", out var roleArray)
                    || roleArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var groupId = ReadGroupId(entity.Value);
                foreach (var role in roleArray.EnumerateArray())
                {
                    if (role.ValueKind == JsonValueKind.Object
                        && role.TryGetProperty("key", out var key)
                        && key.ValueKind == JsonValueKind.String
                        && key.GetString() is { Length: > 0 } roleKey)
                    {
                        roles.Add(new ScoutIdRoleAssignment(entityType, entity.Name, roleKey, groupId));
                    }
                }
            }
        }

        string? error = root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String
            ? errorElement.GetString()
            : null;

        return new ScoutIdMemberships(roles, error);
    }

    /// <summary>
    /// Parses a legacy SimpleSAML role string such as "group:1137:leader" or "troop:999:other_leader".
    /// Wildcard values like "*:*:leader" are rejected.
    /// </summary>
    public static bool TryParseLegacyRole(string value, out ScoutIdRoleAssignment role)
    {
        var match = s_legacyRole.Match(value);
        if (!match.Success)
        {
            role = null!;
            return false;
        }

        role = new ScoutIdRoleAssignment(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
        return true;
    }

    private static bool IsScoutnetId(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);

    private static int? ReadGroupId(JsonElement entity)
    {
        if (!entity.TryGetProperty("groupId", out var groupId))
        {
            return null;
        }

        return groupId.ValueKind switch
        {
            JsonValueKind.Number when groupId.TryGetInt32(out var id) => id,
            JsonValueKind.String when int.TryParse(groupId.GetString(), out var id) => id,
            _ => null,
        };
    }
}
