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
    }
}
