using System.Web;

namespace Skojjt.Shared;

/// <summary>
/// Utility for resolving image URLs that may be wrapped in CDN/optimization proxies.
/// For example, Next.js image optimization URLs embed the actual image URL as a query parameter.
/// </summary>
public static class ImageUrlHelper
{
    /// <summary>
    /// Known path segments that indicate the URL is an image optimization proxy wrapper.
    /// The actual image URL is expected in the <c>url</c> query parameter.
    /// </summary>
    private static readonly string[] ProxyPathSegments = ["/_next/image"];

    /// <summary>
    /// Resolves the actual image URL from a potentially wrapped proxy URL.
    /// If the URL contains a known proxy path (e.g. <c>/_next/image</c>) and a <c>url</c>
    /// query parameter, the decoded inner URL is returned. Otherwise the original URL is returned unchanged.
    /// </summary>
    /// <param name="imageUrl">The image URL to resolve. May be null or empty.</param>
    /// <returns>The resolved image URL, or the original value if no proxy wrapper was detected.</returns>
    public static string? ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return imageUrl;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            return imageUrl;

        var isProxy = Array.Exists(ProxyPathSegments,
            segment => uri.AbsolutePath.Contains(segment, StringComparison.OrdinalIgnoreCase));

        if (!isProxy)
            return imageUrl;

        var queryParams = HttpUtility.ParseQueryString(uri.Query);
        var innerUrl = queryParams["url"];

        if (string.IsNullOrWhiteSpace(innerUrl))
            return imageUrl;

        // Validate the extracted URL is actually a valid absolute URL
        if (!Uri.TryCreate(innerUrl, UriKind.Absolute, out _))
            return imageUrl;

        return innerUrl;
    }
}
