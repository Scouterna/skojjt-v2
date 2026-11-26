using System.Text.Json;
using Skojjt.Infrastructure.Authentication;

namespace Skojjt.Web.Tests.Authentication;

[TestClass]
public class ScoutIdMembershipsParserTests
{
    /// <summary>
    /// Shape taken from scoutid-keycloak-provider config_support/id_token_example.json.
    /// </summary>
    private const string ExampleMemberships = """
        {
          "groups": {
            "999": {
              "name": "Scoutkåren Mälarscouterna",
              "is_primary": true,
              "roles": [
                {"id": 136, "key": "it_manager", "name": "IT Manager"},
                {"id": 9, "key": "member_registrar", "name": "Medlemsregistrerare"}
              ]
            },
            "000": { "name": "Föreningen för Frustunaby", "is_primary": false, "roles": [] }
          },
          "troops": {
            "12345": { "name": "Yggdrasil", "groupId": 999, "roles": [{"id": 4, "key": "vice_leader"}] },
            "12346": { "groupId": null, "roles": [{"id": 11, "key": "leader"}] }
          },
          "patrols": {
            "9001": { "name": "Vargarna", "groupId": 999, "roles": [{"id": 7, "key": "patrol_leader"}] }
          },
          "organisations": { "692": { "roles": [{"id": 68, "key": "scoutid_admin"}] } },
          "regions": {},
          "districts": {},
          "corps": {},
          "networks": {},
          "projects": {}
        }
        """;

    [TestMethod]
    public void Parse_ExampleMemberships_ReturnsRolesForEveryEntityType()
    {
        var result = ScoutIdMembershipsParser.Parse(ExampleMemberships);

        Assert.IsNull(result.Error);
        CollectionAssert.AreEquivalent(
            new[]
            {
                "group:999:it_manager",
                "group:999:member_registrar",
                "troop:12345:vice_leader",
                "troop:12346:leader",
                "patrol:9001:patrol_leader",
                "organisation:692:scoutid_admin",
            },
            result.Roles.Select(r => r.ToRoleString()).ToArray());
    }

    [TestMethod]
    public void Parse_TroopGroupId_IsReadWhenKnownAndNullOtherwise()
    {
        var result = ScoutIdMembershipsParser.Parse(ExampleMemberships);

        Assert.AreEqual(999, result.Roles.Single(r => r.EntityId == "12345").GroupId);
        Assert.IsNull(result.Roles.Single(r => r.EntityId == "12346").GroupId);
        Assert.IsNull(result.Roles.First(r => r.EntityType == "group").GroupId);
    }

    [TestMethod]
    public void Parse_TruncatedPayload_ReturnsGroupRolesAndError()
    {
        const string json = """
            {
              "groups": { "1137": { "name": "Kåren", "is_primary": true, "roles": [{"id": 2, "key": "leader"}] } },
              "error": "memberships_truncated: payload exceeded 2048 chars"
            }
            """;

        var result = ScoutIdMembershipsParser.Parse(json);

        Assert.AreEqual("memberships_truncated: payload exceeded 2048 chars", result.Error);
        Assert.AreEqual("group:1137:leader", result.Roles.Single().ToRoleString());
    }

    [TestMethod]
    public void Parse_MalformedEntries_AreSkipped()
    {
        const string json = """
            {
              "groups": {
                "1": "not an object",
                "2": { "roles": "not an array" },
                "3": { "roles": [ "not an object", {"id": 1}, {"id": 2, "key": ""}, {"id": 3, "key": "leader"} ] }
              },
              "troops": [],
              "unknown_type": { "5": { "roles": [{"id": 1, "key": "leader"}] } }
            }
            """;

        var result = ScoutIdMembershipsParser.Parse(json);

        Assert.AreEqual("group:3:leader", result.Roles.Single().ToRoleString());
    }

    [TestMethod]
    public void Parse_NonNumericEntityIds_AreSkipped()
    {
        const string json = """
            {
              "groups": {
                "12,34": { "roles": [{"id": 2, "key": "leader"}] },
                "abc": { "roles": [{"id": 2, "key": "leader"}] },
                "-1": { "roles": [{"id": 2, "key": "leader"}] },
                " 5": { "roles": [{"id": 2, "key": "leader"}] },
                "": { "roles": [{"id": 2, "key": "leader"}] },
                "1137": { "roles": [{"id": 2, "key": "leader"}] }
              },
              "troops": { "7,8": { "groupId": 1137, "roles": [{"id": 11, "key": "leader"}] } }
            }
            """;

        var result = ScoutIdMembershipsParser.Parse(json);

        Assert.AreEqual("group:1137:leader", result.Roles.Single().ToRoleString());
    }

    [TestMethod]
    public void Parse_NonObjectRoot_ReturnsErrorWithoutRoles()
    {
        var result = ScoutIdMembershipsParser.Parse("[]");

        Assert.IsEmpty(result.Roles);
        Assert.IsNotNull(result.Error);
    }

    [TestMethod]
    public void Parse_InvalidJson_Throws()
    {
        // JsonReaderException derives from JsonException, which is what callers catch.
        Assert.Throws<JsonException>(() => ScoutIdMembershipsParser.Parse("group:1:leader"));
    }

    [TestMethod]
    [DataRow("group:1137:leader", "group", "1137", "leader")]
    [DataRow("troop:999:other_leader", "troop", "999", "other_leader")]
    [DataRow("organisation:692:scoutid_admin", "organisation", "692", "scoutid_admin")]
    public void TryParseLegacyRole_ValidValue_Parses(string value, string type, string id, string key)
    {
        Assert.IsTrue(ScoutIdMembershipsParser.TryParseLegacyRole(value, out var role));
        Assert.AreEqual(new ScoutIdRoleAssignment(type, id, key), role);
    }

    [TestMethod]
    [DataRow("*:*:leader")]
    [DataRow("group:*:leader")]
    [DataRow("Admin")]
    [DataRow("MemberRegistrar")]
    public void TryParseLegacyRole_WildcardOrPlainValue_IsRejected(string value)
    {
        Assert.IsFalse(ScoutIdMembershipsParser.TryParseLegacyRole(value, out _));
    }
}
