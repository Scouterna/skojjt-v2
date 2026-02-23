using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Skojjt.Core.Authentication;
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
}
