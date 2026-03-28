using System.Reflection;
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

        return Markdown.ToHtml(markdown, Pipeline);
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
