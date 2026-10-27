using System.Text.Json;
using System.Text.Json.Serialization;
using Skojjt.Core.Exports.Validation;
using Skojjt.Core.Interfaces;

namespace Skojjt.Web.Services;

/// <summary>
/// Loads and caches the built-in aktivitetsbidrag settings from
/// <c>wwwroot/data/aktivitetsbidrag.json</c>.
/// </summary>
public sealed class AktivitetsbidragCatalogService : IAktivitetsbidragCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IWebHostEnvironment _environment;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<AktivitetsbidragSettings>? _settings;

    public AktivitetsbidragCatalogService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<IReadOnlyList<AktivitetsbidragSettings>> GetAllAsync(CancellationToken ct = default)
    {
        if (_settings is not null)
        {
            return _settings;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_settings is not null)
            {
                return _settings;
            }

            var path = Path.Combine(_environment.WebRootPath, "data", "aktivitetsbidrag.json");
            await using var stream = File.OpenRead(path);
            var catalog = await JsonSerializer.DeserializeAsync<AktivitetsbidragCatalogFile>(stream, SerializerOptions, ct)
                ?? throw new InvalidOperationException("Kunde inte läsa aktivitetsbidragskatalogen.");

            _settings = catalog.Kommuner;
            return _settings;
        }
        finally
        {
            _lock.Release();
        }
    }

    private sealed class AktivitetsbidragCatalogFile
    {
        public string Version { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public IReadOnlyList<AktivitetsbidragSettings> Kommuner { get; init; } = [];
    }
}
