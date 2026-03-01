namespace Skojjt.Core.Exports;


/// <summary>
/// Result of parsing a DAK XML file. Contains the parsed data and any issues found.
/// Since Softadmin provides no error messages, this gives detailed diagnostics.
/// </summary>
public class DakParseResult
{
    /// <summary>
    /// The parsed DAK data. Null if parsing failed completely.
    /// </summary>
    public DakData? Data { get; init; }

    /// <summary>
    /// All issues found during parsing, ordered by line number.
    /// </summary>
    public IReadOnlyList<DakParseIssue> Issues { get; init; } = [];

    /// <summary>
    /// True if the file was parsed successfully (may still have warnings).
    /// </summary>
    public bool Success => Data is not null && !Issues.Any(i => i.Severity == DakIssueSeverity.Error);

    /// <summary>
    /// True if any errors were found.
    /// </summary>
    public bool HasErrors => Issues.Any(i => i.Severity == DakIssueSeverity.Error);

    /// <summary>
    /// True if any warnings were found.
    /// </summary>
    public bool HasWarnings => Issues.Any(i => i.Severity == DakIssueSeverity.Warning);

    /// <summary>
    /// Source file name, if available.
    /// </summary>
    public string FileName { get; init; } = string.Empty;
}


/// <summary>
/// A single issue found during DAK XML parsing.
/// Designed to pinpoint exact location since Softadmin gives no error feedback.
/// </summary>
public record DakParseIssue
{
    /// <summary>
    /// Severity level.
    /// </summary>

    public required DakIssueSeverity Severity { get; init; }

    /// <summary>
    /// Human-readable description of the issue (in Swedish).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Line number in the XML file (1-based), or 0 if unknown.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// Column position in the XML file (1-based), or 0 if unknown.
    /// </summary>
    public int LinePosition { get; init; }

    /// <summary>
    /// XML element path where the issue was found
    /// (e.g., "Kommun/Foerening/Naervarokort/Sammankomster/Sammankomst[@kod='003']").
    /// </summary>
    public string XmlPath { get; init; } = string.Empty;

    /// <summary>
    /// The problematic value, if applicable.
    /// </summary>
    public string? ActualValue { get; init; }

    /// <summary>
    /// What was expected, if applicable.
    /// </summary>

    public string? ExpectedValue { get; init; }

    /// <summary>
    /// Formatted display string including location info for debugging.
    /// </summary>
    public override string ToString()
    {
        var location = LineNumber > 0 ? $"Rad {LineNumber}, kolumn {LinePosition}" : "Okänd plats";
        var path = !string.IsNullOrEmpty(XmlPath) ? $" ({XmlPath})" : "";
        var values = ActualValue is not null
            ? ExpectedValue is not null
                ? $" [fick: '{ActualValue}', förväntade: '{ExpectedValue}']"
                : $" [värde: '{ActualValue}']"
            : "";
        return $"[{Severity}] {location}{path}: {Message}{values}";
    }
}

/// <summary>
/// Severity levels for DAK parse issues.
/// </summary>
public enum DakIssueSeverity
{
    /// <summary>Information only.</summary>
    Info,

    /// <summary>Potential problem.</summary>
    Warning,

    /// <summary>Invalid data.</summary>
    Error
}
