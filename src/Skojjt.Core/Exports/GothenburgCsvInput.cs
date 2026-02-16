namespace Skojjt.Core.Exports;

/// <summary>
/// Input data for generating a Gothenburg municipality attendance grant CSV.
/// See: https://goteborg.se/wps/portal/start/foretag-och-organisationer/foreningar/kulturstod-och-bidrag-till-foreningar/ansok-om-bidrag-till-foreningar-inom-idrott-och-fritid/aktivitetsbidrag
/// </summary>
public class GothenburgCsvInput
{
    /// <summary>
    /// Scout group ID.
    /// </summary>
    public required int ScoutGroupId { get; init; }

    /// <summary>
    /// Semester ID for filtering.
    /// </summary>
    public required int SemesterId { get; init; }

    /// <summary>
    /// Whether to use semester-based attendance minimum (true) or year-based (false).
    /// </summary>
    public bool UseSemesterMinimum { get; init; } = false;
}
