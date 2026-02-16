namespace Skojjt.Core.Exports;

/// <summary>
/// Result of comparing two DAK XML files.
/// </summary>
public class DakComparisonResult
{
    /// <summary>
    /// Changes in metadata (föreningsnamn, kommun-ID, etc.).
    /// </summary>
    public List<DakFieldChange> MetadataChanges { get; init; } = [];

    /// <summary>
    /// Sammankomster that exist only in the first (old) file.
    /// </summary>
    public List<DakSammankomst> RemovedSammankomster { get; init; } = [];

    /// <summary>
    /// Sammankomster that exist only in the second (new) file.
    /// </summary>
    public List<DakSammankomst> AddedSammankomster { get; init; } = [];

    /// <summary>
    /// Sammankomster that exist in both files but have changes.
    /// </summary>
    public List<DakSammankomstDiff> ModifiedSammankomster { get; init; } = [];

    /// <summary>
    /// Persons added to the register (not in old, in new).
    /// </summary>
    public List<DakDeltagare> AddedDeltagare { get; init; } = [];

    /// <summary>
    /// Persons removed from the register (in old, not in new).
    /// </summary>
    public List<DakDeltagare> RemovedDeltagare { get; init; } = [];

    /// <summary>
    /// Persons added to the leader register.
    /// </summary>
    public List<DakDeltagare> AddedLedare { get; init; } = [];

    /// <summary>
    /// Persons removed from the leader register.
    /// </summary>
    public List<DakDeltagare> RemovedLedare { get; init; } = [];

    /// <summary>
    /// True if there are no differences.
    /// </summary>
    public bool AreEqual =>
        MetadataChanges.Count == 0 &&
        RemovedSammankomster.Count == 0 &&
        AddedSammankomster.Count == 0 &&
        ModifiedSammankomster.Count == 0 &&
        AddedDeltagare.Count == 0 &&
        RemovedDeltagare.Count == 0 &&
        AddedLedare.Count == 0 &&
        RemovedLedare.Count == 0;
}

/// <summary>
/// A changed field between two DAK files.
/// </summary>
public record DakFieldChange(string FieldName, string? OldValue, string? NewValue);

/// <summary>
/// Differences within a single sammankomst that exists in both files.
/// </summary>
public class DakSammankomstDiff
{
    /// <summary>
    /// The meeting code used to match the two versions.
    /// </summary>
    public required string Kod { get; init; }

    /// <summary>
    /// Field-level changes (datum, tid, duration, typ, etc.).
    /// </summary>
    public List<DakFieldChange> FieldChanges { get; init; } = [];

    /// <summary>
    /// Deltagare added to this meeting.
    /// </summary>
    public List<DakDeltagare> AddedDeltagare { get; init; } = [];

    /// <summary>
    /// Deltagare removed from this meeting.
    /// </summary>
    public List<DakDeltagare> RemovedDeltagare { get; init; } = [];

    /// <summary>
    /// Ledare added to this meeting.
    /// </summary>
    public List<DakDeltagare> AddedLedare { get; init; } = [];

    /// <summary>
    /// Ledare removed from this meeting.
    /// </summary>
    public List<DakDeltagare> RemovedLedare { get; init; } = [];
}
