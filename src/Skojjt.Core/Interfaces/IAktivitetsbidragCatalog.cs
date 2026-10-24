using Skojjt.Core.Exports.Validation;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Provides the built-in aktivitetsbidrag settings (rules and default amounts) from a data file.
/// </summary>
public interface IAktivitetsbidragCatalog
{
    /// <summary>
    /// Gets all built-in settings.
    /// </summary>
    Task<IReadOnlyList<AktivitetsbidragSettings>> GetAllAsync(CancellationToken ct = default);
}
