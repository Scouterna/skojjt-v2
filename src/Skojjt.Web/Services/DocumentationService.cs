using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace Skojjt.Web.Services;

/// <summary>
/// Service that reads embedded markdown documentation files and converts them to HTML.
/// </summary>
public sealed class DocumentationService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private readonly Assembly _assembly = typeof(DocumentationService).Assembly;

    /// <summary>
    /// Gets a list of all available documentation pages with their slugs and titles.
    /// </summary>
    public IReadOnlyList<DocPage> GetPages()
    {
        var prefix = "Skojjt.Web.Docs.";
        var resources = _assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal);

        List<DocPage> pages = [];

        foreach (var resourceName in resources)
        {
            var fileName = resourceName[prefix.Length..^3]; // remove prefix and .md
            var markdown = ReadResource(resourceName);
            if (markdown is null)
                continue;

            var title = ExtractTitle(markdown) ?? fileName;
            pages.Add(new DocPage(fileName, title));
        }

        return pages;
    }

    /// <summary>
    /// Reads a documentation page by slug and returns the rendered HTML.
    /// </summary>
    public string? GetPageHtml(string slug)
    {
        var resourceName = $"Skojjt.Web.Docs.{slug}.md";
        var markdown = ReadResource(resourceName);
        if (markdown is null)
            return null;

        var html = Markdown.ToHtml(markdown, Pipeline);
        return NormalizeFragmentAnchors(html, slug);
    }

    /// <summary>
    /// Reads a documentation page by slug and returns the raw markdown.
    /// </summary>
    public string? GetPageMarkdown(string slug)
    {
        var resourceName = $"Skojjt.Web.Docs.{slug}.md";
        return ReadResource(resourceName);
    }

    private string? ReadResource(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Normalizes heading id attributes and fragment-only href attributes to ASCII
    /// by stripping diacritics (e.g. ä→a, ö→o, å→a), and rewrites fragment-only
    /// links to include the full page path so Blazor navigates within the page.
    /// </summary>
    private static string NormalizeFragmentAnchors(string html, string slug)
    {
        // Normalize id="..." on heading elements
        html = Regex.Replace(html, " id=\"([^\"]+)\"", m =>
            $" id=\"{RemoveDiacritics(m.Groups[1].Value)}\"");

        // Rewrite fragment-only href="#..." to full path "/hjalp/slug#..."
        // so Blazor's enhanced navigation stays on the documentation page
        // instead of navigating to the root URL.
        html = Regex.Replace(html, " href=\"#([^\"]+)\"", m =>
            $" href=\"/hjalp/{slug}#{RemoveDiacritics(m.Groups[1].Value)}\"");

        return html;
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? ExtractTitle(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                return trimmed[2..].Trim();
        }

        return null;
    }
}

/// <summary>
/// Represents a single documentation page.
/// </summary>
/// <param name="Slug">The resource slug used in the URL and for loading.</param>
/// <param name="Title">The display title extracted from the markdown heading.</param>
public sealed record DocPage(string Slug, string Title);
