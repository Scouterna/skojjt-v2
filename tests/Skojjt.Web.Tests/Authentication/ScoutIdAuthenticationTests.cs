using System.Security.Claims;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.AspNetCore.Http;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Authentication;

namespace Skojjt.Web.Tests.Authentication;

[TestClass]
public class ScoutIdAuthenticationTests
{
    [TestMethod]
    public void FakeScoutIdService_GetAvailableUsers_ReturnsDefaultUsers()
    {
        // Arrange
        var service = new FakeScoutIdService();

        // Act
        var users = service.GetAvailableUsers();

        // Assert
        Assert.IsNotNull(users);
        Assert.IsGreaterThanOrEqualTo(4, users.Count, "Should have at least 4 default test users");
        Assert.IsTrue(users.Any(u => u.IsMemberRegistrar), "Should have at least one member registrar");
    }

    [TestMethod]
    public void FakeScoutIdService_GetUserByEmail_ReturnsCorrectUser()
    {
        // Arrange
        var service = new FakeScoutIdService();

        // Act
        var user = service.GetUserByEmail("admin@test.scout.se");

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual("admin@test.scout.se", user.Email);
        Assert.IsTrue(user.IsMemberRegistrar);
    }

    [TestMethod]
    public void FakeScoutIdService_GetUserByUid_ReturnsCorrectUser()
    {
        // Arrange
        var service = new FakeScoutIdService();

        // Act
        var user = service.GetUserByUid("12345");

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual("12345", user.Uid);
    }

    [TestMethod]
    public void FakeScoutIdService_CreateClaimsForUser_ReturnsValidClaims()
    {
        // Arrange
        var service = new FakeScoutIdService();
        var user = service.GetUserByEmail("admin@test.scout.se")!;

        // Act
        var claims = service.CreateClaimsForUser(user);

        // Assert
        Assert.IsNotNull(claims);
        Assert.AreEqual(user.Email, claims.Email);
        Assert.AreEqual(user.Uid, claims.Uid);
        Assert.Contains(user.GroupId, claims.MemberRegistrarGroups);
        Assert.IsGreaterThan(0, claims.AccessibleGroupIds.Count);
    }

    [TestMethod]
    public void FakeScoutIdService_CreateCustomUser_CreatesValidUser()
    {
        // Arrange & Act
        var user = FakeScoutIdService.CreateCustomUser(
            uid: "99999",
            email: "custom@test.se",
            displayName: "Custom User",
            groupId: 2000,
            isMemberRegistrar: true,
            accessibleGroups: [2000, 2001]);

        // Assert
        Assert.AreEqual("99999", user.Uid);
        Assert.AreEqual("custom@test.se", user.Email);
        Assert.AreEqual(2000, user.GroupId);
        Assert.IsTrue(user.IsMemberRegistrar);
        Assert.HasCount(2, user.AccessibleGroupIds);
        Assert.IsTrue(user.GroupRoles.ContainsKey("2000"));
    }

    [TestMethod]
    public void CurrentUserService_GetUserFromPrincipal_WithValidClaims_ReturnsUser()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var service = new CurrentUserService(mockHttpContextAccessor.Object, new AdminModeService());

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Test User"),
            new(ClaimTypes.Email, "test@test.se"),
            new("sub", "12345"),
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
            new(ScoutIdClaimTypes.DisplayName, "Test User"),
            new(ScoutIdClaimTypes.AccessibleGroups, "1001,1002"),
            new(ScoutIdClaimTypes.MemberRegistrarGroups, "1001"),
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var user = service.GetUserFromPrincipal(principal);

        // Assert
        Assert.IsNotNull(user);
        Assert.AreEqual("12345", user.Uid);
        Assert.AreEqual("test@test.se", user.Email);
        Assert.AreEqual("Test User", user.DisplayName);
        Assert.IsTrue(user.IsMemberRegistrar(1001));
        Assert.HasCount(2, user.AccessibleGroupIds);
    }

    [TestMethod]
    public void CurrentUserService_GetUserFromPrincipal_WithUnauthenticated_ReturnsNull()
    {
        // Arrange
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var service = new CurrentUserService(mockHttpContextAccessor.Object, new AdminModeService());

        var identity = new ClaimsIdentity(); // Not authenticated
        var principal = new ClaimsPrincipal(identity);

        // Act
        var user = service.GetUserFromPrincipal(principal);

        // Assert
        Assert.IsNull(user);
    }

    [TestMethod]
    public void CurrentUserService_HasGroupAccess_WithAccess_ReturnsTrue()
    {
        // Arrange
        var service = CreateServiceWithUser(accessibleGroups: "1001,1002");

        // Act & Assert
        Assert.IsTrue(service.HasGroupAccess(1001));
        Assert.IsTrue(service.HasGroupAccess(1002));
        Assert.IsFalse(service.HasGroupAccess(9999), "Should NOT have access to groups not in AccessibleGroupIds");
    }

    [TestMethod]
    public void CurrentUserService_HasGroupAccess_WithoutAccess_ReturnsFalse()
    {
        // Arrange - user only has access to group 1001
        var service = CreateServiceWithUser(accessibleGroups: "1001");

        // Act & Assert
        Assert.IsTrue(service.HasGroupAccess(1001));
        Assert.IsFalse(service.HasGroupAccess(1002), "Should NOT have access to group 1002");
        Assert.IsFalse(service.HasGroupAccess(9999), "Should NOT have access to group 9999");
    }

    [TestMethod]
    public void CurrentUserService_IsMemberRegistrar_WithRole_ReturnsTrue()
    {
        // Arrange
        var service = CreateServiceWithUser(
            accessibleGroups: "1001",
            memberRegistrarGroups: "1001");

        // Act & Assert
        Assert.IsTrue(service.IsMemberRegistrar(1001));
    }

    [TestMethod]
    public void CurrentUserService_IsMemberRegistrar_WithoutGroupAccess_ReturnsFalse()
    {
        // Arrange - user has registrar role for 1002, but only access to 1001
        var service = CreateServiceWithUser(
            accessibleGroups: "1001",
            memberRegistrarGroups: "1002");

        // Act & Assert
        Assert.IsFalse(service.IsMemberRegistrar(1002), 
            "Should NOT be registrar for group without access, even with role claim");
    }

    [TestMethod]
    public void CurrentUserService_GetAccessibleGroupIds_ReturnsOnlyAccessibleGroups()
    {
        // Arrange
        var service = CreateServiceWithUser(accessibleGroups: "1001,1002,1003");

        // Act
        var accessibleGroups = service.GetAccessibleGroupIds();

        // Assert
        Assert.HasCount(3, accessibleGroups);
        CollectionAssert.AreEquivalent(new[] { 1001, 1002, 1003 }, accessibleGroups.ToList());
    }

    [TestMethod]
    public void CurrentUserService_RequireGroupAccess_WithAccess_DoesNotThrow()
    {
        // Arrange
        var service = CreateServiceWithUser(accessibleGroups: "1001");

        // Act & Assert - should not throw
        service.RequireGroupAccess(1001);
    }

    [TestMethod]
    public void CurrentUserService_RequireGroupAccess_WithoutAccess_ThrowsUnauthorized()
    {
        // Arrange
        var service = CreateServiceWithUser(accessibleGroups: "1001");

        // Act & Assert
        var ex = Assert.ThrowsExactly<UnauthorizedAccessException>(() => 
            service.RequireGroupAccess(9999));
        Assert.Contains("9999", ex.Message, "Exception should mention the group ID");
    }

    [TestMethod]
    public void CurrentUserService_IsMemberRegistrar_WithoutGroupAccess_ReturnsFalse_ForNonAccessibleGroup()
    {
        // Arrange - has registrar claim but no group access
        var service = CreateServiceWithUser(
            accessibleGroups: "1001",
            memberRegistrarGroups: "9999");

        // Act & Assert
        Assert.IsFalse(service.IsMemberRegistrar(9999),
            "Should NOT be registrar for group without access");
    }

    [TestMethod]
    public void CurrentUserService_IsAnyMemberRegistrar_WithRegistrarRole_ReturnsTrue()
    {
        // Arrange
        var service = CreateServiceWithUser(
            accessibleGroups: "1001",
            memberRegistrarGroups: "1001");

        // Act & Assert
        Assert.IsTrue(service.IsAnyMemberRegistrar);
    }

    [TestMethod]
    public void CurrentUserService_IsAnyMemberRegistrar_WithoutRegistrarRole_ReturnsFalse()
    {
        // Arrange - user has group access but no registrar role
        var service = CreateServiceWithUser(
            accessibleGroups: "1001");

        // Act & Assert
        Assert.IsFalse(service.IsAnyMemberRegistrar,
            "Should NOT be any member registrar without registrar role claims");
    }

    private static CurrentUserService CreateServiceWithUser(
        string accessibleGroups,
        string? memberRegistrarGroups = null)
    {
        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        var mockHttpContext = new Mock<HttpContext>();
        
        var claims = new List<Claim>
        {
            new("sub", "12345"),
            new(ClaimTypes.Email, "test@test.se"),
            new(ScoutIdClaimTypes.ScoutnetUid, "12345"),
            new(ScoutIdClaimTypes.DisplayName, "Test User"),
            new(ScoutIdClaimTypes.AccessibleGroups, accessibleGroups),
            new(ScoutIdClaimTypes.MemberRegistrarGroups, memberRegistrarGroups ?? ""),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        mockHttpContext.Setup(c => c.User).Returns(principal);
        mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(mockHttpContext.Object);

        return new CurrentUserService(mockHttpContextAccessor.Object, new AdminModeService());
    }
}
