using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Infrastructure.Authentication;
using Skojjt.Web.Controllers;

namespace Skojjt.Web.Tests.Controllers;

[TestClass]
public class AuthControllerAdminModeTests
{
    private static AuthController CreateController(HttpContext httpContext)
    {
        var configuration = new ConfigurationBuilder().Build();
        var controller = new AuthController(NullLogger<AuthController>.Instance, configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
        };
        return controller;
    }

    private static DefaultHttpContext CreateHttpContext(bool isAdmin, bool isHttps = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "Test User"),
        };
        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity),
        };
        httpContext.Request.Scheme = isHttps ? "https" : "http";
        return httpContext;
    }

    private static string GetSetCookieHeader(HttpContext httpContext)
    {
        return httpContext.Response.Headers.SetCookie.ToString();
    }

    [TestMethod]
    public void SetAdminMode_WhenAdminEnables_SetsCookie()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAdmin: true);
        var controller = CreateController(httpContext);

        // Act
        var result = controller.SetAdminMode(active: true);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        var setCookie = GetSetCookieHeader(httpContext);
        StringAssert.Contains(setCookie, $"{AdminModeService.CookieName}=1");
        StringAssert.Contains(setCookie, "httponly");
        StringAssert.Contains(setCookie, "secure");
        StringAssert.Contains(setCookie, "samesite=lax");
    }

    [TestMethod]
    public void SetAdminMode_WhenAdminDisables_ClearsCookie()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAdmin: true);
        var controller = CreateController(httpContext);

        // Act
        var result = controller.SetAdminMode(active: false);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        var setCookie = GetSetCookieHeader(httpContext);
        // Deleting a cookie emits it with an expired date / empty value.
        StringAssert.Contains(setCookie, $"{AdminModeService.CookieName}=");
        StringAssert.Contains(setCookie, "expires=");
    }

    [TestMethod]
    public void SetAdminMode_WhenNonAdminEnables_ReturnsForbidAndSetsNoCookie()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAdmin: false);
        var controller = CreateController(httpContext);

        // Act
        var result = controller.SetAdminMode(active: true);

        // Assert
        Assert.IsInstanceOfType(result, typeof(ForbidResult));

        var setCookie = GetSetCookieHeader(httpContext);
        Assert.IsFalse(
            setCookie.Contains(AdminModeService.CookieName, StringComparison.OrdinalIgnoreCase),
            "No admin-mode cookie should be set for a non-admin user.");
    }

    [TestMethod]
    public void SetAdminMode_WhenNonAdminDisables_IsAllowed()
    {
        // Arrange: disabling admin mode should always be permitted, even without the role.
        var httpContext = CreateHttpContext(isAdmin: false);
        var controller = CreateController(httpContext);

        // Act
        var result = controller.SetAdminMode(active: false);

        // Assert
        Assert.IsInstanceOfType(result, typeof(OkObjectResult));

        var setCookie = GetSetCookieHeader(httpContext);
        StringAssert.Contains(setCookie, $"{AdminModeService.CookieName}=");
    }

    [TestMethod]
    public void SetAdminMode_OverHttp_DoesNotSetSecureFlag()
    {
        // Arrange
        var httpContext = CreateHttpContext(isAdmin: true, isHttps: false);
        var controller = CreateController(httpContext);

        // Act
        controller.SetAdminMode(active: true);

        // Assert
        var setCookie = GetSetCookieHeader(httpContext);
        StringAssert.Contains(setCookie, $"{AdminModeService.CookieName}=1");
        Assert.IsFalse(
            setCookie.Contains("secure", StringComparison.OrdinalIgnoreCase),
            "Secure flag should not be set over plain HTTP.");
    }
}
