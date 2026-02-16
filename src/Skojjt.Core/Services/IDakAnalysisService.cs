using Skojjt.Core.Exports;
using Skojjt.Core.Exports.Validation;

namespace Skojjt.Core.Services;

/// <summary>
/// Service for analyzing DAK XML files: parse, compare, and validate.
/// </summary>
public interface IDakAnalysisService
{
    /// <summary>
    /// Parse a DAK XML file from a stream.
    /// </summary>
    DakParseResult Parse(Stream stream, string fileName = "");

    /// <summary>
    /// Parse a DAK XML file from a byte array.
    /// </summary>
    DakParseResult Parse(byte[] data, string fileName = "");

    /// <summary>
    /// Compare two DAK files and return a structured diff.
    /// Both files are normalized before comparison.
    /// </summary>
    DakComparisonResult Compare(DakData old, DakData @new);

    /// <summary>
    /// Validate a DAK file against aktivitetsbidrag rules and calculate expected grant.
    /// </summary>
    /// <param name="dak">The parsed DAK data.</param>
    /// <param name="settings">Municipality/year settings. If null, attempts to auto-detect from KommunId.</param>
    /// <param name="terminsAr">Semester year for age calculations.</param>
    AktivitetsbidragResult Validate(DakData dak, AktivitetsbidragSettings? settings, int terminsAr);
}
