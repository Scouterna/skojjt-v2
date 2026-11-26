using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Skojjt.Core.Authentication;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Authentication;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Web.Tests.Authentication;

[TestClass]
public class ClaimsTransformationTests
{
    private static ScoutIdClaimsTransformation CreateTransformation()
    {
        // Create a mock DbContextFactory that returns a context with no data
        var mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();

        // For these tests, we don't need actual database lookups
        // The transformation will fail gracefully when lookup fails
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database not available in test"));

        return new ScoutIdClaimsTransformation(mockFactory.Object, NullLogger<ScoutIdClaimsTransformation>.Instance);
    }

    private static (ScoutIdClaimsTransformation Transformation, DbContextOptions<SkojjtDbContext> Options) CreateTransformationWithDb()
    {
        var options = new DbContextOptionsBuilder<SkojjtDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var mockFactory = new Mock<IDbContextFactory<SkojjtDbContext>>();
        mockFactory.Setup(f => f.CreateDbContext()).Returns(() => new SkojjtDbContext(options));
        mockFactory.Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new SkojjtDbContext(options));

        var transformation = new ScoutIdClaimsTransformation(mockFactory.Object, NullLogger<ScoutIdClaimsTransformation>.Instance);
        return (transformation, options);
    }

    [TestMethod]
    public async Task TransformAsync_WithUnauthenticated_ReturnsUnchanged()
    {
        // Arrange
        var transformation = CreateTransformation();
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        Assert.IsFalse(result.Identity?.IsAuthenticated ?? false);
    }

    [TestMethod]
    public async Task TransformAsync_WithExistingScoutIdClaims_DoesNotDuplicate()
    {
        // Arrange
        var transformation = CreateTransformation();
        var claims = new List<Claim>
        {
            new("sub", "12345"),
            new(ScoutIdClaimTypes.ScoutnetUid, "12345") // Already transformed
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var uidClaims = ((ClaimsIdentity)result.Identity!).FindAll(ScoutIdClaimTypes.ScoutnetUid).ToList();
        Assert.HasCount(1, uidClaims, "Should not duplicate ScoutnetUid claim");
    }

    [TestMethod]
    public async Task TransformAsync_WithBasicClaims_AddsScoutIdClaims()
    {
        // Arrange
        var transformation = CreateTransformation();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "12345"),
            new(ClaimTypes.Email, "test@test.se"),
            new("name", "Test User"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var resultIdentity = (ClaimsIdentity)result.Identity!;
        Assert.IsNotNull(resultIdentity.FindFirst(ScoutIdClaimTypes.ScoutnetUid));
        Assert.AreEqual("12345", resultIdentity.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
    }

    [TestMethod]
    public async Task TransformAsync_WithTroopRoleClaim_ResolvesScoutGroupFromDatabase()
    {
        // Arrange
        var (transformation, options) = CreateTransformationWithDb();

        // Seed a troop with ScoutnetId=999 belonging to ScoutGroupId=42
        await using (var context = new SkojjtDbContext(options))
        {
            context.Troops.Add(new Troop
            {
                ScoutnetId = 999,
                ScoutGroupId = 42,
                SemesterId = 20251,
                Name = "Test Troop"
            });
            await context.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "67890"),
            new(ClaimTypes.Email, "leader@test.se"),
            new("name", "Test Leader"),
            new("role", "troop:999:other_leader"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var resultIdentity = (ClaimsIdentity)result.Identity!;
        var accessibleGroups = resultIdentity.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value;
        Assert.IsNotNull(accessibleGroups);
        Assert.Contains(accessibleGroups, "42", $"Expected accessible groups to contain '42', but was '{accessibleGroups}'");

        var accessibleTroops = resultIdentity.FindFirst(ScoutIdClaimTypes.AccessibleTroops)?.Value;
        Assert.IsNotNull(accessibleTroops);
        Assert.Contains(accessibleTroops, "999", $"Expected accessible troops to contain '999', but was '{accessibleTroops}'");
    }

    [TestMethod]
    public async Task TransformAsync_WithViceLeaderTroopRoleClaim_GrantsTroopAccess()
    {
        // Arrange
        var (transformation, options) = CreateTransformationWithDb();

        await using (var context = new SkojjtDbContext(options))
        {
            context.Troops.Add(new Troop
            {
                ScoutnetId = 18008,
                ScoutGroupId = 42,
                SemesterId = 20251,
                Name = "Test Troop"
            });
            await context.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "67891"),
            new(ClaimTypes.Email, "vice@test.se"),
            new("name", "Test Vice Leader"),
            new("role", "troop:18008:vice_leader"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = await transformation.TransformAsync(principal);

        // Assert
        var resultIdentity = (ClaimsIdentity)result.Identity!;
        var accessibleGroups = resultIdentity.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value;
        Assert.IsNotNull(accessibleGroups);
        Assert.Contains(accessibleGroups, "42", $"Expected accessible groups to contain '42', but was '{accessibleGroups}'");

        var accessibleTroops = resultIdentity.FindFirst(ScoutIdClaimTypes.AccessibleTroops)?.Value;
        Assert.IsNotNull(accessibleTroops);
        Assert.Contains(accessibleTroops, "18008", $"Expected accessible troops to contain '18008', but was '{accessibleTroops}'");
    }

    /// <summary>
    /// ID token payload in the Keycloak ScoutID format (scoutid-keycloak-provider
    /// config_support/id_token_example.json). sub is a Keycloak UUID, as it is by default.
    /// Troop 70002 has no groupId and must be resolved from the database; 70003 is unknown.
    /// </summary>
    private const string KeycloakIdTokenPayload = """
        {
          "iss": "http://localhost:8080/realms/master",
          "aud": "skojjt-v2",
          "sub": "abc12345-abc1-abc1-abc1-abc123456789",
          "typ": "ID",
          "preferred_username": "9876543@scoutnet",
          "name": "Teo Leader",
          "given_name": "Teo",
          "family_name": "Leader",
          "email": "teo.leader@example.com",
          "scoutnet_member_no": "9876543",
          "primary_group_name": "Kåren",
          "primary_group_no": "4242",
          "group_emails_json": { "4242": "teo.leader@example.com" },
          "memberships": {
            "groups": {
              "4242": { "name": "Kåren", "is_primary": true, "roles": [{"id": 9, "key": "member_registrar", "name": "Medlemsregistrerare"}] },
              "5151": { "name": "Annan kår", "is_primary": false, "roles": [{"id": 136, "key": "it_manager", "name": "IT Manager"}] }
            },
            "troops": {
              "70001": { "name": "Yggdrasil", "groupId": 4242, "roles": [{"id": 4, "key": "vice_leader"}] },
              "70002": { "groupId": null, "roles": [{"id": 11, "key": "leader"}] },
              "70003": { "groupId": null, "roles": [{"id": 11, "key": "leader"}] }
            },
            "patrols": {},
            "organisations": { "692": { "roles": [{"id": 68, "key": "scoutid_admin"}] } },
            "regions": {}, "districts": {}, "corps": {}, "networks": {}
          }
        }
        """;

    /// <summary>
    /// Validates a token the same way the OIDC handler does (JsonWebTokenHandler with
    /// MapInboundClaims = true, the OpenIdConnectOptions default), so the test sees the exact
    /// claim types and value formats that reach the claims transformation in production.
    /// </summary>
    private static async Task<ClaimsIdentity> ValidateIdTokenAsync(string payload)
    {
        var key = new SymmetricSecurityKey(new byte[32]);
        var handler = new JsonWebTokenHandler { MapInboundClaims = true };
        var jwt = handler.CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        var result = await handler.ValidateTokenAsync(jwt, new TokenValidationParameters
        {
            IssuerSigningKey = key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role,
        });
        Assert.IsTrue(result.IsValid, result.Exception?.Message);
        return result.ClaimsIdentity;
    }

    private static HashSet<string> CommaSet(ClaimsIdentity identity, string claimType) =>
        (identity.FindFirst(claimType)?.Value ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

    [TestMethod]
    public async Task TransformAsync_WithKeycloakMembershipsClaim_DerivesAccessFromJson()
    {
        // Arrange
        var (transformation, options) = CreateTransformationWithDb();
        await using (var context = new SkojjtDbContext(options))
        {
            context.Troops.Add(new Troop { ScoutnetId = 70002, ScoutGroupId = 6262, SemesterId = 20251, Name = "DB Troop" });
            await context.SaveChangesAsync();
        }

        var identity = await ValidateIdTokenAsync(KeycloakIdTokenPayload);
        Assert.AreEqual(JsonClaimValueTypes.Json, identity.FindFirst("memberships")?.ValueType,
            "Precondition: the token handler should surface the memberships object as one JSON claim.");

        // Act
        var result = (ClaimsIdentity)(await transformation.TransformAsync(new ClaimsPrincipal(identity))).Identity!;

        // Assert
        Assert.AreEqual("9876543", result.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value,
            "uid should be scoutnet_member_no, not the Keycloak UUID in sub.");
        Assert.AreEqual("Teo Leader", result.FindFirst(ScoutIdClaimTypes.DisplayName)?.Value);

        var groups = CommaSet(result, ScoutIdClaimTypes.AccessibleGroups);
        Assert.IsTrue(groups.SetEquals(["4242", "6262"]), $"Accessible groups were '{string.Join(",", groups)}'");

        var registrarGroups = CommaSet(result, ScoutIdClaimTypes.MemberRegistrarGroups);
        Assert.IsTrue(registrarGroups.SetEquals(["4242"]), $"Member registrar groups were '{string.Join(",", registrarGroups)}'");

        var troops = CommaSet(result, ScoutIdClaimTypes.AccessibleTroops);
        Assert.IsTrue(troops.SetEquals(["70001", "70002"]), $"Accessible troops were '{string.Join(",", troops)}'");

        Assert.AreEqual("true", result.FindFirst(ScoutIdClaimTypes.Admin)?.Value);
        Assert.IsTrue(result.HasClaim(ClaimTypes.Role, "Admin"));
    }

    [TestMethod]
    public async Task TransformAsync_WithKeycloakTokenWithoutMemberNo_FallsBackToSub()
    {
        // Arrange: a client with the "sub member_no" mapper but no profile scope.
        var transformation = CreateTransformation();
        const string payload = """
            {
              "sub": "9876543",
              "name": "Teo Leader",
              "email": "teo.leader@example.com",
              "memberships": { "groups": { "4242": { "is_primary": true, "roles": [{"id": 2, "key": "leader"}] } } }
            }
            """;
        var identity = await ValidateIdTokenAsync(payload);

        // Act
        var result = (ClaimsIdentity)(await transformation.TransformAsync(new ClaimsPrincipal(identity))).Identity!;

        // Assert
        Assert.AreEqual("9876543", result.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
        Assert.AreEqual("4242", result.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value);
        Assert.AreEqual("false", result.FindFirst(ScoutIdClaimTypes.Admin)?.Value);
    }

    [TestMethod]
    public async Task TransformAsync_WithInvalidMembershipsJson_StillSignsInWithoutRoles()
    {
        // Arrange
        var transformation = CreateTransformation();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "9876543"),
            new(ClaimTypes.Email, "teo.leader@example.com"),
            new("name", "Teo Leader"),
            new("memberships", "{not json", JsonClaimValueTypes.Json),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = (ClaimsIdentity)(await transformation.TransformAsync(principal)).Identity!;

        // Assert
        Assert.AreEqual("9876543", result.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
        Assert.AreEqual("", result.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value);
    }

    [TestMethod]
    public async Task TransformAsync_WithAdminRoleInProfileClaim_DoesNotGrantAdmin()
    {
        // Arrange: profile fields are user-editable in Scoutnet and must not count as roles.
        var transformation = CreateTransformation();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "9876543"),
            new("name", "Teo Leader"),
            new(ClaimTypes.GivenName, "organisation:692:scoutid_admin"),
            new("alt_email", "organisation:692:scoutid_admin"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        // Act
        var result = (ClaimsIdentity)(await transformation.TransformAsync(principal)).Identity!;

        // Assert
        Assert.AreEqual("false", result.FindFirst(ScoutIdClaimTypes.Admin)?.Value);
        Assert.IsFalse(result.HasClaim(ClaimTypes.Role, "Admin"));
    }

    [TestMethod]
    public async Task TrimRedundantRoleClaims_AfterKeycloakTransform_RemovesRawScoutIdClaims()
    {
        // Arrange
        var transformation = CreateTransformation();
        var identity = await ValidateIdTokenAsync(KeycloakIdTokenPayload);
        var transformed = (ClaimsIdentity)(await transformation.TransformAsync(new ClaimsPrincipal(identity))).Identity!;

        // Act
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(transformed);

        // Assert: bulky raw claims are gone, the claims the app reads remain.
        foreach (var type in new[] { "memberships", "group_emails_json", "scoutnet_member_no", "primary_group_no", "primary_group_name", "preferred_username" })
        {
            Assert.IsNull(transformed.FindFirst(type), $"Claim '{type}' should have been trimmed.");
        }
        Assert.AreEqual("9876543", transformed.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
        Assert.AreEqual("teo.leader@example.com", transformed.FindFirst(ClaimTypes.Email)?.Value);
        Assert.AreEqual("Teo Leader", transformed.FindFirst("name")?.Value);
        Assert.IsTrue(transformed.HasClaim(ClaimTypes.Role, "Admin"));
    }

    [TestMethod]
    public void TrimRedundantRoleClaims_RemovesRawGroupTroopAndOrganisationRoleClaims()
    {
        // Arrange: raw ScoutID role values, both as "role" and as the duplicate ClaimTypes.Role.
        var claims = new List<Claim>
        {
            new("role", "group:1137:leader"),
            new("role", "troop:999:other_leader"),
            new("role", "organisation:692:scoutid_admin"),
            new(ClaimTypes.Role, "group:1137:leader"),
            new(ClaimTypes.Role, "troop:999:other_leader"),
            // Compact claim present, mirroring the post-condensation state.
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");

        // Act
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);

        // Assert: no claim value retains a raw prefix.
        Assert.IsFalse(
            identity.Claims.Any(c => c.Value.StartsWith("group:", StringComparison.Ordinal)
                                  || c.Value.StartsWith("troop:", StringComparison.Ordinal)
                                  || c.Value.StartsWith("organisation:", StringComparison.Ordinal)),
            "All raw group:/troop:/organisation: role claims should be removed.");
    }

    [TestMethod]
    public void TrimRedundantRoleClaims_RemovesUnusedOidcProtocolAndProfileClaims()
    {
        // Arrange: protocol/profile claims that OIDC adds but Skojjt never reads.
        string[] trimmableTypes =
        [
            "iss", "aud", "exp", "iat", "nbf", "auth_time", "nonce", "azp",
            "at_hash", "c_hash", "s_hash", "sid", "jti", "acr", "amr",
            "given_name", "family_name", "preferred_username", "email_verified",
            "locale", "updated_at", "picture",
        ];

        var claims = trimmableTypes.Select(t => new Claim(t, "value")).ToList();
        claims.Add(new Claim(ScoutIdClaimTypes.ScoutnetUid, "12345"));
        var identity = new ClaimsIdentity(claims, "TestAuth");

        // Act
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);

        // Assert
        foreach (var type in trimmableTypes)
        {
            Assert.IsNull(identity.FindFirst(type), $"Claim '{type}' should have been trimmed.");
        }
    }

    [TestMethod]
    public void TrimRedundantRoleClaims_KeepsClaimsReadAtRuntime()
    {
        // Arrange: every claim the application reads after sign-in must survive trimming.
        var claims = new List<Claim>
        {
            // Identity claims
            new(ClaimTypes.NameIdentifier, "12345"),
            new(ClaimTypes.Email, "leader@test.se"),
            new("name", "Test Leader"),                 // OIDC NameClaimType => User.Identity.Name
            new(ClaimTypes.Name, "Test Leader"),         // cookie/dev NameClaimType => User.Identity.Name
            // Synthesized authorization roles (drive policies) - not raw prefixes
            new(ClaimTypes.Role, "Admin"),
            new(ClaimTypes.Role, "MemberRegistrar"),
            // Compact scoutid/* claims consumed by CurrentUserService / AuthController
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
            new(ScoutIdClaimTypes.DisplayName, "Test Leader"),
            new(ScoutIdClaimTypes.AccessibleGroups, "42,1137"),
            new(ScoutIdClaimTypes.MemberRegistrarGroups, "42"),
            new(ScoutIdClaimTypes.AccessibleTroops, "999"),
            new(ScoutIdClaimTypes.Admin, "true"),
            // A raw role claim that SHOULD be removed, to prove trimming still runs.
            new("role", "troop:999:other_leader"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");

        // Act
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);

        // Assert: kept claims
        Assert.AreEqual("12345", identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.AreEqual("leader@test.se", identity.FindFirst(ClaimTypes.Email)?.Value);
        Assert.AreEqual("Test Leader", identity.FindFirst("name")?.Value);
        Assert.AreEqual("Test Leader", identity.FindFirst(ClaimTypes.Name)?.Value);
        Assert.IsTrue(identity.HasClaim(ClaimTypes.Role, "Admin"));
        Assert.IsTrue(identity.HasClaim(ClaimTypes.Role, "MemberRegistrar"));
        Assert.AreEqual("12345", identity.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
        Assert.AreEqual("Test Leader", identity.FindFirst(ScoutIdClaimTypes.DisplayName)?.Value);
        Assert.AreEqual("42,1137", identity.FindFirst(ScoutIdClaimTypes.AccessibleGroups)?.Value);
        Assert.AreEqual("42", identity.FindFirst(ScoutIdClaimTypes.MemberRegistrarGroups)?.Value);
        Assert.AreEqual("999", identity.FindFirst(ScoutIdClaimTypes.AccessibleTroops)?.Value);
        Assert.AreEqual("true", identity.FindFirst(ScoutIdClaimTypes.Admin)?.Value);

        // Assert: the raw role claim was still trimmed.
        Assert.IsFalse(
            identity.Claims.Any(c => c.Value.StartsWith("troop:", StringComparison.Ordinal)),
            "Raw troop: role claim should be removed even when runtime claims are present.");
    }

    [TestMethod]
    public void TrimRedundantRoleClaims_WithNothingToTrim_LeavesClaimsUnchanged()
    {
        // Arrange: only claims the app reads; nothing should be removed.
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, "leader@test.se"),
            new("name", "Test Leader"),
            new(ClaimTypes.Role, "MemberRegistrar"),
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
            new(ScoutIdClaimTypes.AccessibleGroups, "42"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var countBefore = identity.Claims.Count();

        // Act
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);

        // Assert
        Assert.AreEqual(countBefore, identity.Claims.Count(), "No claims should be removed when there is nothing to trim.");
    }

    [TestMethod]
    public void TrimRedundantRoleClaims_IsIdempotent()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("role", "troop:999:other_leader"),
            new("iss", "https://scoutid.example"),
            new(ClaimTypes.Email, "leader@test.se"),
            new("name", "Test Leader"),
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");

        // Act: trimming twice must produce the same result as trimming once.
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);
        var countAfterFirst = identity.Claims.Count();
        ScoutIdClaimsTransformation.TrimRedundantRoleClaims(identity);
        var countAfterSecond = identity.Claims.Count();

        // Assert
        Assert.AreEqual(countAfterFirst, countAfterSecond, "A second trim should not remove any additional claims.");
        Assert.AreEqual("12345", identity.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value);
        Assert.AreEqual("leader@test.se", identity.FindFirst(ClaimTypes.Email)?.Value);
        Assert.AreEqual("Test Leader", identity.FindFirst("name")?.Value);
    }
}
