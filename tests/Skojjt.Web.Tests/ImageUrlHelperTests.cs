using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Shared;

namespace Skojjt.Web.Tests;

[TestClass]
public class ImageUrlHelperTests
{
    [TestMethod]
    public void ResolveImageUrl_WithNextImageProxy_ReturnsInnerUrl()
    {
        // Arrange
        var proxyUrl = "https://www.scouterna.se/_next/image/?url=https%3A%2F%2Fmedia.scoutcontent.se%2Fuploads%2F2021%2F03%2F07a190a-4.png&w=3840&q=75";

        // Act
        var result = ImageUrlHelper.ResolveImageUrl(proxyUrl);

        // Assert
        Assert.AreEqual("https://media.scoutcontent.se/uploads/2021/03/07a190a-4.png", result);
    }

    [TestMethod]
    public void ResolveImageUrl_WithDirectUrl_ReturnsUnchanged()
    {
        // Arrange
        var directUrl = "https://media.scoutcontent.se/uploads/2021/03/07a190a-4.png";

        // Act
        var result = ImageUrlHelper.ResolveImageUrl(directUrl);

        // Assert
        Assert.AreEqual(directUrl, result);
    }

    [TestMethod]
    public void ResolveImageUrl_WithNull_ReturnsNull()
    {
        Assert.IsNull(ImageUrlHelper.ResolveImageUrl(null));
    }

    [TestMethod]
    public void ResolveImageUrl_WithEmpty_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, ImageUrlHelper.ResolveImageUrl(string.Empty));
    }

    [TestMethod]
    public void ResolveImageUrl_WithWhitespace_ReturnsWhitespace()
    {
        Assert.AreEqual("  ", ImageUrlHelper.ResolveImageUrl("  "));
    }

    [TestMethod]
    public void ResolveImageUrl_WithNextImageProxyMissingUrlParam_ReturnsOriginal()
    {
        // Arrange — has the proxy path but no url query parameter
        var proxyUrl = "https://www.scouterna.se/_next/image/?w=3840&q=75";

        // Act
        var result = ImageUrlHelper.ResolveImageUrl(proxyUrl);

        // Assert
        Assert.AreEqual(proxyUrl, result);
    }

    [TestMethod]
    public void ResolveImageUrl_WithNextImageProxyInvalidInnerUrl_ReturnsOriginal()
    {
        // Arrange — url param is not a valid absolute URL
        var proxyUrl = "https://www.scouterna.se/_next/image/?url=not-a-valid-url&w=3840&q=75";

        // Act
        var result = ImageUrlHelper.ResolveImageUrl(proxyUrl);

        // Assert
        Assert.AreEqual(proxyUrl, result);
    }

    [TestMethod]
    public void ResolveImageUrl_WithRelativeUrl_ReturnsUnchanged()
    {
        var relativeUrl = "/images/badge.png";
        Assert.AreEqual(relativeUrl, ImageUrlHelper.ResolveImageUrl(relativeUrl));
    }

    [TestMethod]
    public void ResolveImageUrl_WithNonProxyQueryString_ReturnsUnchanged()
    {
        // Arrange — has a url query parameter but not a proxy path
        var normalUrl = "https://example.com/page?url=https%3A%2F%2Fother.com%2Fimage.png";

        // Act
        var result = ImageUrlHelper.ResolveImageUrl(normalUrl);

        // Assert
        Assert.AreEqual(normalUrl, result);
    }
}
