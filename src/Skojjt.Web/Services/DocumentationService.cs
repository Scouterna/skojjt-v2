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

    /// <summary>
    /// Returns a short plain-text description for a page, suitable for a
    /// <c>&lt;meta name="description"&gt;</c> tag. Uses the first regular
    /// paragraph of the markdown (skipping the title heading), stripped of
    /// markdown formatting and truncated to roughly 160 characters.
    /// </summary>
    public string? GetPageDescription(string slug)
    {
        var markdown = GetPageMarkdown(slug);
        if (markdown is null)
            return null;

        return ExtractDescription(markdown);
    }

    private static string? ExtractDescription(string markdown)
    {
        var paragraph = new StringBuilder();

        foreach (var rawLine in markdown.Split('\n'))
        {
            var line = rawLine.Trim();

            // Skip headings, empty lines before content, and common block markers.
            if (line.Length == 0)
            {
                if (paragraph.Length > 0)
                    break; // paragraph finished
                continue;
            }

            if (line.StartsWith('#') || line.StartsWith('>') || line.StartsWith("---", StringComparison.Ordinal)
                || line.StartsWith('|') || line.StartsWith("```", StringComparison.Ordinal))
            {
                if (paragraph.Length > 0)
                    break;
                continue;
            }

            if (paragraph.Length > 0)
                paragraph.Append(' ');
            paragraph.Append(line);
        }

        if (paragraph.Length == 0)
            return null;

        var text = StripMarkdown(paragraph.ToString());
        return Truncate(text, 160);
    }

    private static string StripMarkdown(string text)
    {
        // Links: [label](url) -> label
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        // Emphasis / inline code markers
        text = Regex.Replace(text, @"[*_`~]", string.Empty);
        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength)
            return text;

        var slice = text[..maxLength];
        var lastSpace = slice.LastIndexOf(' ');
        if (lastSpace > 0)
            slice = slice[..lastSpace];

        return slice.TrimEnd() + "…";
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
