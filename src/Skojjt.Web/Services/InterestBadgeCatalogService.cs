using System.Text.Json;
using System.Text.Json.Serialization;

namespace Skojjt.Web.Services;

/// <summary>
/// Loads and caches the interest badge (intressemärken) catalog from the JSON
/// definition in <c>wwwroot/data/interest-badges.json</c>. The catalog describes
/// the badge progression graph used to render the overview map.
/// </summary>
public sealed class InterestBadgeCatalogService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private InterestBadgeCatalog? _catalog;

    public InterestBadgeCatalogService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Gets the parsed catalog, loading and caching it on first access.
    /// </summary>
    public async Task<InterestBadgeCatalog> GetCatalogAsync(CancellationToken ct = default)
    {
        if (_catalog is not null)
        {
            return _catalog;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_catalog is not null)
            {
                return _catalog;
            }

            var path = Path.Combine(_environment.WebRootPath, "data", "interest-badges.json");
            await using var stream = File.OpenRead(path);
            var catalog = await JsonSerializer.DeserializeAsync<InterestBadgeCatalog>(stream, SerializerOptions, ct)
                ?? throw new InvalidOperationException("Kunde inte läsa intressemärkeskatalogen.");

            _catalog = catalog;
            return _catalog;
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Root of the interest badge catalog.</summary>
public sealed class InterestBadgeCatalog
{
    public string Version { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<InterestBadgeLevel> Levels { get; init; } = [];

    public IReadOnlyList<InterestBadgeSeries> Series { get; init; } = [];

    public IReadOnlyList<InterestBadgeNode> Standalone { get; init; } = [];

    /// <summary>Finds a level definition by its key, or <c>null</c> if unknown.</summary>
    public InterestBadgeLevel? FindLevel(string key)
        => Levels.FirstOrDefault(l => string.Equals(l.Key, key, StringComparison.Ordinal));
}

/// <summary>An age-based scouting level (row in the map).</summary>
public sealed class InterestBadgeLevel
{
    public string Key { get; init; } = string.Empty;

    public int UnitTypeId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Color { get; init; } = "#607D8B";

    public int Order { get; init; }
}

/// <summary>A thematic progression series (column in the map).</summary>
public sealed class InterestBadgeSeries
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public IReadOnlyList<InterestBadgeNode> Badges { get; init; } = [];
}

/// <summary>A single badge within a series (or a standalone badge).</summary>
public sealed class InterestBadgeNode
{
    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Level { get; init; } = string.Empty;

    public int Order { get; init; }

    public IReadOnlyList<string> Next { get; init; } = [];

    public IReadOnlyList<string> Variants { get; init; } = [];

    /// <summary>Introductory text (Inledning) describing the badge.</summary>
    public string? Description { get; init; }

    /// <summary>The badge requirements (Märkeskriterier).</summary>
    public IReadOnlyList<string> Criteria { get; init; } = [];

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; init; }

    /// <summary>Image URLs for each variant, when the badge has several.</summary>
    [JsonPropertyName("variantImages")]
    public IReadOnlyList<string> VariantImages { get; init; } = [];
}
