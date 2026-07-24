namespace Skojjt.Core.Exports;

/// <summary>
/// How a conflicting meeting (present in both the DAK file and Skojjt with different
/// data) should be resolved during an incremental import.
/// </summary>
public enum MeetingImportChoice
{
    /// <summary>Keep the meeting data currently in Skojjt (ignore the DAK file version).</summary>
    KeepExisting,

    /// <summary>Overwrite the Skojjt meeting with the data from the DAK file.</summary>
    UseImported
}

/// <summary>
/// Classification of a DAK file meeting relative to what already exists in Skojjt.
/// </summary>
public enum DakImportMeetingStatus
{
    /// <summary>Meeting does not exist in Skojjt and will be added.</summary>
    New,

    /// <summary>Meeting exists and is identical in all round-trippable fields; nothing to do.</summary>
    Unchanged,

    /// <summary>Meeting exists but differs; the user must choose which version wins.</summary>
    Conflict
}

/// <summary>
/// A meeting parsed from a DAK file, with persons resolved against Skojjt members.
/// </summary>
public class DakImportMeeting
{
    public required DateOnly Date { get; init; }
    public TimeOnly StartTime { get; init; }
    public int DurationMinutes { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool IsHike { get; init; }

    /// <summary>Skojjt person IDs (member numbers) that attended and exist as members.</summary>
    public IReadOnlyList<int> AttendingPersonIds { get; init; } = [];
}

/// <summary>
/// A snapshot of the meeting already stored in Skojjt, used to show the user what
/// the DAK file would replace.
/// </summary>
public class DakExistingMeeting
{
    public required int MeetingId { get; init; }
    public required DateOnly Date { get; init; }
    public TimeOnly StartTime { get; init; }
    public int DurationMinutes { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool IsHike { get; init; }
    public IReadOnlyList<int> AttendingPersonIds { get; init; } = [];
}

/// <summary>
/// A meeting that exists in both the DAK file and Skojjt with differing data.
/// The user chooses which version to keep.
/// </summary>
public class DakImportConflict
{
    public required DakImportMeeting Imported { get; init; }
    public required DakExistingMeeting Existing { get; init; }

    /// <summary>Human-readable (Swedish) descriptions of the fields that differ.</summary>
    public IReadOnlyList<string> Differences { get; init; } = [];
}

/// <summary>
/// A person referenced in the DAK file that could not be matched to a Skojjt member
/// and was therefore skipped.
/// </summary>
public record DakSkippedPerson(string Uid, string Name);

/// <summary>
/// A meeting in the DAK file whose date falls outside the target semester's date
/// range. Such meetings are never imported; they usually indicate the wrong semester
/// (or file) was selected.
/// </summary>
public record DakOutOfRangeMeeting(DateOnly Date, string Name);

/// <summary>
/// The result of analysing a DAK file against a specific troop + semester in Skojjt,
/// prior to applying any changes.
/// </summary>
public class DakImportPreview
{
    /// <summary>True when the file parsed well enough to be imported.</summary>
    public bool CanImport { get; init; }

    /// <summary>Fatal parse/validation errors preventing import (Swedish).</summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>Non-fatal warnings (Swedish).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Meetings that will be added because they do not yet exist in Skojjt.</summary>
    public IReadOnlyList<DakImportMeeting> NewMeetings { get; init; } = [];

    /// <summary>Meetings present in both but identical; no action needed.</summary>
    public IReadOnlyList<DakImportMeeting> UnchangedMeetings { get; init; } = [];

    /// <summary>Meetings present in both but different; require a user decision.</summary>
    public IReadOnlyList<DakImportConflict> Conflicts { get; init; } = [];

    /// <summary>Persons in the file that don't exist as Skojjt members and were skipped.</summary>
    public IReadOnlyList<DakSkippedPerson> SkippedPersons { get; init; } = [];

    /// <summary>Meetings skipped because their date is outside the target semester.</summary>
    public IReadOnlyList<DakOutOfRangeMeeting> OutOfRangeMeetings { get; init; } = [];
}

/// <summary>
/// Request to apply an incremental DAK import. The file is re-parsed to guarantee the
/// applied data matches what the user reviewed; <see cref="Decisions"/> resolves conflicts
/// keyed by meeting date.
/// </summary>
public class DakImportApplyRequest
{
    public required int ScoutGroupId { get; init; }
    public required int TroopId { get; init; }
    public required int SemesterId { get; init; }
    public required byte[] FileContent { get; init; }
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Choice per conflicting meeting date. Dates missing from this map default to
    /// <see cref="MeetingImportChoice.KeepExisting"/> (no change).
    /// </summary>
    public IReadOnlyDictionary<DateOnly, MeetingImportChoice> Decisions { get; init; }
        = new Dictionary<DateOnly, MeetingImportChoice>();
}

/// <summary>
/// Outcome of applying an incremental DAK import.
/// </summary>
public class DakImportResult
{
    public bool Success { get; init; }
    public int AddedMeetings { get; init; }
    public int UpdatedMeetings { get; init; }
    public int UnchangedMeetings { get; init; }
    public int SkippedPersons { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
