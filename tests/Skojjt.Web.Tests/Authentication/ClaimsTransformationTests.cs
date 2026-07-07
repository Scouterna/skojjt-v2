using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
