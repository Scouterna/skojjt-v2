namespace Skojjt.Core.Exports.Validation;

/// <summary>
/// Result of aktivitetsbidrag validation.
/// Contains both rule validation issues and the calculated grant summary.
/// </summary>
public class AktivitetsbidragResult
{
    /// <summary>
    /// Validation issues found (rule violations, warnings, info).
    /// </summary>
    public required IReadOnlyList<DakParseIssue> Issues { get; init; }

    /// <summary>
    /// Calculated grant summary. Null if validation failed completely.
    /// </summary>
    public required BidragsSummering Summering { get; init; }

    /// <summary>
    /// True if there are any errors.
    /// </summary>
    public bool HasErrors => Issues.Any(i => i.Severity == DakIssueSeverity.Error);

    /// <summary>
    /// True if there are any warnings.
    /// </summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == DakIssueSeverity.Warning);
}
