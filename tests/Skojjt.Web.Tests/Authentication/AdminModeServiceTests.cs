using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Skojjt.Infrastructure.Authentication;

namespace Skojjt.Web.Tests.Authentication;

[TestClass]
public class AdminModeServiceTests
{
    private static AdminModeService CreateService(string? cookieValue)
    {
        var httpContext = new DefaultHttpContext();
        if (cookieValue != null)
        {
            httpContext.Request.Headers.Cookie = $"{AdminModeService.CookieName}={cookieValue}";
        }

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);

        return new AdminModeService(accessor.Object);
    }

    [TestMethod]
    public void IsAdminModeActive_WhenNoCookie_IsFalse()
    {
        var service = CreateService(cookieValue: null);

        Assert.IsFalse(service.IsAdminModeActive);
    }

    [TestMethod]
    public void IsAdminModeActive_WhenCookieIsOne_IsTrue()
    {
        var service = CreateService(cookieValue: "1");

        Assert.IsTrue(service.IsAdminModeActive);
    }

    [TestMethod]
    public void IsAdminModeActive_WhenCookieHasOtherValue_IsFalse()
    {
        var service = CreateService(cookieValue: "0");

        Assert.IsFalse(service.IsAdminModeActive);
    }

    [TestMethod]
    public void SetAdminMode_ChangingValue_RaisesStateChanged()
    {
        var service = CreateService(cookieValue: null);
        var raised = 0;
        service.StateChanged += () => raised++;

        service.SetAdminMode(true);

        Assert.IsTrue(service.IsAdminModeActive);
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void SetAdminMode_SameValue_DoesNotRaiseStateChanged()
    {
        var service = CreateService(cookieValue: "1");
        var raised = 0;
        service.StateChanged += () => raised++;

        // Cookie already made it active; setting active again should be a no-op.
        service.SetAdminMode(true);

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void SetAdminMode_WhenHttpContextNull_DefaultsToInactiveAndCanEnable()
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var service = new AdminModeService(accessor.Object);

        Assert.IsFalse(service.IsAdminModeActive);

        service.SetAdminMode(true);

        Assert.IsTrue(service.IsAdminModeActive);
    }
}
